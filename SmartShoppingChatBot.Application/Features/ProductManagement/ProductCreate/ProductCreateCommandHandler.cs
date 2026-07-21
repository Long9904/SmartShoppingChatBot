using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductCreate
{
    public class ProductCreateCommandHandler : IRequestHandler<ProductCreateCommand, Result<ProductResponse>>
    {
        private readonly ILogger<ProductCreateCommandHandler> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly IProductRepository _productRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _time;
        private readonly IBusinessQuotaRepository _businessQuotaRepository;
        private readonly IPublishEndpoint _publisher;
        private readonly IHttpContextAccessor _httpContextAccessor;


        public ProductCreateCommandHandler(
            ILogger<ProductCreateCommandHandler> logger,
            ICurrentUserService currentUserService,
            IProductRepository productRepository,
            IUnitOfWork unitOfWork,
            IPublishEndpoint publisher,
            TimeProvider timeProvider,
            IHttpContextAccessor httpContextAccessor,
            IBusinessQuotaRepository businessQuotaRepository)
        {
            _logger = logger;
            _currentUserService = currentUserService;
            _productRepository = productRepository;
            _unitOfWork = unitOfWork;
            _publisher = publisher;
            _time = timeProvider;
            _businessQuotaRepository = businessQuotaRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<Result<ProductResponse>> Handle(ProductCreateCommand request, CancellationToken cancellationToken)
        {
            var business = await _currentUserService.GetBusiness();
            if (!business.IsSuccess || business.Data == null)
            {
                return Result<ProductResponse>.Failure(business.StatusCode, business.Message, null, business.MessageCode);
            }

            var existingProduct = await _productRepository.FindAsync(p =>
            p.BusinessId == business.Data.Id
            && p.ExternalId == request.ExternalId
            && p.Status != ProductStatus.Deleted);

            if (existingProduct != null)
            {
                return Result<ProductResponse>.Failure(409, "ExternalId is exsiting", null, ProductMessageCode.ExternalIdConflict);
            }


            var businessQuota = await _businessQuotaRepository.FindAsync(b => b.BusinessId == business.Data.Id);
            if (businessQuota == null)
                return Result<ProductResponse>.Failure(404, "Business quota not found", null, BusinessQuotaMessageCode.NotFound);

            var productCount = await _productRepository.CountAsync(p => p.BusinessId == business.Data.Id && p.Status != ProductStatus.Deleted);


            if (productCount >= businessQuota.MaxProductAllowed)
            {
                return Result<ProductResponse>.Failure(400, "Rate limit for create new product", null, ProductMessageCode.ProdcutRateLimit);
            }



            var pointId = Guid.NewGuid();
            var dateNow = _time.GetUtcNow();
            var productId = ObjectId.GenerateNewId();


            var product = new Product
            {
                Id = productId,
                BusinessId = business.Data!.Id,
                ExternalId = request.ExternalId,
                ExternalProductUrl = request.ExternalProductUrl ?? "",
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                Currency = request.Currency,
                Brand = request.Brand,
                StockQuantity = request.StockQuantity,
                Category = request.Category,
                Status = ProductStatus.PendingEmbedding,
                Images = request.Images,
                Metadata = request.Metadata,
                QdrantPointId = pointId,
                CreatedAt = dateNow,
                UpdatedAt = dateNow,
                EmbbbedAt = dateNow,
            };

            // External or internal

            var authType = _httpContextAccessor.HttpContext?.User.Identity?.AuthenticationType;
            Console.WriteLine(authType + "------------------------------------");

            if ("ApiKey".Equals(authType))
            {
                product.CreatedBy = new UserEmbedded
                {
                    Name = "Business: " + business.Data.BusinessName,
                };

                product.UpdatedBy = new UserEmbedded
                {
                    Name = "Business: " + business.Data.BusinessName,
                };
            }
            else
            {
                var user = await _currentUserService.GetUser();

                if (!user.IsSuccess) return Result<ProductResponse>.Failure(user.StatusCode, user.Message, user.Errors, user.MessageCode);

                product.CreatedBy = new UserEmbedded
                {
                    Id = user.Data!.Id,
                    Name = user.Data.FullName,
                };

                product.UpdatedBy = new UserEmbedded
                {
                    Id = user.Data!.Id,
                    Name = user.Data.FullName,
                };
            }


            // Build search text
            var embeddingText = product.BuildEmbeddingText();
            product.SearchContent = embeddingText;


            try
            {
                await _unitOfWork.BeginTransactionAsync();
                await _productRepository.AddAsync(product);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                // Publish the ProductCreateEvent 
                await _publisher.Publish(new ProductCreateEvent
                {
                    ProductId = productId.ToString(),
                    QdrantPointId = pointId
                }, cancellationToken);

                return Result<ProductResponse>.Success(
                    ProductMappings.ToResponse(product),
                    statusCode: 200,
                    message: "Product create success",
                    messageCode: ProductMessageCode.CreateSuccess
                );

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when saving product");
                await _unitOfWork.RollBackAsync();
                return Result<ProductResponse>.Failure(500, "Server error", null, "MG_SERVER_500");
            }
        }


    }
}
