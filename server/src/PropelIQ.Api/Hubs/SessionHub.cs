using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PropelIQ.Api.Hubs;

/// <summary>
/// SignalR hub for real-time session lifecycle notifications.
/// Requires a valid JWT bearer token (AC-3: push "SessionEnded" to displaced device).
/// Connections are grouped by user ID so targeted messages reach all tabs for that user.
/// </summary>
[Authorize]
public sealed class SessionHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?
            .FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?
            .FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (userId is not null)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");

        await base.OnDisconnectedAsync(exception);
    }
}
