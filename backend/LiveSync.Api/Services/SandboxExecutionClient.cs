using System.Net.Http.Json;
using LiveSync.Execution.Contracts;

namespace LiveSync.Api.Services;

public sealed class SandboxExecutionClient : ISandboxExecutionClient
{
    private readonly HttpClient _httpClient;

    public SandboxExecutionClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ExecutionLanguageDescriptor>> GetLanguagesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _httpClient.GetFromJsonAsync<List<ExecutionLanguageDescriptor>>("api/execution/languages", cancellationToken);
        return result ?? [];
    }

    public async Task<SandboxExecutionResponse> ExecuteAsync(SandboxExecutionRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/execution/run", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SandboxExecutionResponse>(cancellationToken: cancellationToken);
        return result ?? throw new InvalidOperationException("Sandbox response body was empty.");
    }
}
