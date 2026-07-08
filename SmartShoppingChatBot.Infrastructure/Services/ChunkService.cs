using MongoDB.Bson;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class ChunkService : IChunkService
    {
        public async Task<List<KnowledgeEntry>> ChunkMarkdownAsync(
            string markdown,
            string fileName,
            ObjectId businessId,
            ObjectId documentId,
            int maxCharsPerChunk = 1800)
        {
            var entries = new List<KnowledgeEntry>();

            var lines = markdown.Split('\n');
            var headingStack = new Dictionary<int, string>();

            var currentContent = new StringBuilder();
            var currentHeadingPath = "";

            void Flush()
            {
                var content = currentContent.ToString().Trim();

                if (string.IsNullOrWhiteSpace(content))
                    return;

                var contextualContent =
                    $"""
                    Tài liệu: {fileName}
                    Mục: {currentHeadingPath}
                    """;

                entries.Add(new KnowledgeEntry
                {
                    Id = ObjectId.GenerateNewId(),
                    BusinessId = businessId,
                    DocumentId = documentId,

                    QdrantPointId = Guid.NewGuid().ToString(),
                    ChunkIndex = entries.Count,

                    Content = content,
                    ContextualContent = contextualContent,
                    EmbeddingText = $"{contextualContent}\n\n{content}",

                    HeadingPath = currentHeadingPath,
                    TokenCount = EstimateTokenCount(content),

                    FileName = fileName,
                    SourceType = "knowledge_document",
                    CreatedAt = DateTime.UtcNow
                });

                currentContent.Clear();
            }

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (IsMarkdownHeading(line, out var level, out var title))
                {
                    Flush();

                    headingStack[level] = title;

                    foreach (var key in headingStack.Keys.Where(k => k > level).ToList())
                        headingStack.Remove(key);

                    currentHeadingPath = string.Join(" > ",
                        headingStack.OrderBy(x => x.Key).Select(x => x.Value));

                    continue;
                }

                if (currentContent.Length + line.Length > maxCharsPerChunk)
                {
                    Flush();
                }

                currentContent.AppendLine(line);
            }

            Flush();

            return entries;
        }
        private bool IsMarkdownHeading(string line, out int level, out string title)
        {
            level = 0;
            title = "";

            if (!line.StartsWith("#"))
                return false;

            level = line.TakeWhile(c => c == '#').Count();

            if (level < 1 || level > 6)
                return false;

            title = line[level..].Trim();

            return !string.IsNullOrWhiteSpace(title);
        }

        private int EstimateTokenCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;
            return (int)Math.Ceiling(text.Length / 4.0);
        }
    }
}
