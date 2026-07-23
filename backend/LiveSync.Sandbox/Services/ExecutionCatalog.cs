using LiveSync.Execution.Contracts;

namespace LiveSync.Sandbox.Services;

public sealed class ExecutionCatalog : IExecutionCatalog
{
    private readonly IReadOnlyDictionary<string, ISandboxExecutor> _executors;
    private readonly IReadOnlyList<ExecutionLanguageDescriptor> _languages;

    public ExecutionCatalog(IEnumerable<ISandboxExecutor> executors)
    {
        var executorList = executors.ToList();
        _executors = executorList.ToDictionary(executor => executor.Language, StringComparer.OrdinalIgnoreCase);
        _languages = executorList
            .Select(executor => executor.DescribeLanguage())
            .OrderBy(language => language.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ExecutionLanguageDescriptor> GetLanguages() => _languages;

    public bool TryGetExecutor(string language, out ISandboxExecutor executor)
    {
        return _executors.TryGetValue(language, out executor!);
    }
}
