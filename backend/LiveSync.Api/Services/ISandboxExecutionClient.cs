using LiveSync.Execution.Contracts;

namespace LiveSync.Api.Services;

public interface ISandboxExecutionClient
{
    Task<IReadOnlyList<ExecutionLanguageDescriptor>> GetLanguagesAsync(CancellationToken cancellationToken = default);
    Task<SandboxExecutionResponse> ExecuteAsync(SandboxExecutionRequest request, CancellationToken cancellationToken = default);
}
