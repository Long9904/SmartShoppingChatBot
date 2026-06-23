using FluentValidation;
using MongoDB.Bson;

namespace SmartShoppingChatBot.Application.Features.UserManagement.GetUserById
{
    public class GetUserByIdQueryValidator : AbstractValidator<GetUserByIdQuery>
    {
        public GetUserByIdQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.")
                .Must(id => ObjectId.TryParse(id, out _)).WithMessage("UserId must be a valid");
        }
    }
}
