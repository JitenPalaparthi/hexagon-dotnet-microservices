using Customer.Grpc;
using CustomerGrpcServer.Data;
using CustomerGrpcServer.Models;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace CustomerGrpcServer.Services;

public class CustomerService(AppDbContext db) : CustomerManager.CustomerManagerBase
{
    public override async Task<CreateCustomerResponse> CreateCustomer(CreateCustomerRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Name and Email are required."));

        var entity = new CustomerEntity
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim()
        };
        db.Customers.Add(entity);
        await db.SaveChangesAsync(context.CancellationToken);// graceful execution

        return new CreateCustomerResponse { Customer = Map(entity) };
    }

    public override async Task<GetCustomerResponse> GetCustomer(GetCustomerRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid GUID."));

        var entity = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, context.CancellationToken);
        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Customer not found."));

        return new GetCustomerResponse { Customer = Map(entity) };
    }

    public override async Task<UpdateCustomerResponse> UpdateCustomer(UpdateCustomerRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid GUID."));

        var entity = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, context.CancellationToken);
        if (entity is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Customer not found."));

        if (!string.IsNullOrWhiteSpace(request.Name)) entity.Name = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.Email)) entity.Email = request.Email.Trim();

        await db.SaveChangesAsync(context.CancellationToken);

        return new UpdateCustomerResponse { Customer = Map(entity) };
    }

    public override async Task<DeleteCustomerResponse> DeleteCustomer(DeleteCustomerRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid GUID."));

        var entity = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, context.CancellationToken);
        if (entity is null)
            return new DeleteCustomerResponse { Success = false };

        db.Customers.Remove(entity);
        await db.SaveChangesAsync(context.CancellationToken);

        return new DeleteCustomerResponse { Success = true };
    }

    public override async Task<ListCustomersResponse> ListCustomers(ListCustomersRequest request, ServerCallContext context)
    {
        var pageSize = request.PageSize > 0 ? Math.Min(request.PageSize, 100) : 50;
        var offset = 0;
        if (!string.IsNullOrEmpty(request.PageToken))
            int.TryParse(request.PageToken, out offset);

        var query = db.Customers.AsNoTracking().OrderBy(c => c.CreatedAt);
        var items = await query.Skip(offset).Take(pageSize).ToListAsync(context.CancellationToken);
        var nextToken = items.Count == pageSize ? (offset + pageSize).ToString() : "";

        var resp = new ListCustomersResponse();
        resp.Customers.AddRange(items.Select(Map));
        resp.NextPageToken = nextToken;
        return resp;
    }

    private static Customer.Grpc.Customer Map(CustomerEntity e) => new()
    {
        Id = e.Id.ToString(),
        Name = e.Name,
        Email = e.Email,
        CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(e.CreatedAt, DateTimeKind.Utc))
    };
}
