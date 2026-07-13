using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SmartShoppingChatBot.API.Middlewares
{
    public class SwaggerSecurityOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation,
            OperationFilterContext context)
        {
            var group = context.ApiDescription.GroupName;

            if (group == "internal")
            {
                operation.Security =
                [
                    new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                }
                ];
            }

            if (group == "external")
            {
                operation.Security =
                [
                    new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "ApiKey"
                            }
                        },
                        Array.Empty<string>()
                    }
                }
                ];
            }
        }
    }
}
