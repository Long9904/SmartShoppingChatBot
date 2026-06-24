using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.UpdateMyProfile
{
    public class UpdateMyProfileCommandHandler : IRequestHandler<UpdateMyProfileCommand, Result<ProfileResponse>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBusinessRepository _businessRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public UpdateMyProfileCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork,
            IBusinessRepository businessRepository, ICurrentUserService currentUserService, IMapper mapper)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _businessRepository = businessRepository;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }

        public async Task<Result<ProfileResponse>> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
        {
            var isUser = await _currentUserService.GetUser();
            if (isUser == null)
            {
                return Result<ProfileResponse>.Failure(404, "User not found.");
            }
            var user = isUser.Data;
            if (user == null)
            {
                return Result<ProfileResponse>.Failure(404, "User not found.");
            }
            var business = await _businessRepository.FindAsync(x => x.Id == user.Business.Id);
            if(business == null)
            {
                return Result<ProfileResponse>.Failure(404, "Business not found.");
            }
            var isUpdate = false;
            //update user information
            if (!string.IsNullOrEmpty(request.FullName) && user.FullName != request.FullName)
            {
                user.FullName = request.FullName;
                isUpdate = true;
            }
            if (!string.IsNullOrEmpty(request.PhoneNumber) && user.PhoneNumber != request.PhoneNumber)
            {
                user.PhoneNumber = request.PhoneNumber;
                isUpdate = true;
            }
            if(request.DateOfBirth.HasValue && user.DateOfBirth != request.DateOfBirth)
            {
                user.DateOfBirth = request.DateOfBirth;
                isUpdate = true;
            }
            if (request.Gender.HasValue && user.Gender != request.Gender)
            {
                user.Gender = request.Gender;
                isUpdate = true;
            }
            //update business information
            //if (!string.IsNullOrEmpty(request.BusinessName) && business.BusinessName != request.BusinessName)
            //{
            //    business.BusinessName = request.BusinessName;
            //    isUpdate = true;
            //}
            if (!string.IsNullOrEmpty(request.AddressLine) && business.AddressLine != request.AddressLine)
            {
                business.AddressLine = request.AddressLine;
                isUpdate = true;
            }
            if(!string.IsNullOrEmpty(request.Hotline) && business.HotLine != request.Hotline)
            {
                business.HotLine = request.Hotline;
                isUpdate = true;
            }
            if(!string.IsNullOrEmpty(request.WebsiteUrl) && business.WebsiteUrl != request.WebsiteUrl)
            {
                business.WebsiteUrl = request.WebsiteUrl;
                isUpdate = true;
            }         
            if (isUpdate)
            {
                await _userRepository.UpdateAsync(user);
                await _businessRepository.UpdateAsync(business);
                await _unitOfWork.SaveChangesAsync();
            }
            var response = _mapper.Map<ProfileResponse>(user);
            return Result<ProfileResponse>.Success(response);
        }
    }
}
