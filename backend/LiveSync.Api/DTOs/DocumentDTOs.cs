using System.ComponentModel.DataAnnotations;

namespace LiveSync.Api.DTOs
{
    public class DocumentDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string? OwnerName { get; set; }
        public string? ShareCode { get; set; }
        public string DefaultAccessLevel { get; set; } = "View";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastEditedAt { get; set; }
        public string? LastEditedBy { get; set; }
        public List<SharedDocumentDto> SharedWith { get; set; } = new();
    }

    public class CreateDocumentRequest
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }

    public class UpdateDocumentRequest
    {
        [StringLength(200)]
        public string? Title { get; set; }

        public string? Content { get; set; }

        public string? LastEditedBy { get; set; }
    }

    public class SharedDocumentDto
    {
        public string Id { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public string DocumentTitle { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public DateTime SharedAt { get; set; }
        public string AccessLevel { get; set; } = "View";
    }

    public class AddSharedDocumentRequest
    {
        [Required]
        [StringLength(50)]
        public string ShareCode { get; set; } = string.Empty;
    }

    public class GenerateShareCodeRequest
    {
        [Required]
        [StringLength(50)]
        public string ShareCode { get; set; } = string.Empty;
    }

    public class DocumentContentUpdateRequest
    {
        [Required]
        public string Content { get; set; } = string.Empty;

        public string? LastEditedBy { get; set; }
    }

    public class UpdateAccessLevelRequest
    {
        [Required]
        public string AccessLevel { get; set; } = string.Empty;
    }

    public class ExecuteDocumentRequest
    {
        [Required]
        [RegularExpression("^(csharp|cs)$", ErrorMessage = "Only C# execution is currently supported.")]
        public string Language { get; set; } = "csharp";

        [StringLength(4000)]
        public string? StandardInput { get; set; }
    }

    public class DocumentExecutionResponse
    {
        public string DocumentId { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? StandardOutput { get; set; }
        public string? StandardError { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}
