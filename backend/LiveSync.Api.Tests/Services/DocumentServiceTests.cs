using LiveSync.Api.Data;
using LiveSync.Api.DTOs;
using LiveSync.Api.Models;
using LiveSync.Api.Services;
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

    private static DocumentService CreateService(ApplicationDbContext context)
    {
        return new DocumentService(context, NullLogger<DocumentService>.Instance);
    }
}
