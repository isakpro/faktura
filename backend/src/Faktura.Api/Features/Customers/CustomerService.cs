using Faktura.Domain.Abstractions;
using Faktura.Domain.Common;
using Faktura.Domain.Customers;

namespace Faktura.Api.Features.Customers;

/// <summary>Tenant-scoped hantering av kunder.</summary>
public sealed class CustomerService
{
    private readonly ITenantContext _tenant;
    private readonly ICustomerRepository _customers;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;

    public CustomerService(ITenantContext tenant, ICustomerRepository customers, IIdGenerator ids, IClock clock)
    {
        _tenant = tenant;
        _customers = customers;
        _ids = ids;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CustomerDto>> ListAsync(CancellationToken ct)
        => (await _customers.ListByTenantAsync(_tenant.TenantId, ct)).Select(ToDto).ToList();

    public async Task<Result<CustomerDto>> GetAsync(string id, CancellationToken ct)
    {
        var c = await _customers.GetByIdAsync(_tenant.TenantId, id, ct);
        return c is null ? Result.Failure<CustomerDto>(Error.NotFound()) : Result.Success(ToDto(c));
    }

    public async Task<Result<CustomerDto>> CreateAsync(CustomerRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Result.Failure<CustomerDto>(Error.Validation("Kundnamn krävs."));

        var customer = Customer.CreateNew(_ids.NewId(), _tenant.TenantId, req.Name, req.Email,
            req.OrgNumber, req.VatNumber, ToAddress(req.Address), req.PaymentTermsDays, _clock.UtcNow);
        await _customers.AddAsync(customer, ct);
        return Result.Success(ToDto(customer));
    }

    public async Task<Result<CustomerDto>> UpdateAsync(string id, CustomerRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Result.Failure<CustomerDto>(Error.Validation("Kundnamn krävs."));

        var customer = await _customers.GetByIdAsync(_tenant.TenantId, id, ct);
        if (customer is null) return Result.Failure<CustomerDto>(Error.NotFound());

        customer.Update(req.Name, req.Email, req.OrgNumber, req.VatNumber, ToAddress(req.Address), req.PaymentTermsDays);
        await _customers.UpdateAsync(customer, ct);
        return Result.Success(ToDto(customer));
    }

    public async Task<Result> ArchiveAsync(string id, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(_tenant.TenantId, id, ct);
        if (customer is null) return Result.Failure(Error.NotFound());
        customer.Archive();
        await _customers.UpdateAsync(customer, ct);
        return Result.Success();
    }

    private static Address? ToAddress(AddressDto? a) =>
        a is null ? null : new Address(a.Line1, a.Line2, a.PostalCode, a.City, a.Country);

    internal static CustomerDto ToDto(Customer c) => new(
        c.Id, c.Name, c.Email, c.OrgNumber, c.VatNumber,
        c.Address is null ? null : new AddressDto(c.Address.Line1, c.Address.Line2, c.Address.PostalCode, c.Address.City, c.Address.Country),
        c.PaymentTermsDays, c.Status.ToString());
}
