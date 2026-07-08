using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IChunkService
    {
        Task<List<KnowledgeEntry>> ChunkMarkdownAsync(string markdown, string fileName, ObjectId businessId, ObjectId documentId, int maxCharsPerChunk = 1800);
    }
}
