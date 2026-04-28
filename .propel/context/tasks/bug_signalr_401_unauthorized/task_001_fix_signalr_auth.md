# Bug Fix Task - bug_signalr_401_unauthorized

## Bug Report Reference
- Bug ID: bug_signalr_401_unauthorized
- Source: Browser console error at `http://localhost:4200/queue`

## Bug Summary

### Issue Classification
- **Priority**: High
- **Severity**: SignalR real-time feature completely non-functional; polling still works as fallback
- **Affected Version**: EP-003 post-implementation (local dev)
- **Environment**: Windows, Chrome, Angular dev server `http://localhost:4200`, IIS Express `https://localhost:44397`

### Steps to Reproduce
1. Log in as admin (`admin@propeliq.local`)
2. Navigate to `http://localhost:4200/queue`
3. Open browser DevTools → Console
4. **Expected**: SignalR connects to `/hubs/session`, no console errors; `HighRiskAlert` push events trigger immediate re-poll
5. **Actual**:
   ```
   WebSocket connection to 'ws://localhost:4200/hubs/session?id=...&access_token=...' failed
   Error: Failed to start the transport 'WebSockets': WebSocket failed to connect.
   GET http://localhost:4200/hubs/session?id=...&access_token=... 401 (Unauthorized)
   Error: Failed to start the transport 'ServerSentEvents': EventSource failed to connect.
   ```

**Error Output**:
```text
chunk-J25FJFZE.js:42 WebSocket connection to 'ws://localhost:4200/hubs/session?id=_FHQ4EDNbDh8cm9UV1CQXw&access_token=eyJ...' failed
chunk-J25FJFZE.js:49 Error: Failed to start the transport 'WebSockets'
session:1 GET http://localhost:4200/hubs/session?id=zIC_DB3BTFkk...&access_token=eyJ... 401 (Unauthorized)
chunk-J25FJFZE.js:49 Error: Failed to start the transport 'ServerSentEvents'
```

### Root Cause Analysis
- **File**: `server/src/PropelIQ.Api/Infrastructure/Auth/AuthenticationSetup.cs`
- **Component**: JWT Bearer authentication middleware
- **Function**: `AddPropelIQAuthentication` → `JwtBearerEvents`
- **Cause**: The `OnMessageReceived` event handler was missing from `JwtBearerEvents`. 

  Browsers **cannot** set an `Authorization` header on WebSocket or Server-Sent Events connections. Instead, SignalR's JS client appends the JWT as a query-string parameter (`?access_token=...`). ASP.NET Core's JWT middleware only reads `Authorization: Bearer <token>` from the HTTP header by default — it never checks the query string. Without `OnMessageReceived` to copy `context.Request.Query["access_token"]` into `context.Token`, all hub connection requests arrive without a token and receive 401.

  The JWT itself was valid (confirmed by the decoded payload showing `role: Admin`). The proxy config (`proxy.conf.json`) correctly forwarded `/hubs` with `ws: true`. The only missing piece was server-side token extraction for SignalR transports.

- **Why not caught earlier**: `OnMessageReceived` is SignalR-specific. The JWT auth setup was written for standard REST API controllers and not extended when `SessionHub` was added.

### Impact Assessment
- **Affected Features**: SignalR `HighRiskAlert` push events on Queue Dashboard (`/queue`); any future hubs added under `/hubs/*`
- **User Impact**: Staff/Admin receive no real-time risk alert pushes. The 15-second polling fallback still works, so risk score data appears but with up to 15s delay after a new high-risk event
- **Data Integrity Risk**: No
- **Security Implications**: None — `OnMessageReceived` only extracts the token; full JWT signature/expiry validation still runs unchanged via `TokenValidationParameters`

## Fix Overview
Added `OnMessageReceived` event handler to `JwtBearerEvents` in `AuthenticationSetup.cs`. The handler copies `access_token` from the query string into `context.Token` only when the request path starts with `/hubs`, following the [Microsoft SignalR authentication docs](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz).

## Fix Dependencies
- IIS Express restart required to pick up the code change

## Impacted Components
### Backend
- `server/src/PropelIQ.Api/Infrastructure/Auth/AuthenticationSetup.cs` — MODIFIED: added `OnMessageReceived` handler

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/src/PropelIQ.Api/Infrastructure/Auth/AuthenticationSetup.cs` | Add `OnMessageReceived` to `JwtBearerEvents` to extract SignalR query-string token |

## Implementation Plan
1. In `AuthenticationSetup.cs`, inside `options.Events = new JwtBearerEvents { ... }`, add before `OnTokenValidated`:
   ```csharp
   OnMessageReceived = context =>
   {
       var accessToken = context.Request.Query["access_token"];
       var path = context.HttpContext.Request.Path;
       if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
       {
           context.Token = accessToken;
       }
       return Task.CompletedTask;
   },
   ```
2. Restart IIS Express
3. Navigate to `http://localhost:4200/queue` — verify no 401 errors in console

## Regression Prevention Strategy
- [ ] Integration test: connect to `/hubs/session` with `?access_token=<valid-jwt>` and assert HTTP 101 (WebSocket upgrade accepted)
- [ ] Integration test: connect without token and assert HTTP 401

## Rollback Procedure
1. Remove the `OnMessageReceived` handler from `AuthenticationSetup.cs`
2. Restart IIS Express
3. Note: rollback restores the 401 error but does not break REST API or polling

## External References
- [ASP.NET Core SignalR authentication](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz#bearer-token-authentication)

## Build Commands
```powershell
# After saving the file, restart IIS Express from Visual Studio (Stop → Start)
# Or use dotnet run:
cd d:\IQ\server
dotnet run --project src/PropelIQ.Api/PropelIQ.Api.csproj --launch-profile https
```

## Implementation Validation Strategy
- [ ] No WebSocket/SSE 401 errors in browser console on `/queue`
- [ ] `[QueueDashboard] SignalR connection failed` warning no longer appears
- [ ] Queue dashboard still shows 8 appointments (polling path unaffected)
- [ ] High-risk alert banner shows 2 entries

## Implementation Checklist
- [x] Root cause identified: missing `OnMessageReceived` in `JwtBearerEvents`
- [x] Fix applied to `AuthenticationSetup.cs`
- [x] No compile errors
- [ ] IIS Express restarted
- [ ] SignalR connection confirmed in DevTools (WS tab shows 101 upgrade)
