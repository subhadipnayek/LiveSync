using LiveSync.Api.Data;
using LiveSync.Api.DTOs;
using LiveSync.Api.Models;
using LiveSync.Api.Services;
using LiveSync.Execution.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LiveSync.Api.Tests.Services;

public class DocumentServiceTests
{
    [Fact]
    public async Task GetAccessLevelAsync_ReturnsEdit_ForOwner()
    {
        await using var context = CreateContext();
        context.Documents.Add(new Document
        {
            Id = "doc-1",
            Title = "Doc",
            Content = "Content",
            OwnerId = "owner-1"
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var access = await service.GetAccessLevelAsync("doc-1", "owner-1");

        Assert.Equal("Edit", access);
    }

    [Fact]
    public async Task GetAccessLevelAsync_ReturnsSharedAccessLevel_ForSharedUser()
    {
        await using var context = CreateContext();
        context.Documents.Add(new Document
        {
            Id = "doc-2",
            Title = "Doc",
            Content = "Content",
            OwnerId = "owner-1"
        });
        context.SharedDocuments.Add(new SharedDocument
        {
            Id = "share-1",
            DocumentId = "doc-2",
            UserId = "user-2",
            AccessLevel = "View"
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var access = await service.GetAccessLevelAsync("doc-2", "user-2");

        Assert.Equal("View", access);
    }

    [Fact]
    public async Task UpdateDocumentAsync_ReturnsNull_WhenUserHasNoEditAccess()
    {
        await using var context = CreateContext();
        context.Documents.Add(new Document
        {
            Id = "doc-3",
            Title = "Doc",
            Content = "Content",
            OwnerId = "owner-1"
        });
        context.SharedDocuments.Add(new SharedDocument
        {
            Id = "share-2",
            DocumentId = "doc-3",
            UserId = "user-2",
            AccessLevel = "View"
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.UpdateDocumentAsync("doc-3", "user-2", new UpdateDocumentRequest
        {
            Title = "New title"
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task GetExecutionLanguagesAsync_ReturnsSandboxLanguages()
    {
        await using var context = CreateContext();
        var sandboxClient = new FakeSandboxExecutionClient
        {
            Languages = [new ExecutionLanguageDescriptor { Name = "csharp", DisplayName = "C#" }]
        };
        var service = CreateService(context, sandboxClient);

        var languages = await service.GetExecutionLanguagesAsync();

        Assert.Single(languages);
        Assert.Equal("csharp", languages[0].Name);
        Assert.Equal("C#", languages[0].DisplayName);
    }

    [Fact]
    public async Task ExecuteDocumentAsync_ReturnsSandboxResponse_ForOwner()
    {
        await using var context = CreateContext();
        context.Documents.Add(new Document
        {
            Id = "doc-4",
            Title = "Program.cs",
            Content = "Console.WriteLine(\"Hello\");",
            OwnerId = "owner-1"
        });
        await context.SaveChangesAsync();

        var sandboxClient = new FakeSandboxExecutionClient
        {
            Response = new SandboxExecutionResponse
            {
                Language = "csharp",
                Status = "Succeeded",
                IsSuccess = true,
                Message = "Execution completed successfully.",
                StandardOutput = "Hello\r\n",
                RequestedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            }
        };

        var service = CreateService(context, sandboxClient);

        var result = await service.ExecuteDocumentAsync("doc-4", "owner-1", new ExecuteDocumentRequest
        {
            Language = "CS",
            StandardInput = "sample input"
        });

        Assert.NotNull(result);
        Assert.Equal("doc-4", result.DocumentId);
        Assert.Equal("csharp", result.Language);
        Assert.Equal("Succeeded", result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello\r\n", result.StandardOutput);
        Assert.NotNull(sandboxClient.LastRequest);
        Assert.Equal("csharp", sandboxClient.LastRequest!.Language);
        Assert.Equal("Console.WriteLine(\"Hello\");", sandboxClient.LastRequest.Code);
        Assert.Equal("sample input", sandboxClient.LastRequest.StandardInput);
    }

    [Fact]
    public async Task ExecuteDocumentAsync_ReturnsNull_WhenUserHasNoEditAccess()
    {
        await using var context = CreateContext();
        context.Documents.Add(new Document
        {
            Id = "doc-5",
            Title = "Program.cs",
            Content = "Console.WriteLine(\"Hello\");",
            OwnerId = "owner-1"
        });
        context.SharedDocuments.Add(new SharedDocument
        {
            Id = "share-3",
            DocumentId = "doc-5",
            UserId = "user-2",
            AccessLevel = "View"
        });
        await context.SaveChangesAsync();

        var sandboxClient = new FakeSandboxExecutionClient();
        var service = CreateService(context, sandboxClient);

        var result = await service.ExecuteDocumentAsync("doc-5", "user-2", new ExecuteDocumentRequest
        {
            Language = "csharp"
        });

        Assert.Null(result);
        Assert.Null(sandboxClient.LastRequest);
    }

    [Fact]
    public void GenerateShareCode_ReturnsExpectedFormat()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        var code = service.GenerateShareCode();

        Assert.Equal(10, code.Length);
        Assert.Matches("^[A-Z0-9]{10}$", code);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static DocumentService CreateService(ApplicationDbContext context, FakeSandboxExecutionClient? sandboxClient = null)
    {
        return new DocumentService(context, sandboxClient ?? new FakeSandboxExecutionClient(), NullLogger<DocumentService>.Instance);
    }

    private sealed class FakeSandboxExecutionClient : ISandboxExecutionClient
    {
        public IReadOnlyList<ExecutionLanguageDescriptor> Languages { get; set; } = [];

        public SandboxExecutionResponse Response { get; set; } = new()
        {
            Language = "csharp",
            Status = "Succeeded",
            IsSuccess = true,
            Message = "Execution completed successfully.",
            RequestedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        public SandboxExecutionRequest? LastRequest { get; private set; }

        public Task<IReadOnlyList<ExecutionLanguageDescriptor>> GetLanguagesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Languages);
        }

        public Task<SandboxExecutionResponse> ExecuteAsync(SandboxExecutionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }
    }
}
