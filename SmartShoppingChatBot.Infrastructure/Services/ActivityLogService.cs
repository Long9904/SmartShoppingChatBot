using Microsoft.Extensions.Logging;
using MongoDB.Bson;
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

        public ActivityLogService(IActivityLogRepository activityLogRepository, ICurrentUserService currentUserService, IUnitOfWork unitOfWork, ILogger<ActivityLogService> logger)
        {
            _activityLogRepository = activityLogRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
            _logger = logger;
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
                var business = await _currentUserService.GetBusiness();
                if (business == null || business.Data == null)
                {
                    throw new Exception("Business not found for the current user.");
                }
                var actor = await _currentUserService.GetUser();
                if (actor == null || actor.Data == null)
                {
                    throw new Exception("Actor not found for the current user.");
                }
                var log = new ActivityLog
                {
                    BusinessId = business.Data.Id.ToString(),
                    ActorId = actor.Data.Id.ToString(),
                    ActorEmail = actor.Data.Email,
                    ActorRole = actor.Data.Business.Role,
                    Action = activityLog.Action,
                    TargetType = activityLog.TargetType,
                    TargetId = activityLog.TargetId,
                    Status = activityLog.Status,
                    Severity = activityLog.Severity,
                    Description = activityLog.Description,
                    IpAddress = activityLog.IpAddress,
                    MetadataJson = activityLog.Metadata != null ? JsonSerializer.Serialize(activityLog.Metadata, _jsonSerializer) : null,
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
