namespace SmartShoppingChatBot.Application.Interface
{
    public interface IHashService
    {
        string HmacSha256(string value);
        string Encrypt(string secret);
        string Decrypt(string encryptedSecret);
    }
}
