using System.Reflection;
using System.Text.Json;
using FluentValidation;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;

namespace SmartShoppingChatBot.Application.Commons.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
     where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (!_validators.Any()) return await next();

            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();

            if (failures.Count != 0)
            {
                var errorsDictionary = failures
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => JsonNamingPolicy.CamelCase.ConvertName(g.Key),
                        g => string.Join(", ", g.Select(e => e.ErrorMessage))
                    );


                var responseType = typeof(TResponse);

                if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
                {
                    var method = responseType.GetMethod(
                        "Failure",
                        BindingFlags.Public | BindingFlags.Static,
                        binder: null,
                        types: new[]
                        {
                            typeof(int),
                            typeof(string),
                            typeof(Dictionary<string, string>)
                        },
                        modifiers: null
                    );

                    if (method != null)
                    {
                        return (TResponse)method.Invoke(null, new object?[]
                        {
                            400, 
                            "Validation failed", 
                            errorsDictionary
                        })!;
                    }
                }
            }
            return await next();
        }
    }
}
