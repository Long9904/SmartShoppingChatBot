using FluentValidation;
using MongoDB.Bson;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.SendMessage
{
    public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
    {
        public SendMessageCommandValidator()
        {

            RuleFor(x => x.Message)
                .NotEmpty().WithMessage("Message cannot empty")
                .MaximumLength(1024).WithMessage("Message length is not over 1024 letter");

            RuleFor(x => x.ExternalCustomerId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("ExternalCustomerId cannot empty")
                .MaximumLength(50).WithMessage("External customer ID cannot exced 50 charator");


            RuleFor(x => x.ConversationId)
                .Cascade(CascadeMode.Stop)
                .Must(ConversationId => ObjectId.TryParse(ConversationId, out _))
                .WithMessage("Conversation Id not valid")
                .When(x => !string.IsNullOrEmpty(x.ConversationId));
        }
    }
}
