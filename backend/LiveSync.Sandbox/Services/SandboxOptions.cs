namespace LiveSync.Sandbox.Services;

public sealed class SandboxOptions
{
    public const string SectionName = "Sandbox";

    public int TimeoutMs { get; set; } = 10000;
    public string WorkingDirectoryRoot { get; set; } = string.Empty;
}
