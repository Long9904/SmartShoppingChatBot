using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Interface;
using System.Security.Cryptography;
using System.Text;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class HashService : IHashService
    {
        private readonly ApiConfigs _apiConfigs;
        private readonly IDataProtector _dataProtector;

        public HashService(IOptions<ApiConfigs> options, IDataProtectionProvider provider)
        {
            _apiConfigs = options.Value;
            _dataProtector = provider.CreateProtector("SmartShopping.ApiKeySecret.v1");
        }

        public string HmacSha256(string value)
        {
            var key = _apiConfigs.SecretKey;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));

            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));

            return Convert.ToBase64String(hash);
        }

        public string Encrypt(string secret)
        {
            return _dataProtector.Protect(secret);
        }

        public string Decrypt(string encryptedSecret)
        {
            return _dataProtector.Unprotect(encryptedSecret);
        }
    }
}
