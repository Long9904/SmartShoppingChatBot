using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly IActivityLogRepository _activityLogRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly JsonSerializerOptions _jsonSerializer;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ActivityLogService> _logger;
        private readonly IUserRepository _userRepository;

        public ActivityLogService(IActivityLogRepository activityLogRepository, ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork, ILogger<ActivityLogService> logger, IUserRepository userRepository)
        {
            _activityLogRepository = activityLogRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _userRepository = userRepository;
            _jsonSerializer = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };
        }


        public async Task LogAsync(ActivityLogRequest activityLog)
        {
            try
            {
                if (activityLog == null)
                    return;

                
                var actor = await _currentUserService.GetUser();
                var actorId = activityLog.ActorId ?? actor.Data?.Id.ToString();
                var actorInfo = await _userRepository.FindAsync(x => x.Id.ToString() == actorId);

                var business = await _currentUserService.GetBusiness();
                var businessId = business.Data?.Id.ToString() ?? actorInfo?.Business.Id.ToString();
                var ipAddress = _currentUserService.GetIpAddress();

                var log = new ActivityLog
                {
                    BusinessId = businessId,
                    ActorId = actorInfo?.Id.ToString(),
                    ActorEmail = actorInfo?.Email,
                    ActorRole = actorInfo?.Business?.Role,

                    Action = activityLog.Action,
                    TargetType = activityLog.TargetType,
                    TargetId = activityLog.TargetId,
                    Status = activityLog.Status,
                    Severity = activityLog.Severity,
                    Description = activityLog.Description,
                    IpAddress = ipAddress,
                    MetadataJson = activityLog.Metadata != null
                        ? JsonSerializer.Serialize(activityLog.Metadata, _jsonSerializer)
                        : null,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                await _activityLogRepository.AddAsync(log);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while logging activity.");
            }
        }
    }
}
