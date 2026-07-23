using LiveSync.Execution.Contracts;
using Microsoft.Extensions.Options;

namespace LiveSync.Sandbox.Services;

public sealed class SandboxExecutionService : ISandboxExecutionService
{
    private readonly IExecutionCatalog _catalog;
    private readonly SandboxOptions _options;

    public SandboxExecutionService(IExecutionCatalog catalog, IOptions<SandboxOptions> options)
    {
        _catalog = catalog;
        _options = options.Value;
    }

    public IReadOnlyList<ExecutionLanguageDescriptor> GetLanguages() => _catalog.GetLanguages();

    public async Task<SandboxExecutionResponse> ExecuteAsync(SandboxExecutionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Language))
        {
            return CreateRejectedResponse(request, "Language is required.");
        }

        var normalizedLanguage = NormalizeLanguage(request.Language);

        if (!_catalog.TryGetExecutor(normalizedLanguage, out var executor))
        {
            return CreateRejectedResponse(request, $"Language '{request.Language}' is not supported.");
        }

        var normalizedRequest = new SandboxExecutionRequest
        {
            Language = normalizedLanguage,
            Code = request.Code,
            StandardInput = request.StandardInput,
            TimeoutMs = request.TimeoutMs > 0 ? request.TimeoutMs : _options.TimeoutMs
        };

        return await executor.ExecuteAsync(normalizedRequest, cancellationToken);
    }

    private static SandboxExecutionResponse CreateRejectedResponse(SandboxExecutionRequest request, string message)
    {
        var timestamp = DateTime.UtcNow;
        return new SandboxExecutionResponse
        {
            Language = request.Language,
            Status = "Rejected",
            IsSuccess = false,
            Message = message,
            RequestedAt = timestamp,
            CompletedAt = timestamp
        };
    }

    private static string NormalizeLanguage(string language)
    {
        var normalized = language.Trim().ToLowerInvariant();
        return normalized == "cs" ? "csharp" : normalized;
    }
}
