using MediatR;
using Microsoft.AspNetCore.Http;
using SmartShoppingChatBot.Application.Commons.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.ProfileManagement.ChangePassword
{
    public class ChangePasswordCommand : IRequest<Result<string>>
    {
        public string currentPassword { get; init; } = string.Empty;
        public string newPassword { get; init; } = string.Empty;
        public string confirmPassword { get; init; } = string.Empty;
    }
}
