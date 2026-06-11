using Microsoft.AspNetCore.Identity;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services;

public class PasswordService : IPasswordService
{
    private readonly PasswordHasher<object> _passwordHasher = new();
    private readonly object _dummyUser = new();
    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(_dummyUser, password);
    }

    public bool VerifyPassword(string providedPassword, string hashedPassword)
    {
        var result = _passwordHasher.VerifyHashedPassword(_dummyUser, hashedPassword, providedPassword);
        return result == PasswordVerificationResult.Success;
    }
}
