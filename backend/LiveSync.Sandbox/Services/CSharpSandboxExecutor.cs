using System.Diagnostics;
using System.Text;
using LiveSync.Execution.Contracts;
using Microsoft.Extensions.Options;

namespace LiveSync.Sandbox.Services;

public sealed class CSharpSandboxExecutor : ISandboxExecutor
{
    private readonly SandboxOptions _options;
    private readonly ILogger<CSharpSandboxExecutor> _logger;

    public CSharpSandboxExecutor(IOptions<SandboxOptions> options, ILogger<CSharpSandboxExecutor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string Language => "csharp";

    public ExecutionLanguageDescriptor DescribeLanguage() => new()
    {
        Name = "csharp",
        DisplayName = "C#"
    };

    public async Task<SandboxExecutionResponse> ExecuteAsync(SandboxExecutionRequest request, CancellationToken cancellationToken)
    {
        var requestedAt = DateTime.UtcNow;
        var workspaceRoot = ResolveWorkspaceRoot();
        var workspacePath = Path.Combine(workspaceRoot, Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(workspacePath);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(workspacePath, "LiveSyncSandbox.csproj"), CreateProjectFile(), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(workspacePath, "Program.cs"), request.Code, cancellationToken);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run --project LiveSyncSandbox.csproj --configuration Release",
                    WorkingDirectory = workspacePath,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            if (!string.IsNullOrEmpty(request.StandardInput))
            {
                await process.StandardInput.WriteAsync(request.StandardInput.AsMemory(), cancellationToken);
            }

            process.StandardInput.Close();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(request.TimeoutMs);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKillProcess(process);
                var timedOutOutput = await AwaitReaderAsync(outputTask);
                var timedOutError = await AwaitReaderAsync(errorTask);

                return new SandboxExecutionResponse
                {
                    Language = Language,
                    Status = "TimedOut",
                    IsSuccess = false,
                    Message = $"Execution exceeded the {request.TimeoutMs}ms limit.",
                    StandardOutput = timedOutOutput,
                    StandardError = timedOutError,
                    ExitCode = null,
                    RequestedAt = requestedAt,
                    CompletedAt = DateTime.UtcNow
                };
            }

            var standardOutput = await outputTask;
            var standardError = await errorTask;
            var completedAt = DateTime.UtcNow;
            var isSuccess = process.ExitCode == 0;

            return new SandboxExecutionResponse
            {
                Language = Language,
                Status = isSuccess ? "Succeeded" : "Failed",
                IsSuccess = isSuccess,
                Message = isSuccess ? "Execution completed successfully." : $"Execution failed with exit code {process.ExitCode}.",
                StandardOutput = standardOutput,
                StandardError = standardError,
                ExitCode = process.ExitCode,
                RequestedAt = requestedAt,
                CompletedAt = completedAt
            };
        }
        catch (OperationCanceledException)
        {
            return new SandboxExecutionResponse
            {
                Language = Language,
                Status = "Canceled",
                IsSuccess = false,
                Message = "Execution was canceled.",
                RequestedAt = requestedAt,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing C# sandbox request");
            return new SandboxExecutionResponse
            {
                Language = Language,
                Status = "Failed",
                IsSuccess = false,
                Message = "Sandbox execution failed unexpectedly.",
                StandardError = ex.Message,
                RequestedAt = requestedAt,
                CompletedAt = DateTime.UtcNow
            };
        }
        finally
        {
            TryDeleteWorkspace(workspacePath);
        }
    }

    private string ResolveWorkspaceRoot()
    {
        if (!string.IsNullOrWhiteSpace(_options.WorkingDirectoryRoot))
        {
            Directory.CreateDirectory(_options.WorkingDirectoryRoot);
            return _options.WorkingDirectoryRoot;
        }

        var path = Path.Combine(Path.GetTempPath(), "LiveSync", "SandboxRuns");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateProjectFile()
    {
        var builder = new StringBuilder();
        builder.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        builder.AppendLine("  <PropertyGroup>");
        builder.AppendLine("    <OutputType>Exe</OutputType>");
        builder.AppendLine("    <TargetFramework>net10.0</TargetFramework>");
        builder.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        builder.AppendLine("    <Nullable>enable</Nullable>");
        builder.AppendLine("  </PropertyGroup>");
        builder.AppendLine("</Project>");
        return builder.ToString();
    }

    private static async Task<string> AwaitReaderAsync(Task<string> readerTask)
    {
        try
        {
            return await readerTask;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private void TryDeleteWorkspace(string workspacePath)
    {
        try
        {
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete sandbox workspace {WorkspacePath}", workspacePath);
        }
    }
}
