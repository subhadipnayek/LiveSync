namespace LiveSync.Execution.Contracts;

public sealed class ExecutionLanguageDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class SandboxExecutionRequest
{
    public string Language { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? StandardInput { get; set; }
    public int TimeoutMs { get; set; } = 10000;
}

public sealed class SandboxExecutionResponse
{
    public string Language { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? StandardOutput { get; set; }
    public string? StandardError { get; set; }
    public int? ExitCode { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}
