using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.GetPendingApproval;

public sealed class GetPendingApprovalQueryHandler
    : IRequestHandler<GetPendingApprovalQuery, Result<List<CategoryAttributeSchemaResponse>>>
{
    private readonly ICategoryAttributeSchemaRepository _repository;
    private readonly IMapper _mapper;

    public GetPendingApprovalQueryHandler(ICategoryAttributeSchemaRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<Result<List<CategoryAttributeSchemaResponse>>> Handle(
        GetPendingApprovalQuery request,
        CancellationToken cancellationToken)
    {
        var schemas = await _repository.GetPendingApprovalAsync();
        var response = _mapper.Map<List<CategoryAttributeSchemaResponse>>(schemas);
        return Result<List<CategoryAttributeSchemaResponse>>.Success(
            response,
            200,
            "Pending category attribute schemas retrieved successfully.");
    }
}
