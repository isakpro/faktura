using Faktura.Domain.PublicApi;
using Faktura.Domain.Webhooks;
using Xunit;

namespace Faktura.Domain.Tests;

public class ApiKeyTests
{
    private static readonly DateTime Now = new(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Generated_keys_are_unique_and_hash_deterministically()
    {
        var (raw1, prefix1) = ApiKeyGenerator.New();
        var (raw2, _) = ApiKeyGenerator.New();

        Assert.NotEqual(raw1, raw2);
        Assert.StartsWith("fkt_live_", raw1);
        Assert.Equal(raw1[..16], prefix1);
        Assert.Equal(ApiKeyGenerator.Hash(raw1), ApiKeyGenerator.Hash(raw1)); // deterministisk
        Assert.NotEqual(ApiKeyGenerator.Hash(raw1), ApiKeyGenerator.Hash(raw2));
    }

    [Fact]
    public void CreateNew_stores_hash_not_raw_key()
    {
        var (raw, _) = ApiKeyGenerator.New();
        var key = ApiKey.CreateNew("k-1", "t-1", "CI-nyckel", raw, [ApiScopes.InvoicesRead], Now);

        Assert.Equal(ApiKeyGenerator.Hash(raw), key.KeyHash);
        Assert.True(key.IsActive);
        Assert.True(key.HasScope(ApiScopes.InvoicesRead));
        Assert.False(key.HasScope(ApiScopes.CustomersWrite));
    }

    [Fact]
    public void Revoked_key_is_no_longer_active()
    {
        var (raw, _) = ApiKeyGenerator.New();
        var key = ApiKey.CreateNew("k-2", "t-1", "Tillfällig", raw, [ApiScopes.InvoicesRead], Now);

        key.Revoke(Now.AddMinutes(5));

        Assert.False(key.IsActive);
    }
}

public class WebhookSignerTests
{
    [Fact]
    public void Sign_is_deterministic_for_same_secret_and_body()
    {
        var sig1 = WebhookSigner.Sign("secret", "{\"a\":1}");
        var sig2 = WebhookSigner.Sign("secret", "{\"a\":1}");

        Assert.Equal(sig1, sig2);
        Assert.Equal(64, sig1.Length); // hex-kodad SHA-256 = 32 bytes = 64 hex-tecken
    }

    [Fact]
    public void Sign_differs_when_secret_or_body_differs()
    {
        var baseline = WebhookSigner.Sign("secret", "{\"a\":1}");

        Assert.NotEqual(baseline, WebhookSigner.Sign("other-secret", "{\"a\":1}"));
        Assert.NotEqual(baseline, WebhookSigner.Sign("secret", "{\"a\":2}"));
    }
}
