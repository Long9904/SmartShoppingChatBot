using System.Linq.Expressions;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Moq;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Features.DocumentManagement.DeleteDocument;
using SmartShoppingChatBot.Application.Features.DocumentManagement.DocumentSemanticSearch;
using SmartShoppingChatBot.Application.Features.DocumentManagement.EmbeddingDocument;
using SmartShoppingChatBot.Application.Features.DocumentManagement.GetAllDocument;
using SmartShoppingChatBot.Application.Features.DocumentManagement.UploadDocument;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.UnitTests;

public class UT_DocumentManagement
{
    [Fact]
    public async Task Upload_WhenBusinessMissing_ReturnsOriginalFailureWithoutUploading()
    {
        var fixture = new DocumentFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(401, "Invalid business"));

        var result = await fixture.UploadHandler.Handle(new UploadDocCommand
        {
            Files = [FormFile("policy.pdf", "application/pdf")]
        }, CancellationToken.None);

        result.StatusCode.Should().Be(401);
        fixture.Cloudinary.Verify(service => service.UploadFileAsync(
            It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        fixture.DocumentRepository.Verify(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<KnowledgeDocument>>()), Times.Never);
    }

    [Fact]
    public async Task Upload_WhenNoFilesProvided_ReturnsFailureWithoutSaving()
    {
        var fixture = new DocumentFixture();

        var result = await fixture.UploadHandler.Handle(new UploadDocCommand { Files = [] }, CancellationToken.None);

        result.StatusCode.Should().Be(500);
        result.Message.Should().Be("No files were uploaded");
        fixture.DocumentRepository.Verify(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<KnowledgeDocument>>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_WhenFilesContainSuccessAndFailure_SavesOnlySuccessfulDocumentsAndPublishesEvents()
    {
        var fixture = new DocumentFixture();
        var validFile = FormFile("return-policy.pdf", "application/pdf", 200);
        var failedFile = FormFile("broken.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", 300);
        List<KnowledgeDocument>? savedDocuments = null;
        fixture.Cloudinary.Setup(service => service.UploadFileAsync(
                validFile, fixture.Business.Id.ToString(), "knowledge-documents"))
            .ReturnsAsync(Result<UploadedFileResponse>.Success(new UploadedFileResponse
            {
                FileName = "return-policy.pdf",
                PublicId = "knowledge-documents/return-policy",
                ContentType = "application/pdf",
                FileUrl = "https://cdn.example/return-policy.pdf",
                SizeInBytes = 200
            }));
        fixture.Cloudinary.Setup(service => service.UploadFileAsync(
                failedFile, fixture.Business.Id.ToString(), "knowledge-documents"))
            .ReturnsAsync(Result<UploadedFileResponse>.Failure(500, "Cloudinary failed"));
        fixture.DocumentRepository.Setup(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<KnowledgeDocument>>()))
            .Callback<IEnumerable<KnowledgeDocument>>(documents => savedDocuments = documents.ToList())
            .Returns(Task.CompletedTask);

        var result = await fixture.UploadHandler.Handle(new UploadDocCommand
        {
            Files = [validFile, failedFile]
        }, CancellationToken.None);

        result.StatusCode.Should().Be(201);
        result.Data!.Items.Should().HaveCount(2);
        result.Data.Items.First().Status.Should().Be(KnowledgeDocumentStatus.Uploaded);
        result.Data.Items.Last().Status.Should().Be(KnowledgeDocumentStatus.Failed);
        savedDocuments.Should().ContainSingle();
        savedDocuments![0].BusinessId.Should().Be(fixture.Business.Id);
        savedDocuments[0].Title.Should().Be("return-policy");
        savedDocuments[0].Type.Should().Be("pdf");
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Publisher.Verify(publisher => publisher.Publish(
            It.Is<DocumentUploadedEvent>(message =>
                message.DocumentId == savedDocuments[0].Id.ToString()
                && message.BusinessId == fixture.Business.Id.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDocuments_WhenBusinessMissing_ReturnsNotFoundWithoutQueryingDocuments()
    {
        var fixture = new DocumentFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(404, "Business not found"));

        var result = await fixture.GetHandler.Handle(new GetDocumentQuery(), CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.DocumentRepository.Verify(repository => repository.AsQueryable(), Times.Never);
    }

    [Fact]
    public async Task GetDocuments_WhenFilterProvided_ReturnsOnlyCurrentBusinessDocumentsOrderedNewestFirst()
    {
        var fixture = new DocumentFixture();
        var older = fixture.Document("policy-old.pdf", KnowledgeDocumentStatus.Uploaded, TestData.Now.AddDays(-2));
        var newest = fixture.Document("policy-new.pdf", KnowledgeDocumentStatus.Uploaded, TestData.Now);
        var failed = fixture.Document("policy-failed.pdf", KnowledgeDocumentStatus.Failed, TestData.Now.AddDays(-1));
        var otherBusiness = fixture.Document(
            "policy-other.pdf",
            KnowledgeDocumentStatus.Uploaded,
            TestData.Now.AddDays(1),
            ObjectId.GenerateNewId());
        fixture.SetupDocuments([older, newest, failed, otherBusiness]);

        var result = await fixture.GetHandler.Handle(new GetDocumentQuery
        {
            Filter = new GetDocumentFilter
            {
                FileName = "policy",
                Status = KnowledgeDocumentStatus.Uploaded,
                PageIndex = 1,
                PageSize = 10
            }
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Select(item => item.Id).Should().Equal(newest.Id.ToString(), older.Id.ToString());
        result.Data.Items.Should().OnlyContain(item => item.BusinessId == fixture.Business.Id.ToString());
    }

    [Fact]
    public async Task Delete_WhenBusinessMissing_ReturnsForbiddenWithoutLookup()
    {
        var fixture = new DocumentFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(403, "Forbidden"));

        var result = await fixture.DeleteHandler.Handle(
            new DeleteDocumentCommand { DocumentId = ObjectId.GenerateNewId().ToString() },
            CancellationToken.None);

        result.StatusCode.Should().Be(403);
        fixture.DocumentRepository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<KnowledgeDocument, bool>>>(),
            It.IsAny<Func<IQueryable<KnowledgeDocument>, IQueryable<KnowledgeDocument>>?>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenDocumentIdInvalid_ReturnsBadRequestWithoutLookup()
    {
        var fixture = new DocumentFixture();

        var result = await fixture.DeleteHandler.Handle(
            new DeleteDocumentCommand { DocumentId = "not-object-id" },
            CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.DocumentRepository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<KnowledgeDocument, bool>>>(),
            It.IsAny<Func<IQueryable<KnowledgeDocument>, IQueryable<KnowledgeDocument>>?>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenDocumentMissing_ReturnsNotFoundWithoutDeletingEntries()
    {
        var fixture = new DocumentFixture();
        fixture.DocumentRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<KnowledgeDocument, bool>>>(),
                It.IsAny<Func<IQueryable<KnowledgeDocument>, IQueryable<KnowledgeDocument>>?>()))
            .ReturnsAsync((KnowledgeDocument?)null);

        var result = await fixture.DeleteHandler.Handle(
            new DeleteDocumentCommand { DocumentId = ObjectId.GenerateNewId().ToString() },
            CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.EntryRepository.Verify(repository => repository.FindAllAsync(
            It.IsAny<Expression<Func<KnowledgeEntry, bool>>>(),
            It.IsAny<Func<IQueryable<KnowledgeEntry>, IQueryable<KnowledgeEntry>>?>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenDocumentExists_MarksDeletedDeletesEntriesAndQdrantPoints()
    {
        var fixture = new DocumentFixture();
        var document = fixture.Document("catalog.pdf", KnowledgeDocumentStatus.Uploaded, TestData.Now);
        var validPointId = Guid.NewGuid();
        var entries = new List<KnowledgeEntry>
        {
            fixture.Entry(document.Id, validPointId.ToString()),
            fixture.Entry(document.Id, "not-a-guid")
        };
        fixture.DocumentRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<KnowledgeDocument, bool>>>(),
                It.IsAny<Func<IQueryable<KnowledgeDocument>, IQueryable<KnowledgeDocument>>?>()))
            .ReturnsAsync(document);
        fixture.EntryRepository.Setup(repository => repository.FindAllAsync(
                It.IsAny<Expression<Func<KnowledgeEntry, bool>>>(),
                It.IsAny<Func<IQueryable<KnowledgeEntry>, IQueryable<KnowledgeEntry>>?>()))
            .ReturnsAsync(entries);

        var result = await fixture.DeleteHandler.Handle(
            new DeleteDocumentCommand { DocumentId = document.Id.ToString() },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        document.Status.Should().Be(KnowledgeDocumentStatus.Deleted);
        document.ProcessedAt.Should().NotBeNull();
        fixture.Qdrant.Verify(service => service.DeletePointsAsync(
            QdrantCollections.Documents,
            It.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { validPointId })),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.DocumentRepository.Verify(repository => repository.UpdateAsync(document), Times.Once);
        fixture.EntryRepository.Verify(repository => repository.DeleteAsync(entries[0].Id), Times.Once);
        fixture.EntryRepository.Verify(repository => repository.DeleteAsync(entries[1].Id), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DocumentSemanticSearch_WhenBusinessMissing_ReturnsNotFoundWithoutEmbedding()
    {
        var fixture = new DocumentFixture();
        fixture.CurrentUser.Setup(service => service.GetBusiness())
            .ReturnsAsync(Result<Business>.Failure(404, "Business not found"));

        var result = await fixture.SemanticSearchHandler.Handle(new DocumentSemanticSearchQuery
        {
            Request = new DocumentSemanticSearchRequest { Query = "return policy" }
        }, CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.Gemini.Verify(service => service.EmbeddingsAsyncV2(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DocumentSemanticSearch_WhenDocumentIdInvalid_ReturnsBadRequestWithoutEmbedding()
    {
        var fixture = new DocumentFixture();

        var result = await fixture.SemanticSearchHandler.Handle(new DocumentSemanticSearchQuery
        {
            Request = new DocumentSemanticSearchRequest
            {
                Query = "return policy",
                DocumentId = "invalid"
            }
        }, CancellationToken.None);

        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("Invalid document id.");
        fixture.Gemini.Verify(service => service.EmbeddingsAsyncV2(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DocumentSemanticSearch_WhenEmbeddingFails_ReturnsDocumentsNotFound()
    {
        var fixture = new DocumentFixture();
        fixture.Gemini.Setup(service => service.EmbeddingsAsyncV2(
                It.IsAny<string>(), "RETRIEVAL_QUERY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GeminiResponse<double[]>>.Failure(500, "embedding failed"));

        var result = await fixture.SemanticSearchHandler.Handle(new DocumentSemanticSearchQuery
        {
            Request = new DocumentSemanticSearchRequest { Query = "return policy" }
        }, CancellationToken.None);

        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("No relevant documents found");
        fixture.Qdrant.Verify(service => service.HybridDocumentSearchAsync(
            It.IsAny<float[]>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Filter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DocumentSemanticSearch_WhenQdrantReturnsNoEntryIds_ReturnsDocumentsNotFound()
    {
        var fixture = new DocumentFixture();
        fixture.SetupSemanticEmbedding();
        fixture.Qdrant.Setup(service => service.HybridDocumentSearchAsync(
                It.IsAny<float[]>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Filter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ScoredPoint()]);

        var result = await fixture.SemanticSearchHandler.Handle(new DocumentSemanticSearchQuery
        {
            Request = new DocumentSemanticSearchRequest { Query = "return policy" }
        }, CancellationToken.None);

        result.StatusCode.Should().Be(404);
        fixture.EntryRepository.Verify(repository => repository.FindAllAsync(
            It.IsAny<Expression<Func<KnowledgeEntry, bool>>>(),
            It.IsAny<Func<IQueryable<KnowledgeEntry>, IQueryable<KnowledgeEntry>>?>()), Times.Never);
    }

    [Fact]
    public async Task DocumentSemanticSearch_WhenRerankFails_ReturnsDocumentsNotFound()
    {
        var fixture = new DocumentFixture();
        var entry = fixture.Entry(ObjectId.GenerateNewId(), Guid.NewGuid().ToString());
        fixture.SetupSemanticSearchEntries([entry]);
        fixture.Gemini.Setup(service => service.RerankerAsyncV2(
                It.IsAny<string>(), It.IsAny<IEnumerable<RankRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GeminiResponse<ICollection<RankedRecord>>>.Failure(500, "rerank failed"));

        var result = await fixture.SemanticSearchHandler.Handle(new DocumentSemanticSearchQuery
        {
            Request = new DocumentSemanticSearchRequest { Query = "return policy" }
        }, CancellationToken.None);

        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("No relevant documents found");
    }

    [Fact]
    public async Task DocumentSemanticSearch_WhenRerankSucceeds_ReturnsTopRankedDocuments()
    {
        var fixture = new DocumentFixture();
        var first = fixture.Entry(ObjectId.GenerateNewId(), Guid.NewGuid().ToString());
        first.SectionTitle = "Refunds";
        var second = fixture.Entry(ObjectId.GenerateNewId(), Guid.NewGuid().ToString());
        second.SectionTitle = "Warranty";
        fixture.SetupSemanticSearchEntries([first, second]);
        fixture.Gemini.Setup(service => service.RerankerAsyncV2(
                It.IsAny<string>(), It.IsAny<IEnumerable<RankRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GeminiResponse<ICollection<RankedRecord>>>.Success(new GeminiResponse<ICollection<RankedRecord>>
            {
                Result =
                [
                    new RankedRecord { Id = first.Id.ToString(), Score = 0.8f },
                    new RankedRecord { Id = second.Id.ToString(), Score = 0.95f }
                ]
            }));

        var result = await fixture.SemanticSearchHandler.Handle(new DocumentSemanticSearchQuery
        {
            Request = new DocumentSemanticSearchRequest
            {
                Query = "return policy",
                CandidateLimit = 10,
                TopK = 1,
                DocumentId = first.DocumentId.ToString()
            }
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().ContainSingle();
        result.Data![0].EntryId.Should().Be(second.Id.ToString());
        result.Data[0].Score.Should().Be(0.95f);
        fixture.Qdrant.Verify(service => service.HybridDocumentSearchAsync(
            It.IsAny<float[]>(),
            It.IsAny<float[]>(),
            10,
            It.Is<Filter>(filter => filter.Must.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmbeddingDocument_WhenIdsInvalid_ReturnsBadRequestWithoutLookup()
    {
        var fixture = new DocumentFixture();

        var result = await fixture.EmbeddingHandler.Handle(new EmbeddingDocumentCommand
        {
            BusinessId = "invalid",
            DocumentId = "invalid"
        }, CancellationToken.None);

        result.StatusCode.Should().Be(400);
        fixture.BusinessRepository.Verify(repository => repository.FindAsync(
            It.IsAny<Expression<Func<Business, bool>>>(),
            It.IsAny<Func<IQueryable<Business>, IQueryable<Business>>?>()), Times.Never);
    }

    [Fact]
    public async Task EmbeddingDocument_WhenBusinessMissing_ReturnsNotFound()
    {
        var fixture = new DocumentFixture();
        fixture.BusinessRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Business, bool>>>(),
                It.IsAny<Func<IQueryable<Business>, IQueryable<Business>>?>()))
            .ReturnsAsync((Business?)null);

        var result = await fixture.EmbeddingHandler.Handle(new EmbeddingDocumentCommand
        {
            BusinessId = fixture.Business.Id.ToString(),
            DocumentId = ObjectId.GenerateNewId().ToString()
        }, CancellationToken.None);

        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Business not found.");
    }

    [Fact]
    public async Task EmbeddingDocument_WhenDocumentMissing_ReturnsNotFound()
    {
        var fixture = new DocumentFixture();
        fixture.BusinessRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<Business, bool>>>(),
                It.IsAny<Func<IQueryable<Business>, IQueryable<Business>>?>()))
            .ReturnsAsync(fixture.Business);
        fixture.DocumentRepository.Setup(repository => repository.FindAsync(
                It.IsAny<Expression<Func<KnowledgeDocument, bool>>>(),
                It.IsAny<Func<IQueryable<KnowledgeDocument>, IQueryable<KnowledgeDocument>>?>()))
            .ReturnsAsync((KnowledgeDocument?)null);

        var result = await fixture.EmbeddingHandler.Handle(new EmbeddingDocumentCommand
        {
            BusinessId = fixture.Business.Id.ToString(),
            DocumentId = ObjectId.GenerateNewId().ToString()
        }, CancellationToken.None);

        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Uploaded document not found.");
    }

    [Fact]
    public async Task EmbeddingDocument_WhenQuotaMissing_ReturnsNotFound()
    {
        var fixture = new DocumentFixture();
        var document = fixture.Document("catalog.pdf", KnowledgeDocumentStatus.Uploaded, TestData.Now);
        fixture.SetupEmbeddingBusinessAndDocument(document);
        fixture.BusinessQuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(fixture.Business.Id))
            .ReturnsAsync((BusinessQuota?)null);

        var result = await fixture.EmbeddingHandler.Handle(new EmbeddingDocumentCommand
        {
            BusinessId = fixture.Business.Id.ToString(),
            DocumentId = document.Id.ToString()
        }, CancellationToken.None);

        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Business quota not found");
    }

    [Fact]
    public async Task EmbeddingDocument_WhenQuotaInsufficient_MarksDocumentFailedAndSaves()
    {
        var fixture = new DocumentFixture();
        var document = fixture.Document("catalog.pdf", KnowledgeDocumentStatus.Uploaded, TestData.Now);
        fixture.SetupEmbeddingBusinessAndDocument(document);
        fixture.BusinessQuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(fixture.Business.Id))
            .ReturnsAsync(TestData.Quota(fixture.Business, usedTokens: 99_999));

        var result = await fixture.EmbeddingHandler.Handle(new EmbeddingDocumentCommand
        {
            BusinessId = fixture.Business.Id.ToString(),
            DocumentId = document.Id.ToString()
        }, CancellationToken.None);

        result.StatusCode.Should().Be(429);
        document.Status.Should().Be(KnowledgeDocumentStatus.Failed);
        document.ErrorMessage.Should().Be("Not enough token quota to embed document.");
        fixture.DocumentRepository.Verify(repository => repository.UpdateAsync(document), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmbeddingDocument_WhenEmbeddingPipelineSucceeds_StoresEntriesUpsertsPointsAndUpdatesQuota()
    {
        var fixture = new DocumentFixture();
        fixture.EnsureSectionSummaryPrompt();
        var document = fixture.Document("catalog.pdf", KnowledgeDocumentStatus.Uploaded, TestData.Now);
        var quota = TestData.Quota(fixture.Business);
        var entry = fixture.Entry(document.Id, Guid.NewGuid().ToString());
        entry.EmbeddingText = "technical text";
        entry.SectionSummary = "section summary";
        fixture.SetupEmbeddingBusinessAndDocument(document);
        fixture.BusinessQuotaRepository.Setup(repository => repository.GetCurrentBusinessQuota(fixture.Business.Id))
            .ReturnsAsync(quota);
        fixture.Cloudinary.Setup(service => service.DownloadFileAsync(document.FileUrl))
            .ReturnsAsync(Result<Stream>.Success(new MemoryStream([1, 2, 3])));
        fixture.ExtractFile.Setup(service => service.ExtractMarkdownAsync(It.IsAny<Stream>(), document.Type))
            .ReturnsAsync("# Refunds\nRefunds are allowed.");
        fixture.Chunk.Setup(service => service.SplitMarkdownByHeadingAsync(It.IsAny<string>()))
            .ReturnsAsync(
            [
                new DocumentSection
                {
                    HeadingPath = "Refunds",
                    MarkdownContent = "Refunds are allowed."
                }
            ]);
        fixture.Chunk.Setup(service => service.ChunkSectionsAsync(
                It.IsAny<IReadOnlyList<DocumentSection>>(),
                document.FileName,
                fixture.Business.Id,
                document.Id,
                It.IsAny<int>()))
            .ReturnsAsync([entry]);
        fixture.Gemini.Setup(service => service.GenerateTextAsyncV2(
                It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GeminiResponse<string>>.Success(new GeminiResponse<string>
            {
                Result = "Refund policy summary",
                InputTokens = 3,
                OutputTokens = 2
            }));
        fixture.Gemini.Setup(service => service.EmbeddingsAsyncV2(
                It.IsAny<string>(), "RETRIEVAL_DOCUMENT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GeminiResponse<double[]>>.Success(new GeminiResponse<double[]>
            {
                Result = [0.1, 0.2, 0.3],
                InputTokens = 4,
                OutputTokens = 1
            }));

        var result = await fixture.EmbeddingHandler.Handle(new EmbeddingDocumentCommand
        {
            BusinessId = fixture.Business.Id.ToString(),
            DocumentId = document.Id.ToString()
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        document.Status.Should().Be(KnowledgeDocumentStatus.Embedded);
        document.ChunkCount.Should().Be(1);
        quota.UsedTokens.Should().BeGreaterThan(0);
        fixture.EntryRepository.Verify(repository => repository.AddRangeAsync(
            It.Is<IEnumerable<KnowledgeEntry>>(entries => entries.Single().Id == entry.Id)), Times.Once);
        fixture.Qdrant.Verify(service => service.UpsertAsync(
            QdrantCollections.Documents,
            It.Is<IReadOnlyList<PointStruct>>(points => points.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.BusinessQuotaRepository.Verify(repository => repository.UpdateAsync(quota), Times.Once);
        fixture.UsageQuotaLogRepository.Verify(repository => repository.AddAsync(
            It.Is<UsageQuotaLog>(log => log.BusinessId == fixture.Business.Id && log.SourceId == document.Id)), Times.Once);
    }

    private static IFormFile FormFile(string fileName, string contentType, long length = 100)
    {
        var stream = new MemoryStream(new byte[length]);
        return new FormFile(stream, 0, length, "files", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private sealed class DocumentFixture
    {
        public Business Business { get; } = TestData.Business();
        public Mock<ICloudinaryService> Cloudinary { get; } = new();
        public Mock<IKnowledgeDocumentRepository> DocumentRepository { get; } = new();
        public Mock<IKnowledgeEntryRepository> EntryRepository { get; } = new();
        public Mock<IBusinessRepository> BusinessRepository { get; } = new();
        public Mock<IBusinessQuotaRepository> BusinessQuotaRepository { get; } = new();
        public Mock<IUsageQuotaLogRepository> UsageQuotaLogRepository { get; } = new();
        public Mock<IExtractFileService> ExtractFile { get; } = new();
        public Mock<IChunkService> Chunk { get; } = new();
        public Mock<IGeminiService> Gemini { get; } = new();
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IPublishEndpoint> Publisher { get; } = new();
        public Mock<IQdrantService> Qdrant { get; } = new();
        public UploadDocCommandHandler UploadHandler { get; }
        public GetDocumentQueryHandler GetHandler { get; }
        public DeleteDocumentCommandHandler DeleteHandler { get; }
        public DocumentSemanticSearchQueryHandler SemanticSearchHandler { get; }
        public EmbeddingDocumentCommandHandler EmbeddingHandler { get; }
        public Mock<IActivityLogService> ActivityLogService { get; } = new();

        public DocumentFixture()
        {
            CurrentUser.Setup(service => service.GetBusiness()).ReturnsAsync(Result<Business>.Success(Business));
            UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Qdrant.Setup(service => service.DeletePointsAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            EntryRepository.Setup(repository => repository.DeleteAsync(It.IsAny<object>())).Returns(Task.CompletedTask);
            EntryRepository.Setup(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<KnowledgeEntry>>())).Returns(Task.CompletedTask);
            DocumentRepository.Setup(repository => repository.UpdateAsync(It.IsAny<KnowledgeDocument>())).Returns(Task.CompletedTask);
            BusinessQuotaRepository.Setup(repository => repository.UpdateAsync(It.IsAny<BusinessQuota>())).Returns(Task.CompletedTask);
            UsageQuotaLogRepository.Setup(repository => repository.AddAsync(It.IsAny<UsageQuotaLog>())).Returns(Task.CompletedTask);
            Qdrant.Setup(service => service.UpsertAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<PointStruct>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            UploadHandler = new UploadDocCommandHandler(
                Cloudinary.Object,
                DocumentRepository.Object,
                CurrentUser.Object,
                UnitOfWork.Object,
                Publisher.Object,
                ActivityLogService.Object);
            GetHandler = new GetDocumentQueryHandler(
                UnitOfWork.Object,
                TestData.Mapper(),
                Mock.Of<ILogger<GetDocumentQueryHandler>>(),
                CurrentUser.Object,
                DocumentRepository.Object,
                EntryRepository.Object);
            DeleteHandler = new DeleteDocumentCommandHandler(
                Mock.Of<ILogger<DeleteDocumentCommandHandler>>(),
                DocumentRepository.Object,
                UnitOfWork.Object,
                EntryRepository.Object,
                Qdrant.Object,
                CurrentUser.Object,
                ActivityLogService.Object);
            SemanticSearchHandler = new DocumentSemanticSearchQueryHandler(
                CurrentUser.Object,
                Gemini.Object,
                Qdrant.Object,
                DocumentRepository.Object,
                EntryRepository.Object,
                Mock.Of<ILogger<DocumentSemanticSearchQueryHandler>>());
            EmbeddingHandler = new EmbeddingDocumentCommandHandler(
                BusinessRepository.Object,
                DocumentRepository.Object,
                EntryRepository.Object,
                UnitOfWork.Object,
                Mock.Of<ILogger<EmbeddingDocumentCommandHandler>>(),
                Cloudinary.Object,
                ExtractFile.Object,
                Chunk.Object,
                Gemini.Object,
                Qdrant.Object,
                BusinessQuotaRepository.Object,
                UsageQuotaLogRepository.Object);
        }

        public void SetupDocuments(IReadOnlyCollection<KnowledgeDocument> documents)
        {
            DocumentRepository.Setup(repository => repository.AsQueryable()).Returns(documents.AsQueryable());
            DocumentRepository.Setup(repository => repository.PaginatedListAsync(
                    It.IsAny<IQueryable<KnowledgeDocument>>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((IQueryable<KnowledgeDocument> query, int pageIndex, int pageSize) =>
                    new BasePaginatedList<KnowledgeDocument>(
                        query.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList(),
                        query.Count(),
                        pageIndex,
                        pageSize));
        }

        public KnowledgeDocument Document(
            string fileName,
            KnowledgeDocumentStatus status,
            DateTimeOffset createdAt,
            ObjectId? businessId = null) => new()
            {
                Id = ObjectId.GenerateNewId(),
                BusinessId = businessId ?? Business.Id,
                Title = Path.GetFileNameWithoutExtension(fileName),
                FileName = fileName,
                PublicId = $"knowledge-documents/{fileName}",
                FileUrl = $"https://cdn.example/{fileName}",
                ContentType = "application/pdf",
                SizeInBytes = 1024,
                Type = Path.GetExtension(fileName).TrimStart('.'),
                Status = status,
                ChunkCount = 3,
                CreatedAt = createdAt
            };

        public KnowledgeEntry Entry(ObjectId documentId, string qdrantPointId) => new()
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = Business.Id,
            DocumentId = documentId,
            QdrantPointId = qdrantPointId,
            ChunkIndex = 1,
            Content = "Warranty policy content",
            ContextualContent = "Warranty policy content",
            EmbeddingText = "Warranty policy content",
            FileName = "catalog.pdf",
            CreatedAt = TestData.Now.UtcDateTime
        };

        public void SetupSemanticEmbedding()
        {
            Gemini.Setup(service => service.EmbeddingsAsyncV2(
                    It.IsAny<string>(), "RETRIEVAL_QUERY", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<GeminiResponse<double[]>>.Success(new GeminiResponse<double[]>
                {
                    Result = [0.1, 0.2, 0.3]
                }));
        }

        public void SetupSemanticSearchEntries(IReadOnlyCollection<KnowledgeEntry> entries)
        {
            SetupSemanticEmbedding();
            Qdrant.Setup(service => service.HybridDocumentSearchAsync(
                    It.IsAny<float[]>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Filter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(entries.Select(entry => new ScoredPoint
                {
                    Payload = { ["mongo_id"] = new Value { StringValue = entry.Id.ToString() } }
                }).ToList());
            EntryRepository.Setup(repository => repository.FindAllAsync(
                    It.IsAny<Expression<Func<KnowledgeEntry, bool>>>(),
                    It.IsAny<Func<IQueryable<KnowledgeEntry>, IQueryable<KnowledgeEntry>>?>()))
                .ReturnsAsync(entries.ToList());
        }

        public void SetupEmbeddingBusinessAndDocument(KnowledgeDocument document)
        {
            BusinessRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<Business, bool>>>(),
                    It.IsAny<Func<IQueryable<Business>, IQueryable<Business>>?>()))
                .ReturnsAsync(Business);
            DocumentRepository.Setup(repository => repository.FindAsync(
                    It.IsAny<Expression<Func<KnowledgeDocument, bool>>>(),
                    It.IsAny<Func<IQueryable<KnowledgeDocument>, IQueryable<KnowledgeDocument>>?>()))
                .ReturnsAsync(document);
        }

        public void EnsureSectionSummaryPrompt()
        {
            Directory.CreateDirectory("prompts");
            File.WriteAllText(Path.Combine("prompts", "SectionSummary.md"), "Summarize the section.");
        }
    }
}
