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
            
            //=========
            //start processing lines
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();  

                if (string.IsNullOrWhiteSpace(line))
                    continue;
                //check heading 
                if (IsMarkdownHeading(line, out var level, out var title))
                {
                    Flush();

                    headingStack[level] = title;

                    foreach (var key in headingStack.Keys.Where(k => k > level).ToList())
                        headingStack.Remove(key);

                    currentLevel = level;
                    currentTitle = title;
                    //1# > 2## > 3### 
                    currentHeadingPath = string.Join(" > ",headingStack.OrderBy(x => x.Key).Select(x => x.Value));
                    continue;
                }

                currentContent.AppendLine(line);
            }

            Flush();

            //when match new heading, flush the current content to a new section
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
            //scan each section
            foreach (var section in sections)
            {
                var content = new StringBuilder();
                //flush content to entries 
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

                        QdrantPointId = CreateDeterministicPointId(documentId, entries.Count),
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

        //Note: Check Markdown
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
            if (maxChars <= 0)
                yield break;

            var remaining = text.Trim();

            while (remaining.Length > maxChars)
            {
                // Find the nearest split index
                var splitIndex = FindNearestSplitIndex(remaining, maxChars);
                var chunkLength = splitIndex > 0 ? splitIndex : maxChars;
                var chunk = remaining[..chunkLength].Trim();

                if (!string.IsNullOrWhiteSpace(chunk))
                    yield return chunk;

                remaining = remaining[chunkLength..].TrimStart();
            }

            if (!string.IsNullOrWhiteSpace(remaining))
                yield return remaining;
        }

        private static int FindNearestSplitIndex(string text, int maxChars)
        {
            var limit = Math.Min(maxChars, text.Length);

            for (var i = limit - 1; i >= 0; i--)
            {
                if (IsChunkDelimiter(text[i]))//check ". ! ? \n ;"
                    return i + 1;
            }

            return -1;
        }

        private static bool IsChunkDelimiter(char character)
        {
            return character is '.' or '!' or '?' or '\n' or ';';
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
        private static string CreateDeterministicPointId(ObjectId documentId, int chunkIndex)
        {
            var input = Encoding.UTF8.GetBytes($"{documentId}:{chunkIndex}");
            var hash = System.Security.Cryptography.MD5.HashData(input);

            hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

            return new Guid(hash).ToString();
        }
    }
}
