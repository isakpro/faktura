using System.Collections.Concurrent;
using Faktura.Domain.Abstractions;
using Faktura.Domain.Authentication;
using Faktura.Domain.Organizations;
using Faktura.Domain.Users;

namespace Faktura.Api.Tests.Fakes;

/// <summary>In-memory user store so API tests run without MongoDB/Docker.</summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _users = new();

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default)
        => Task.FromResult(_users.Values.Any(u => u.Email == normalizedEmail));

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default)
        => Task.FromResult(_users.Values.FirstOrDefault(u => u.Email == normalizedEmail));

    public Task<User?> GetByIdAsync(string tenantId, string userId, CancellationToken ct = default)
        => Task.FromResult(_users.Values.FirstOrDefault(u => u.Id == userId && u.TenantId == tenantId));

    public Task<int> CountByTenantAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult(_users.Values.Count(u => u.TenantId == tenantId));

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryOrganizationRepository : IOrganizationRepository
{
    private readonly ConcurrentDictionary<string, Organization> _orgs = new();

    public Task AddAsync(Organization organization, CancellationToken ct = default)
    {
        _orgs[organization.Id] = organization;
        return Task.CompletedTask;
    }

    public Task<Organization?> GetByIdAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult(_orgs.GetValueOrDefault(tenantId));

    public Task UpdateAsync(Organization organization, CancellationToken ct = default)
    {
        _orgs[organization.Id] = organization;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ConcurrentDictionary<string, RefreshTokenRecord> _tokens = new();

    public Task AddAsync(RefreshTokenRecord record, CancellationToken ct = default)
    {
        _tokens[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task<RefreshTokenRecord?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => Task.FromResult(_tokens.Values.FirstOrDefault(r => r.TokenHash == tokenHash));

    public Task UpdateAsync(RefreshTokenRecord record, CancellationToken ct = default)
    {
        _tokens[record.Id] = record;
        return Task.CompletedTask;
    }
}
