using LiveSync.Execution.Contracts;

namespace LiveSync.Sandbox.Services;

public interface IExecutionCatalog
{
    IReadOnlyList<ExecutionLanguageDescriptor> GetLanguages();
    bool TryGetExecutor(string language, out ISandboxExecutor executor);
}
