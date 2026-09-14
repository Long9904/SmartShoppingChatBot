using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.GetAllSchemas;

public sealed class GetAllSchemasQueryHandler
    : IRequestHandler<GetAllSchemasQuery, Result<List<CategoryAttributeSchemaResponse>>>
{
    private readonly ICategoryAttributeSchemaRepository _repository;
    private readonly IMapper _mapper;

    public GetAllSchemasQueryHandler(ICategoryAttributeSchemaRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<Result<List<CategoryAttributeSchemaResponse>>> Handle(
        GetAllSchemasQuery request,
        CancellationToken cancellationToken)
    {
        var schemas = await _repository.GetLatestSchemasAsync();
        var response = _mapper.Map<List<CategoryAttributeSchemaResponse>>(schemas);
        return Result<List<CategoryAttributeSchemaResponse>>.Success(
            response,
            200,
            "Latest category attribute schemas retrieved successfully.");
    }
}
