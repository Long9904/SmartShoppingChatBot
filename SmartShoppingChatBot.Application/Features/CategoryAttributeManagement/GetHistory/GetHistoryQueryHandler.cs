using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.GetHistory;

public sealed class GetHistoryQueryHandler : IRequestHandler<GetHistoryQuery, Result<List<CategoryAttributeSchemaResponse>>>
{
    private readonly ICategoryAttributeSchemaRepository _repository;
    private readonly IMapper _mapper;

    public GetHistoryQueryHandler(ICategoryAttributeSchemaRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<Result<List<CategoryAttributeSchemaResponse>>> Handle(
        GetHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var schemas = await _repository.GetByCategoryAsync(request.Category.Trim());
        var response = _mapper.Map<List<CategoryAttributeSchemaResponse>>(schemas);
        return Result<List<CategoryAttributeSchemaResponse>>.Success(
            response,
            200,
            "Category attribute schema history retrieved successfully.");
    }
}
