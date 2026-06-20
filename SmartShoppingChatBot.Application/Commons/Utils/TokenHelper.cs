using System.Security.Cryptography;
using System.Text;

namespace SmartShoppingChatBot.Application.Commons.Utils;

public static class TokenHelper
{
    public static string BuildHashToken(string input)
    {
        using var sha = SHA256.Create();

        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));

        return Convert.ToHexString(bytes);
    }
}