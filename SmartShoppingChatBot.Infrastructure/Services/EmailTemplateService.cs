using Microsoft.AspNetCore.Hosting;
using RazorLight;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class EmailTemplateService : IEmailTemplateService
    {
        private readonly RazorLightEngine _engine;


        public EmailTemplateService(IWebHostEnvironment env)
        {
            _engine = new RazorLightEngineBuilder()
                .UseFileSystemProject(
                    Path.Combine(
                        env.ContentRootPath,
                        "EmailTemplates"))
                .UseMemoryCachingProvider()
                .Build();
        }

        public async Task<string> RenderEmailTemplateAsync<T>(string templateName, T model)
        {
            return await _engine.CompileRenderAsync($"{templateName}.cshtml", model);
        }
    }
}
