namespace GloryCafe.Application.Products.Queries.GetProducts;

public record ProductDto(
    int Id,
    string Name,
    string Description,
    decimal Price,
    string? ImageUrl,
    bool IsAvailable,
    int CategoryId,
    string CategoryName);
