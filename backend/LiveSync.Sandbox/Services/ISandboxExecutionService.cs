using LiveSync.Execution.Contracts;

namespace LiveSync.Sandbox.Services;

public interface ISandboxExecutionService
{
    IReadOnlyList<ExecutionLanguageDescriptor> GetLanguages();
    Task<SandboxExecutionResponse> ExecuteAsync(SandboxExecutionRequest request, CancellationToken cancellationToken);
}
