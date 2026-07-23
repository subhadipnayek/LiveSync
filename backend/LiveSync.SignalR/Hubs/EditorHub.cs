using LiveSync.Models;
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
        private readonly ConflictResolver _conflictResolver;

        private static readonly string[] CursorColors =
            ["#FF6B6B", "#4ECDC4", "#45B7D1", "#FFA07A", "#98D8C8", "#F7DC6F"];

        public EditorHub(
            ILogger<EditorHub> logger,
            DocumentAccessClient documentAccessClient,
            IDocumentStateService state,
            ConflictResolver conflictResolver)
        {
            _logger = logger;
            _documentAccessClient = documentAccessClient;
            _state = state;
            _conflictResolver = conflictResolver;
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
            {
                await _state.DeleteContentAsync(documentId);
                // Also clean up operation log when last user leaves
                var operationLog = _state.GetOperationLog();
                await operationLog.DeleteOperationsAsync(documentId);
            }

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

        /// <summary>
        /// Sends an operation (insert/delete) from a client to be applied on all replicas.
        /// The operation is validated, transformed against concurrent operations, applied to state,
        /// and broadcast to all clients in the document.
        /// </summary>
        public async Task SendOperation(string documentId, Operation operation)
        {
            if (operation == null)
                throw new HubException("Operation cannot be null.");

            if (string.IsNullOrWhiteSpace(documentId))
                throw new HubException("Document ID is required.");

            // Verify edit access
            var accessLevel = await _state.GetAccessAsync(Context.ConnectionId, documentId);
            if (accessLevel != "Edit")
                throw new HubException("You do not have edit access to this document.");

            var operationLog = _state.GetOperationLog();

            try
            {
                // Get current server revision
                var currentRevision = await operationLog.GetCurrentRevisionAsync(documentId);

                // Assign server revision
                var serverOp = operation with { ServerRevision = currentRevision + 1 };

                // Get all operations since client's last known revision
                var concurrentOps = await operationLog.GetOperationsSinceAsync(documentId, operation.ClientRevision);

                // Transform the incoming operation against all concurrent operations
                Operation transformedOp = serverOp;
                foreach (var concurrentOp in concurrentOps)
                {
                    transformedOp = _conflictResolver.TransformAgainstConcurrent(transformedOp, concurrentOp);
                }

                // Store the transformed operation
                await operationLog.AppendOperationAsync(documentId, transformedOp);

                // Apply operation to get updated content
                var currentContent = await _state.GetContentAsync(documentId) ?? "";
                var updatedContent = _conflictResolver.ApplyOperation(currentContent, transformedOp);
                await _state.SetContentAsync(documentId, updatedContent);

                _logger.LogInformation(
                    "Operation applied for document {DocumentId} by {ConnectionId}. Type: {OpType}, ServerRevision: {ServerRevision}",
                    documentId, Context.ConnectionId, operation.GetType().Name, transformedOp.ServerRevision);

                // Broadcast the transformed operation to all clients (including sender)
                await Clients.Group(documentId).SendAsync("ReceiveOperation", transformedOp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing operation for document {DocumentId}", documentId);
                throw new HubException($"Failed to process operation: {ex.Message}");
            }
        }

        /// <summary>
        /// Client requests missed operations (for resync after reconnection).
        /// Returns all operations since the client's last known revision.
        /// </summary>
        public async Task RequestMissedOperations(string documentId, long fromRevision)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new HubException("Document ID is required.");

            var accessLevel = await _state.GetAccessAsync(Context.ConnectionId, documentId);
            if (accessLevel is null)
                throw new HubException("Join the document before requesting missed operations.");

            var operationLog = _state.GetOperationLog();

            try
            {
                var missedOps = await operationLog.GetOperationsSinceAsync(documentId, fromRevision);

                _logger.LogInformation(
                    "Sending {MissedOpCount} missed operations to {ConnectionId} for document {DocumentId} since revision {FromRevision}",
                    missedOps.Count, Context.ConnectionId, documentId, fromRevision);

                // Send all missed operations to the calling client
                foreach (var op in missedOps)
                {
                    await Clients.Caller.SendAsync("ReceiveOperation", op);
                }

                // Signal to client that all missed operations have been sent
                var currentRevision = await operationLog.GetCurrentRevisionAsync(documentId);
                await Clients.Caller.SendAsync("ResyncComplete", currentRevision);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending missed operations for document {DocumentId}", documentId);
                throw new HubException($"Failed to retrieve missed operations: {ex.Message}");
            }
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

            var operationLog = _state.GetOperationLog();

            var notificationTasks = docs.Select(async kvp =>
            {
                var (documentId, _) = (kvp.Key, kvp.Value);
                await _state.RemoveUserFromDocumentAsync(documentId, Context.ConnectionId);
                var count = await _state.GetUserCountAsync(documentId);

                _logger.LogInformation(
                    "Auto-removed {ConnectionId} from document {DocumentId}. Remaining: {Count}",
                    Context.ConnectionId, documentId, count);

                if (count == 0)
                {
                    await _state.DeleteContentAsync(documentId);
                    // Clean up operations when last user leaves
                    await operationLog.DeleteOperationsAsync(documentId);
                }

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
