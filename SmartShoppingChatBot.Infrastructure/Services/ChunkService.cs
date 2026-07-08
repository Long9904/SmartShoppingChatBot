using MongoDB.Bson;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using System.Text;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class ChunkService : IChunkService
    {
        public async Task<List<DocumentSection>> SplitMarkdownByHeadingAsync(string markdown)
        {
            var sections = new List<DocumentSection>();
            var lines = markdown.Split('\n');
            var headingStack = new Dictionary<int, string>();

            var currentContent = new StringBuilder();
            var currentLevel = 1;
            var currentTitle = "Document";
            var currentHeadingPath = currentTitle;

            void Flush()
            {
                var content = currentContent.ToString().Trim();

                if (string.IsNullOrWhiteSpace(content))
                    return;

                sections.Add(new DocumentSection
                {
                    SectionIndex = sections.Count,
                    Level = currentLevel,
                    Title = currentTitle,
                    HeadingPath = currentHeadingPath,
                    MarkdownContent = content,
                    SectionSummary = BuildSectionSummary(content),
                    TokenCount = EstimateTokenCount(content)
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

                    currentLevel = level;
                    currentTitle = title;
                    currentHeadingPath = string.Join(" > ",
                        headingStack.OrderBy(x => x.Key).Select(x => x.Value));

                    continue;
                }

                currentContent.AppendLine(line);
            }

            Flush();

            return await Task.FromResult(sections);
        }

        public async Task<List<KnowledgeEntry>> ChunkSectionsAsync(
            IReadOnlyList<DocumentSection> sections,
            string fileName,
            ObjectId businessId,
            ObjectId documentId,
            int maxCharsPerChunk = 1800)
        {
            var entries = new List<KnowledgeEntry>();

            foreach (var section in sections)
            {
                var content = new StringBuilder();

                void Flush()
                {
                    var chunkContent = content.ToString().Trim();

                    if (string.IsNullOrWhiteSpace(chunkContent))
                        return;

                    var contextualContent =
                        $"""
                        Tài liệu: {fileName}
                        Mục: {section.HeadingPath}
                        Tóm tắt mục: {section.SectionSummary}
                        """;

                    entries.Add(new KnowledgeEntry
                    {
                        Id = ObjectId.GenerateNewId(),
                        BusinessId = businessId,
                        DocumentId = documentId,

                        QdrantPointId = Guid.NewGuid().ToString(),
                        ChunkIndex = entries.Count,

                        SectionId = section.SectionId,
                        SectionIndex = section.SectionIndex,
                        SectionTitle = section.Title,
                        SectionSummary = section.SectionSummary,

                        Content = chunkContent,
                        ContextualContent = contextualContent,
                        EmbeddingText = $"{contextualContent}\n\nNội dung chunk:\n{chunkContent}",

                        HeadingPath = section.HeadingPath,
                        TokenCount = EstimateTokenCount(chunkContent),
                        PageStart = section.PageStart,
                        PageEnd = section.PageEnd,

                        FileName = fileName,
                        SourceType = "knowledge_document",
                        CreatedAt = DateTime.UtcNow
                    });

                    content.Clear();
                }

                foreach (var paragraph in SplitParagraphs(section.MarkdownContent))
                {
                    if (content.Length > 0 && content.Length + paragraph.Length + 1 > maxCharsPerChunk)
                    {
                        Flush();
                    }

                    if (paragraph.Length > maxCharsPerChunk)
                    {
                        Flush();

                        foreach (var slice in SplitLongText(paragraph, maxCharsPerChunk))
                        {
                            content.AppendLine(slice);
                            Flush();
                        }

                        continue;
                    }

                    content.AppendLine(paragraph);
                }

                Flush();
            }

            return await Task.FromResult(entries);
        }

        public async Task<List<KnowledgeEntry>> ChunkMarkdownAsync(
            string markdown,
            string fileName,
            ObjectId businessId,
            ObjectId documentId,
            int maxCharsPerChunk = 1800)
        {
            var sections = await SplitMarkdownByHeadingAsync(markdown);
            return await ChunkSectionsAsync(sections, fileName, businessId, documentId, maxCharsPerChunk);
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

        private static IEnumerable<string> SplitParagraphs(string text)
        {
            return text
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));
        }

        private static IEnumerable<string> SplitLongText(string text, int maxChars)
        {
            for (var i = 0; i < text.Length; i += maxChars)
            {
                yield return text.Substring(i, Math.Min(maxChars, text.Length - i));
            }
        }

        private static string BuildSectionSummary(string content)
        {
            var normalized = string.Join(" ", SplitParagraphs(content));

            if (normalized.Length <= 500)
                   return normalized;

            return normalized[..500].TrimEnd() + "...";
        }

        private static int EstimateTokenCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            return (int)Math.Ceiling(text.Length / 4.0);
        }
    }
}
