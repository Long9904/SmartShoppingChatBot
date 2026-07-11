using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Events;
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

        public ProductCreateCommandHandler(
            ILogger<ProductCreateCommandHandler> logger,
            ICurrentUserService currentUserService,
            IProductRepository productRepository,
            IUnitOfWork unitOfWork,
            IPublishEndpoint publisher,
            TimeProvider timeProvider, 
            IBusinessQuotaRepository businessQuotaRepository)
        {
            _logger = logger;
            _currentUserService = currentUserService;
            _productRepository = productRepository;
            _unitOfWork = unitOfWork;
            _publisher = publisher;
            _time = timeProvider;
            _businessQuotaRepository = businessQuotaRepository;
        }

        public async Task<Result<ProductResponse>> Handle(ProductCreateCommand request, CancellationToken cancellationToken)
        {
            var business = await _currentUserService.GetBusiness();
            if (!business.IsSuccess || business.Data == null )
            {
                return Result<ProductResponse>.Failure(business.StatusCode, business.Message);
            }

            var user = await _currentUserService.GetUser();

            if (!user.IsSuccess || user.Data == null)
            {
                return Result<ProductResponse>.Failure(user.StatusCode, user.Message);
            }

            var existingProduct = await _productRepository.FindAsync(p => 
            p.BusinessId == business.Data.Id 
            && p.ExternalId == request.ExternalId
            && p.Status != ProductStatus.Deleted);

            if (existingProduct != null)
            {
                return Result<ProductResponse>.Failure(409, "Id của sản phẫm tồn tại");
            }


            var businessQuota = await _businessQuotaRepository.FindAsync(b => b.BusinessId == business.Data.Id);
            if (businessQuota == null) 
                return Result<ProductResponse>.Failure(404, "Hạn mức của doanh nghiệp không thể tìm thấy");

            var productCount = await _productRepository.CountAsync(p => p.BusinessId == business.Data.Id && p.Status != ProductStatus.Deleted);

            if (productCount >= businessQuota.MaxProductAllowed)
            {
                return Result<ProductResponse>.Failure(400, "Doanh nghiệp đã đạt tới giới hạn tạo sản phẩm");
            }

            var pointId = Guid.NewGuid();
            var dateNow = _time.GetUtcNow();
            var productId = ObjectId.GenerateNewId();
             
            var product = new Product
            {
                Id = productId,
                BusinessId = business.Data!.Id,
                ExternalId = request.ExternalId,
                ExternalProductUrl = request.ExternalProductUrl,
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

                CreatedBy = new UserEmbedded
                {
                    Id = user.Data!.Id,
                    Name = user.Data.FullName,
                },

                UpdatedBy = new UserEmbedded
                {
                    Id = user.Data.Id,
                    Name = user.Data.FullName,
                },

                EmbbbedAt = dateNow,
            };


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

                return Result<ProductResponse>.Success(new ProductResponse
                {
                    Id = product.Id.ToString(),
                    BusinessId = product.BusinessId.ToString(),
                    ExternalId = product.ExternalId,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    Currency = product.Currency,
                    Brand = product.Brand,
                    StockQuantity = product.StockQuantity,
                    Category = product.Category,
                    Status = product.Status,
                    Images = product.Images,
                    CreatedAt = product.CreatedAt,
                    Metadata = product.Metadata
                },
                message: "Sản phẩm được tạo thành công"
                );

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình lưu sản phẩm");
                await _unitOfWork.RollBackAsync();
                return Result<ProductResponse>.Failure(500, "Lỗi server");
            }
        }


    }
}
