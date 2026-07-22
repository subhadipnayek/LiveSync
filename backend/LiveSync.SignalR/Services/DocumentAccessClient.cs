using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR;

namespace LiveSync.Services;

public sealed class DocumentAccessClient(HttpClient httpClient, ILogger<DocumentAccessClient> logger)
{
    public async Task<string?> GetAccessLevelAsync(
        string documentId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/documents/{Uri.EscapeDataString(documentId)}/access");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return null;

            response.EnsureSuccessStatusCode();
            var access = await response.Content.ReadFromJsonAsync<DocumentAccessResponse>(
                cancellationToken: cancellationToken);
            return access?.AccessLevel;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Timed out validating access to document {DocumentId}", documentId);
            throw new HubException("Document access validation timed out.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Could not validate access to document {DocumentId}", documentId);
            throw new HubException("Document access validation is temporarily unavailable.");
        }
    }

    private sealed record DocumentAccessResponse(string AccessLevel);
}
