using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class DocumentSemanticSearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public int TopK { get; set; } = 5;
        public int CandidateLimit { get; set; } = 20;
        public string? DocumentId { get; set; }
    }
}
