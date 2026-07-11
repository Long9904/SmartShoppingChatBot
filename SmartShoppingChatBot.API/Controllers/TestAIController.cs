using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.API.Controllers
{
    [Route("api/v1/ai-test")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "internal")]
    public class TestAIController : ControllerBase
    {
        private readonly IGeminiService _service;
        private readonly IQwenService _qwenService;

        public TestAIController(IGeminiService service, IQwenService qwenService)
        {
            _service = service;
            _qwenService = qwenService;
        }

        [HttpPost("gemini/chat")]
        public async Task<IActionResult> GenerateText([FromBody] string prompt)
        {
            var result = await _service.GenerateTextAsync(prompt, 9000, 0.7);
            if (result.IsSuccess)
            {
                return Ok(result.Data);
            }
            else
            {
                return BadRequest(result.Message);
            }
        }
        [HttpPost("gemini/embeddings")]
        public async Task<IActionResult> GenerateEmbeddings([FromBody] string input)
        {
            var result = await _service.EmbeddingsAsync(input);
            if (result.IsSuccess)
            {
                return Ok(result.Data);
            }
            else
            {
                return BadRequest(result.Message);
            }
        }

        [HttpPost("qwen/chat")]
        public async Task<IActionResult> GenerateQwenChat([FromBody] string prompt)
        {
            var result = await _qwenService.GenerateTextAsync(prompt, 2000, 0.7, false);
            if (result.IsSuccess)
            {
                return Ok(result.Data);
            }
            else
            {
                return BadRequest(result.Message);
            }
        }
    }
}
