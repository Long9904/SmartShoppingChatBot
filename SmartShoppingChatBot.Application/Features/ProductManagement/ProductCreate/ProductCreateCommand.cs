using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductCreate
{
    public class ProductCreateCommand : IRequest<Result<ProductResponse>>
    {
        public string ExternalId { get; set; } = default!; // Unique identifier for the product in the external system

        public string Name { get; set; } = default!;

        public string? Description { get; set; }

        public string? ExternalProductUrl { get; set; }

        public decimal Price { get; set; }

        public string Currency { get; set; } = "VND";

        public string? Brand { get; set; }

        public int StockQuantity { get; set; }

        public string Category { get; set; } = default!; // e.g., laptop > MSI or phone > Samsung

        public List<string> Images { get; set; } = [];

        public Dictionary<string, string> Metadata { get; set; } = [];
    }
}
