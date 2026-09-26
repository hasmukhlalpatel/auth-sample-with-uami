using Microsoft.AspNetCore.Authorization;
namespace AuthDemo.Endpoints;

/// <summary>
/// Minimal API endpoints — products resource.
/// Registered via IEndpointRouteBuilder extension to keep Program.cs clean.
///
/// GET  /api/products          → any authenticated user
/// GET  /api/products/{id}     → any authenticated user
/// POST /api/products          → Manager or Admin role
/// DELETE /api/products/{id}   → Admin role only
/// </summary>
public static class ProductEndpoints
{
    // In-memory store — replace with EF Core / repository in production
    private static readonly List<ProductDto> Store =
    [
        new(1, "Laptop",     999.99m,  "Electronics"),
        new(2, "Desk Chair", 299.50m,  "Furniture"),
        new(3, "Notebook",   4.99m,    "Stationery"),
        new(4, "Monitor",    449.00m,  "Electronics"),
    ];

    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/products")
            .WithTags("Products (Minimal API)")
            .RequireAuthorization();          // all endpoints in the group need a valid JWT

        // GET /api/products — any authenticated user
        group.MapGet("/", () => Results.Ok(Store))
             .WithName("GetAllProducts")
             .WithSummary("Returns all products (any authenticated user)");

        // GET /api/products/{id} — any authenticated user
        group.MapGet("/{id:int}", (int id) =>
        {
            var product = Store.FirstOrDefault(p => p.Id == id);
            return product is null ? Results.NotFound() : Results.Ok(product);
        })
        .WithName("GetProductById")
        .WithSummary("Returns a single product by ID (any authenticated user)");

        // POST /api/products — Manager or Admin
        group.MapPost("/", [Authorize(Roles = "Manager,Admin")] (ProductDto dto) =>
        {
            var newProduct = dto with { Id = Store.Count > 0 ? Store.Max(p => p.Id) + 1 : 1 };
            Store.Add(newProduct);
            return Results.Created($"/api/products/{newProduct.Id}", newProduct);
        })
        .WithName("CreateProduct")
        .WithSummary("Creates a product (Manager or Admin role required)");

        // DELETE /api/products/{id} — Admin only
        group.MapDelete("/{id:int}", [Authorize(Roles = "Admin")] (int id) =>
        {
            var product = Store.FirstOrDefault(p => p.Id == id);
            if (product is null) return Results.NotFound();

            Store.Remove(product);
            return Results.NoContent();
        })
        .WithName("DeleteProduct")
        .WithSummary("Deletes a product (Admin role required)");

        return app;
    }
}
public record ProductDto(int Id, string Name, decimal Price, string Category);