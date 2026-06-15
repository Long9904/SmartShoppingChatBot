using DnsClient.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Infrastructure.Seeders;

public class UserSeeder
{
    private readonly MongoDbContext _context;
    private IConfiguration _configuration;
    private readonly IPasswordService _passwordService;
    private readonly ILogger<UserSeeder> _logger;
    private readonly TimeProvider _time;

    public UserSeeder(
        MongoDbContext context,
        IConfiguration configuration,
        IPasswordService passwordService,
        ILogger<UserSeeder> logger,
        TimeProvider time)
    {
        _context = context;
        _configuration = configuration;
        _passwordService = passwordService;
        _logger = logger;
        _time = time;
    }

    public async Task SeedUsersAsync()
    {
        var fullName = _configuration.GetSection("UserSeeder:FullName").Value;
        var usersEmail = _configuration.GetSection("UserSeeder:Email").Value;
        var usersPassword = _configuration.GetSection("UserSeeder:Password").Value;
        var usersRole = _configuration.GetSection("UserSeeder:Role").Value;
        var businessName = _configuration.GetSection("UserSeeder:BusinessName").Value;

        if (string.IsNullOrEmpty(usersEmail)
            || string.IsNullOrEmpty(usersPassword)
            || string.IsNullOrEmpty(businessName))
        {
            return;
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == usersEmail);
        if (user != null)
        {
            _logger.LogInformation("UserSeeder: User with email {Email} already exists. Skipping seeding.", usersEmail);
            return;
        }

        if (!Enum.TryParse<RoleEnums>(usersRole, out var role))
        {
            return;

        }

        var userId = ObjectId.GenerateNewId();
        var businessId = ObjectId.GenerateNewId();

        var newUser = new User
        {
            Id = userId,
            FullName = fullName,
            Email = usersEmail,
            PasswordHash = _passwordService.HashPassword(usersPassword),
            UserStatus = UserStatus.ACTIVE,
            IsEmailVerified = true,
            IsProfileCompleted = true,
            CreatedAt = _time.GetUtcNow(),
            UpdatedAt = _time.GetUtcNow(),
            Gender = 2,
            Business = new BusinessEmbedded
            {
                Id = businessId,
                BusinessName = businessName,
                Role = role,
                JoinedAt = _time.GetUtcNow(),
            }

        };

        var business = new Business
        {
            Id = businessId,
            BusinessName = businessName,
            BusinessStatus = BusinessEnums.ACTIVE,
        };

        await _context.Businesses.AddAsync(business);
        await _context.Users.AddAsync(newUser);
        await _context.SaveChangesAsync();
        _logger.LogInformation("UserSeeder: Seeded user successfully");
    }
}
