using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.ProfileManagement.ForgotPassword
{
    public class ForgotPasswordCommand : IRequest<Result<string>>
    {
        public string Email { get; init; } = string.Empty;
    }
}
