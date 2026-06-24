using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.UpdateMyProfile
{
    public class UpdateMyProfileCommand : IRequest<Result<ProfileResponse>>
    {
        //update user profile information
        public string FullName { get; init; } = string.Empty;
        public string? PhoneNumber { get; init; }
        public DateTime? DateOfBirth { get; init; }
        public int? Gender { get; init; } 

        //update business
        //public string BusinessName { get; init; } = string.Empty;
        public string Hotline { get; init; } = string.Empty;
        public string WebsiteUrl { get; init; } = string.Empty;
        public string AddressLine { get; init; } = string.Empty;
    }
}
