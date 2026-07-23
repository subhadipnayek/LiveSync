using LiveSync.Execution.Contracts;

namespace LiveSync.Sandbox.Services;

public interface ISandboxExecutor
{
    string Language { get; }
    ExecutionLanguageDescriptor DescribeLanguage();
    Task<SandboxExecutionResponse> ExecuteAsync(SandboxExecutionRequest request, CancellationToken cancellationToken);
}
