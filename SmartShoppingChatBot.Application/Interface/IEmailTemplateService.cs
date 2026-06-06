namespace SmartShoppingChatBot.Application.Interface;

public interface IEmailTemplateService
{
    Task<string> RenderEmailTemplateAsync<T>(string templateName, T model);
}
