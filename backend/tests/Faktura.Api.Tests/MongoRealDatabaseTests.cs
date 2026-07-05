using Faktura.Domain.Articles;
using Faktura.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;

namespace Faktura.Api.Tests;

/// <summary>
/// Integrationstester mot en RIKTIG MongoDB (Testcontainers). Verifierar det som in-memory-
/// fakes inte kan bevisa: index-semantik (unikt partial-index för SKU), tenant-filter på
/// riktiga queries och nummerseriens atomicitet under parallellism. Skippas snyggt när
/// Docker inte är tillgängligt (körs alltid i CI).
/// </summary>
public sealed class MongoContainerFixture : IAsyncLifetime
{
    private MongoDbContainer? _container;

    public MongoContext? Context { get; private set; }
    public bool Available => Context is not null;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new MongoDbBuilder().Build();
            await _container.StartAsync();
            Context = new MongoContext(Options.Create(new MongoOptions
            {
                ConnectionString = _container.GetConnectionString(),
                Database = "faktura_integration"
            }));
            await Context.EnsureIndexesAsync();
        }
        catch (Exception)
        {
            Context = null; // Docker saknas — [SkippableFact] hoppar över testerna.
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }
}

public class MongoRealDatabaseTests : IClassFixture<MongoContainerFixture>
{
    private readonly MongoContainerFixture _mongo;

    public MongoRealDatabaseTests(MongoContainerFixture mongo) => _mongo = mongo;

    private MongoContext Context
    {
        get
        {
            Skip.IfNot(_mongo.Available, "Docker/Testcontainers är inte tillgängligt på den här maskinen.");
            return _mongo.Context!;
        }
    }

    private static string NewId() => ObjectId.GenerateNewId().ToString();

    private static Article NewArticle(string tenantId, string name, string? sku) =>
        Article.CreateNew(NewId(), tenantId, name, sku, "st", 100m, 25, DateTime.UtcNow).Value;

    [SkippableFact]
    public async Task Sku_unique_index_is_per_tenant_and_ignores_missing_sku()
    {
        var repo = new MongoArticleRepository(Context);
        var (t1, t2) = (NewId(), NewId());

        await repo.AddAsync(NewArticle(t1, "A", "SKU-1"));

        // Samma SKU i samma tenant → unikt indexet slår till.
        var duplicate = await Record.ExceptionAsync(() => repo.AddAsync(NewArticle(t1, "B", "SKU-1")));
        var write = Assert.IsType<MongoWriteException>(duplicate);
        Assert.Equal(ServerErrorCategory.DuplicateKey, write.WriteError.Category);

        // Samma SKU i en annan tenant är OK (unikheten är per tenant).
        await repo.AddAsync(NewArticle(t2, "C", "SKU-1"));

        // Flera artiklar UTAN sku i samma tenant är OK (partial-index — sparse hade kolliderat här).
        await repo.AddAsync(NewArticle(t1, "U1", null));
        await repo.AddAsync(NewArticle(t1, "U2", null));

        Assert.Equal(3, (await repo.ListByTenantAsync(t1)).Count);
    }

    [SkippableFact]
    public async Task Tenant_scoped_repository_filters_real_queries()
    {
        var repo = new MongoArticleRepository(Context);
        var (t1, t2) = (NewId(), NewId());
        var secret = NewArticle(t1, "Hemlig", null);
        await repo.AddAsync(secret);

        Assert.Null(await repo.GetByIdAsync(t2, secret.Id));            // direkt id-access cross-tenant
        Assert.DoesNotContain(await repo.ListByTenantAsync(t2), a => a.Id == secret.Id);
        Assert.NotNull(await repo.GetByIdAsync(t1, secret.Id));         // egen tenant når den
    }

    [SkippableFact]
    public async Task Invoice_number_sequence_is_atomic_under_parallelism()
    {
        var sequence = new MongoInvoiceNumberSequence(Context);
        var tenant = NewId();
        var otherTenant = NewId();

        const int n = 40;
        var numbers = await Task.WhenAll(Enumerable.Range(0, n).Select(_ => sequence.NextAsync(tenant)));

        Assert.Equal(n, numbers.Distinct().Count());                     // inga dubbletter
        Assert.Equal(Enumerable.Range(1, n).Select(x => (long)x),
            numbers.OrderBy(x => x));                                    // obruten 1..n

        Assert.Equal(1, await sequence.NextAsync(otherTenant));          // serier är per tenant
    }
}
