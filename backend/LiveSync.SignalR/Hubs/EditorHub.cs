using LiveSync.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LiveSync.Hubs
{
    [Authorize]
    public class EditorHub : Hub
    {
        private readonly ILogger<EditorHub> _logger;
        private readonly DocumentAccessClient _documentAccessClient;
        private readonly IDocumentStateService _state;

        private static readonly string[] CursorColors =
            ["#FF6B6B", "#4ECDC4", "#45B7D1", "#FFA07A", "#98D8C8", "#F7DC6F"];

        public EditorHub(
            ILogger<EditorHub> logger,
            DocumentAccessClient documentAccessClient,
            IDocumentStateService state)
        {
            _logger = logger;
            _documentAccessClient = documentAccessClient;
            _state = state;
        }

        public async Task JoinDocument(string documentId)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new HubException("A document id is required.");

            var accessLevel = await _documentAccessClient.GetAccessLevelAsync(
                documentId,
                GetAccessToken(),
                Context.ConnectionAborted);

            if (accessLevel is null)
                throw new HubException("You do not have access to this document.");

            var wasAdded = await _state.AddUserToDocumentAsync(documentId, Context.ConnectionId, accessLevel);
            if (!wasAdded)
            {
                _logger.LogInformation("Connection {ConnectionId} already in document {DocumentId}",
                    Context.ConnectionId, documentId);
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, documentId);

            var activeCount = await _state.GetUserCountAsync(documentId);

            _logger.LogInformation(
                "User {ConnectionId} joined document {DocumentId}. Active users: {ActiveCount}",
                Context.ConnectionId, documentId, activeCount);

            // Send current content to the joining user before broadcasting the join event
            var currentContent = await _state.GetContentAsync(documentId);
            if (currentContent is not null)
                await Clients.Caller.SendAsync("ReceiveContentUpdate", currentContent);

            await Clients.Group(documentId).SendAsync("UserJoined", Context.ConnectionId, activeCount);
        }

        public async Task LeaveDocument(string documentId)
        {
            var wasRemoved = await _state.RemoveUserFromDocumentAsync(documentId, Context.ConnectionId);
            if (!wasRemoved)
                return;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, documentId);

            var activeCount = await _state.GetUserCountAsync(documentId);

            _logger.LogInformation(
                "User {ConnectionId} left document {DocumentId}. Active users: {ActiveCount}",
                Context.ConnectionId, documentId, activeCount);

            if (activeCount == 0)
                await _state.DeleteContentAsync(documentId);

            await Clients.Group(documentId).SendAsync("UserLeft", Context.ConnectionId, activeCount);
        }

        public async Task SendContentUpdate(string documentId, string content)
        {
            var accessLevel = await _state.GetAccessAsync(Context.ConnectionId, documentId);
            if (accessLevel != "Edit")
                throw new HubException("You do not have edit access to this document.");

            await _state.SetContentAsync(documentId, content);

            await Clients.OthersInGroup(documentId).SendAsync("ReceiveContentUpdate", content);
        }

        public async Task SendCursorPosition(string documentId, int position)
        {
            var accessLevel = await _state.GetAccessAsync(Context.ConnectionId, documentId);
            if (accessLevel is null)
                throw new HubException("Join the document before sending cursor updates.");

            var color = await _state.GetColorAsync(Context.ConnectionId) ?? "#2196F3";
            await Clients.OthersInGroup(documentId)
                .SendAsync("ReceiveCursorUpdate", Context.ConnectionId, position, color);
        }

        public override async Task OnConnectedAsync()
        {
            var color = CursorColors[Random.Shared.Next(CursorColors.Length)];
            await _state.SetColorAsync(Context.ConnectionId, color);
            _logger.LogInformation("Connection established: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Connection disconnected: {ConnectionId}", Context.ConnectionId);

            var docs = await _state.GetDocumentsForConnectionAsync(Context.ConnectionId);

            var notificationTasks = docs.Select(async kvp =>
            {
                var (documentId, _) = (kvp.Key, kvp.Value);
                await _state.RemoveUserFromDocumentAsync(documentId, Context.ConnectionId);
                var count = await _state.GetUserCountAsync(documentId);

                _logger.LogInformation(
                    "Auto-removed {ConnectionId} from document {DocumentId}. Remaining: {Count}",
                    Context.ConnectionId, documentId, count);

                if (count == 0)
                    await _state.DeleteContentAsync(documentId);

                await Clients.Group(documentId).SendAsync("UserLeft", Context.ConnectionId, count);
            });

            await Task.WhenAll(notificationTasks);
            await _state.RemoveConnectionAsync(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        private string GetAccessToken()
        {
            var request = Context.GetHttpContext()?.Request;
            var queryToken = request?.Query["access_token"].ToString();
            if (!string.IsNullOrWhiteSpace(queryToken))
                return queryToken;

            var authorization = request?.Headers.Authorization.ToString();
            return authorization?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                ? authorization["Bearer ".Length..].Trim()
                : string.Empty;
        }
    }
}
