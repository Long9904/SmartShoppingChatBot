using MongoDB.Bson;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface IChunkService
    {
        Task<List<DocumentSection>> SplitMarkdownByHeadingAsync(string markdown);

        Task<List<KnowledgeEntry>> ChunkSectionsAsync(
            IReadOnlyList<DocumentSection> sections,
            string fileName,
            ObjectId businessId,
            ObjectId documentId,
            int maxCharsPerChunk = 1800);

        Task<List<KnowledgeEntry>> ChunkMarkdownAsync(
            string markdown,
            string fileName,
            ObjectId businessId,
            ObjectId documentId,
            int maxCharsPerChunk = 1800);
    }
}
