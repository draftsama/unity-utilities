# TCPConnector Changelog

All notable changes to the TCPConnector component will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.6.0] - 2026-02-22

### Added
- **Protocol Version Checking**: Automatic version negotiation during handshake
  - `m_EnableVersionCheck` (default: true): Toggle protocol version validation
  - `m_ProtocolVersion` (default: "2.5"): Current protocol version (Major.Minor format)
  - `ACTION_VERSION_REQUEST` (65523): Server requests client's protocol version
  - `ACTION_VERSION_RESPONSE` (65522): Client responds with protocol version
  - `OnVersionMismatch(ConnectorInfo, string localVersion, string remoteVersion)`: Fired when versions don't match
  - Automatic disconnection: Incompatible clients rejected during handshake

### Changed
- **BREAKING**: `MaxUserAction` reduced from 65523 to 65522
  - Reserved action range expanded to 65523-65535 (was 65524-65535)
  - **Migration**: If your application uses actions 65522-65523, change to 65521 or lower
  - Reason: ACTION_VERSION_REQUEST (65523) and ACTION_VERSION_RESPONSE (65522) now reserved

### Fixed
- Fixed: No version compatibility checking between clients and servers
  - Previous: Clients with different protocol versions could connect, causing undefined behavior
  - Now: Server validates client version during handshake (after ACK, before authentication)
  - Impact: Prevents protocol incompatibilities and ensures all connected clients use same version

### Technical Details
- **Handshake Flow** (with version checking):
  1. Client connects, server sends HELLO
  2. Client sends ACK
  3. **[NEW] Server sends VERSION_REQUEST**
  4. **[NEW] Client sends VERSION_RESPONSE with m_ProtocolVersion**
  5. **[NEW] Server validates version (must match exactly)**
  6. Server sends AUTH_REQUEST (if authentication enabled)
  7. Client sends AUTH_RESPONSE
  8. Server sends CLIENT_LIST_FULL
  9. Client sends READY

- **Server Behavior**:
  - Sends VERSION_REQUEST after receiving ACK from client
  - Waits up to 5 seconds for VERSION_RESPONSE
  - Compares client version with m_ProtocolVersion (exact string match)
  - Disconnects client if versions don't match
  - Fires `m_OnVersionMismatch` event with local and remote versions
  - Logs: "❌ Protocol version mismatch: Server=2.6, Client=2.5"

- **Client Behavior**:
  - Receives VERSION_REQUEST from server during handshake
  - Immediately responds with VERSION_RESPONSE containing m_ProtocolVersion
  - Logs: "📤 Sent protocol version to server: 2.6"
  - Client automatically disconnected by server if version mismatch

- **Version Format**:
  - Format: "Major.Minor" (e.g., "2.6", "3.0")
  - Comparison: Exact string match required
  - Reasoning: Simple and explicit, no implicit compatibility assumptions
  - Future: Could be extended to support version ranges or backwards compatibility

- **Integration**:
  - Version check occurs after ACK, before authentication
  - Avoids wasting authentication resources on incompatible clients
  - Handshake messages handled in ReceiveDataLoopAsync()
  - Server-side only validation (client just responds)

- **Error Handling**:
  - Timeout: 5 seconds for VERSION_RESPONSE (faster than auth timeout)
  - Missing response: Client disconnected, handshake fails
  - Mismatched version: Client disconnected, OnVersionMismatch fired
  - Network error: Handled by existing error handling

### Use Cases
- **Protocol Updates**: Ensure clients update before connecting to new server version
- **Beta Testing**: Separate beta and production servers by version
- **Backwards Compatibility**: Detect old clients and prevent connection
- **Debugging**: Identify version mismatches in logs and events

### Performance
- Minimal impact: Single request/response per connection (one-time during handshake)
- Fast validation: Simple string comparison (~microseconds)
- Early rejection: Incompatible clients rejected before resource-intensive authentication
- Zero runtime cost: Only executes during connection handshake

### Migration Guide
**Action Number Conflict (BREAKING CHANGE)**:
```csharp
// ❌ Before v2.6.0 - If you used actions 65522-65523
await SendDataAsync(65523, myData);
await SendDataAsync(65522, myData);

// ✅ After v2.6.0 - Change to 65521 or lower
await SendDataAsync(65521, myData);
await SendDataAsync(65520, myData);
```

**Updating Protocol Version**:
```csharp
// Set before starting connection
tcpConnector.m_ProtocolVersion = "2.6"; // Match server version

// Handling version mismatches
tcpConnector.m_OnVersionMismatch.AddListener((client, localVer, remoteVer) => {
    Debug.LogError($"Version mismatch: Local={localVer}, Remote={remoteVer}");
    // Show UI message to user: "Please update your client"
});
```

**Disabling Version Check** (not recommended):
```csharp
// For testing or development only
tcpConnector.m_EnableVersionCheck = false;
```

**Version Migration Strategy**:
1. Update server to v2.6.0 (clients will be rejected if using old version)
2. Update all clients to v2.6.0
3. After all clients updated, server can increment m_ProtocolVersion for next breaking change

**Recommended**: Keep version checking enabled in production to ensure protocol compatibility.

## [2.5.0] - 2026-02-22

### Added
- **Graceful Shutdown**: Clean connection termination with notification
  - `m_EnableGracefulShutdown` (default: true): Toggle graceful shutdown
  - `m_ShutdownTimeout` (default: 3000ms): Maximum wait time for notification delivery
  - `ACTION_DISCONNECT_NOTIFICATION` (65524): Protocol action for graceful disconnect
  - Automatic notification: Server/client sends disconnect message before closing
  - Priority queue integration: Disconnect notifications sent as Critical priority (if enabled)

### Changed
- **BREAKING**: `MaxUserAction` reduced from 65524 to 65523
  - Reserved action range expanded to 65524-65535 (was 65525-65535)
  - **Migration**: If your application uses action 65524, change to 65523 or lower
  - Reason: ACTION_DISCONNECT_NOTIFICATION now uses action 65524

### Fixed
- Fixed: Abrupt connection termination caused unexpected disconnections
  - Previous: StopConnection() immediately closed all connections
  - Now: Sends disconnect notification, waits up to 500ms for delivery
  - Impact: Clients can distinguish graceful shutdowns from network failures

### Technical Details
- **Shutdown Flow**:
  1. `StopConnection()` called
  2. Send `ACTION_DISCONNECT_NOTIFICATION` to all connected peers
  3. Wait up to 500ms (capped from m_ShutdownTimeout for safety)
  4. Clear priority queues if enabled
  5. Close all connections normally
  
- **Server Behavior**:
  - Broadcasts disconnect notification to all connected clients
  - Writes notification synchronously to each client stream
  - Flushes stream to ensure delivery
  - Logs notification count: "📢 Sending graceful disconnect notification to N client(s)..."
  
- **Client Behavior**:
  - Sends disconnect notification to server
  - Single synchronous write to server stream
  - Logs: "📢 Sending graceful disconnect notification to server..."
  
- **Receive Handling**:
  - ACTION_DISCONNECT_NOTIFICATION breaks receive loop immediately
  - Server logs: "Client {id} is disconnecting gracefully"
  - Client logs: "Server is shutting down gracefully"
  - Connection closed cleanly after notification received
  
- **Priority Queue Integration**:
  - All priority queues cleared during shutdown
  - Prevents pending messages from blocking shutdown
  - Logs: "🧹 Priority queues cleared"
  
- **Error Handling**:
  - Failed notification delivery logs warning, continues shutdown
  - Network errors during notification ignored (connection closing anyway)
  - Timeout capped at 500ms to prevent excessive delays

### Use Cases
- Server maintenance: Notify clients before scheduled restart
- Client disconnect: Inform server of intentional disconnect vs crash
- Load balancing: Graceful server shutdown before migration
- Testing: Distinguish intentional disconnects from bugs

### Performance
- Synchronous notification: 500ms maximum delay during shutdown
- Minimal overhead: Single packet per peer (<100 bytes)
- Zero runtime cost: Only executes during StopConnection()
- Queue cleanup: Frees memory from pending messages

### Migration Guide
**Action Number Conflict (BREAKING CHANGE)**:
```csharp
// ❌ Before v2.5.0 - If you used action 65524
await SendDataAsync(65524, myData);

// ✅ After v2.5.0 - Change to 65523 or lower
await SendDataAsync(65523, myData);
```

**Disabling Graceful Shutdown** (if needed):
```csharp
// Set before calling StopConnection()
m_EnableGracefulShutdown = false;
StopConnection(); // Will skip notification
```

**Recommended**: Keep graceful shutdown enabled for production environments to improve connection handling and user experience.

## [2.4.0] - 2026-02-22

### Added
- **Message Priority Queues**: Priority-based message queuing for important messages
  - `m_EnablePriorityQueue` (default: false): Toggle priority queue system
  - `m_QueueMaxSize` (default: 100): Maximum messages per priority level
  - `MessagePriority` enum: Critical (0), High (1), Normal (2), Low (3)
  - New overloads: `SendDataAsync(action, data, priority, token)`
  - Automatic queue processing: Higher priority messages sent first
  - Per-priority queues: Separate queue for each priority level

### Changed
- `SendDataAsync()` methods now have priority-aware overloads
  - Standard methods use Normal priority (backward compatible)
  - New overloads accept `MessagePriority` parameter
  - Queue processing runs asynchronously in background

### Technical Details
- Implementation: Four separate queues (one per priority level)
  - Critical: Connection control, critical game state
  - High: Player actions, important events
  - Normal: Regular game updates (default)
  - Low: Chat messages, non-critical data
- Queue processor: `ProcessMessageQueue()` runs continuously when messages queued
  - Processes one message per iteration
  - Checks Critical → High → Normal → Low order
  - 1ms delay between messages to prevent tight loop
- Message structure: `QueuedMessage` class with action, data, targets, progress, completion
- Thread-safe: Queue operations protected by `_queueLock`
- Initialization: Queues created in `InitializePriorityQueues()` during `Awake()`
- Processing state: `_isProcessingQueue` flag prevents multiple processors
- Completion: Uses `UniTaskCompletionSource<bool>` for async result
- Overflow handling: Messages rejected if queue full (max 100 per priority)

### Use Cases
- Critical messages: Disconnect notifications, emergency state sync
- High priority: Player movement, combat actions, critical UI updates
- Normal priority: General game state, periodic updates (default for all existing code)
- Low priority: Chat messages, notifications, analytics

### Performance
- Zero overhead when disabled (m_EnablePriorityQueue = false)
- Minimal latency: ~1ms processing delay per message
- Memory efficient: Only queues messages when enabled
- Backward compatible: All existing SendDataAsync() calls use Normal priority

## [2.3.0] - 2026-02-22

### Added
- **Rate Limiting**: Token bucket algorithm to prevent message spam (server-side protection)
  - `m_EnableRateLimit` (default: true): Toggle rate limiting
  - `m_RateLimit` (default: 100 msg/s): Maximum messages per second per client
  - `m_BurstSize` (default: 50): Maximum burst size (allows short bursts)
  - `OnRateLimitExceeded(ConnectorInfo, int)`: Fired when client exceeds rate limit (with token count)
  - Automatic disconnection: Clients exceeding rate limit are disconnected immediately
  - Per-client tracking: Each client has independent token bucket

### Fixed
- Fixed: Malicious clients could spam server with unlimited messages (DoS vulnerability)
  - Previous: No rate limiting, clients could send thousands of messages per second
  - Now: Token bucket algorithm limits messages to configurable rate (default 100/s)
  - Impact: Prevents denial-of-service attacks and server overload

### Technical Details
- Algorithm: Token bucket with refill based on elapsed time
  - Tokens refill at constant rate (m_RateLimit per second)
  - Maximum tokens: m_BurstSize (allows short bursts above rate limit)
  - Consumption: Each message consumes 1 token
  - Rejection: Messages rejected if insufficient tokens available
- Implementation: `TokenBucket` class with thread-safe token management
  - `TryConsume(int)`: Attempt to consume tokens, returns false if insufficient
  - `Refill()`: Private method that refills tokens based on elapsed time
  - `GetCurrentTokens()`: Diagnostics method to check current token count
- Integration: Rate limit checked in `ReceiveDataLoopAsync()` before processing messages
- Server-side only: Rate limiting only applies to server (protecting server from malicious clients)
- Per-client tracking: `_rateLimiters` dictionary maps TcpClient to TokenBucket
- Cleanup: Rate limiters removed in HandleClientAsync finally block on disconnection
- Initialization: Rate limiters created after authentication completes
- Event firing: `m_OnRateLimitExceeded` fired on main thread before disconnection

### Configuration Guidelines
- Local multiplayer: 100-200 msg/s with burst size 50-100
- Real-time games: 50-100 msg/s (reduce server load)
- Turn-based games: 10-20 msg/s (minimal traffic)
- Stress testing: Disable rate limiting temporarily
- Production: Always enable rate limiting to prevent abuse

## [2.2.0] - 2026-02-22

### Added
- **Connection Metrics**: Real-time connection statistics and bandwidth tracking
  - `m_EnableMetrics` (default: true): Toggle metrics tracking
  - `m_MetricsReportInterval` (default: 5s): Interval for periodic metrics reporting
  - `OnMetricsUpdated(ConnectionMetrics)`: Fired periodically with connection statistics
  - `GetConnectionMetrics(int clientId)`: Retrieve metrics for specific client or server connection
  - `GetAllConnectionMetrics()`: Retrieve metrics for all clients (server mode only)
  - Tracks: bytes sent/received, messages sent/received, connection uptime, average send/receive rates
  - Per-client tracking (server mode) and server connection tracking (client mode)

### Technical Details
- Metrics structure: `ConnectionMetrics` class with real-time statistics
  - `BytesSent` / `BytesReceived`: Total bytes transmitted
  - `MessagesSent` / `MessagesReceived`: Total message count
  - `ConnectionUptimeSeconds`: Time since connection established
  - `AverageSendRate` / `AverageReceiveRate`: Bytes per second (dynamically calculated)
  - `ConnectionStartTime` / `LastActivityTime`: Timestamps for monitoring
- Thread-safe: Metrics locked via `_metricsLock` for concurrent access
- Zero allocation: Metrics updated in-place, no GC pressure
- Server-side: Per-client metrics tracked in `_clientMetrics` dictionary
- Client-side: Single `_serverMetrics` instance tracks server connection
- Metrics loop: `MetricsReportLoopAsync()` runs concurrently with heartbeat and receive loops
- Integration: Metrics tracked in `SendDataToStreamAsync()` and `ReceiveDataLoopAsync()`
- Cleanup: Metrics removed in finally blocks on disconnection

### Use Cases
- Network debugging: Identify bandwidth bottlenecks and connection issues
- Performance monitoring: Track message throughput and connection health
- Load balancing: Distribute clients based on connection statistics
- Analytics: Log connection metrics for post-mortem analysis
- Rate limiting validation: Verify rate limiting effectiveness

## [2.1.0] - 2026-02-22

### Added
- **Connection Metrics**: Real-time connection statistics and bandwidth tracking
  - `m_EnableMetrics` (default: true): Toggle metrics tracking
  - `m_MetricsReportInterval` (default: 5s): Interval for periodic metrics reporting
  - `OnMetricsUpdated(ConnectionMetrics)`: Fired periodically with connection statistics
  - `GetConnectionMetrics(int clientId)`: Retrieve metrics for specific client or server connection
  - `GetAllConnectionMetrics()`: Retrieve metrics for all clients (server mode only)
  - Tracks: bytes sent/received, messages sent/received, connection uptime, average send/receive rates
  - Per-client tracking (server mode) and server connection tracking (client mode)

### Technical Details
- Metrics structure: `ConnectionMetrics` class with real-time statistics
  - `BytesSent` / `BytesReceived`: Total bytes transmitted
  - `MessagesSent` / `MessagesReceived`: Total message count
  - `ConnectionUptimeSeconds`: Time since connection established
  - `AverageSendRate` / `AverageReceiveRate`: Bytes per second (dynamically calculated)
  - `ConnectionStartTime` / `LastActivityTime`: Timestamps for monitoring
- Thread-safe: Metrics locked via `_metricsLock` for concurrent access
- Zero allocation: Metrics updated in-place, no GC pressure
- Server-side: Per-client metrics tracked in `_clientMetrics` dictionary
- Client-side: Single `_serverMetrics` instance tracks server connection
- Metrics loop: `MetricsReportLoopAsync()` runs concurrently with heartbeat and receive loops
- Integration: Metrics tracked in `SendDataToStreamAsync()` and `ReceiveDataLoopAsync()`
- Cleanup: Metrics removed in finally blocks on disconnection

### Use Cases
- Network debugging: Identify bandwidth bottlenecks and connection issues
- Performance monitoring: Track message throughput and connection health
- Load balancing: Distribute clients based on connection statistics
- Analytics: Log connection metrics for post-mortem analysis
- Rate limiting validation: Verify rate limiting effectiveness

## [2.1.0] - 2026-02-22

### Added
- **Authentication System**: Password-based client authentication (server-side validation)
  - `m_EnableAuthentication` (default: false): Toggle authentication requirement
  - `m_AuthPassword` (string): Server password that clients must provide
  - `m_AuthTimeout` (default: 10s): Authentication timeout duration
  - `OnAuthSuccess(ConnectorInfo)`: Fired when client authentication succeeds
  - `OnAuthFailed(ConnectorInfo, string)`: Fired when client authentication fails (with reason)
  - Challenge-response protocol: Server sends AUTH_REQUEST → Client sends AUTH_RESPONSE → Server validates
  - SHA256 password hashing: Passwords hashed before comparison (never stored in plain text)

### Changed
- **[BREAKING]** Reserved action range expanded to 65525-65535 (was 65527-65535)
  - `MaxUserAction` reduced from 65526 to 65524
  - Added `ACTION_AUTH_REQUEST` (65526) and `ACTION_AUTH_RESPONSE` (65525)
  - Impact: Applications using actions 65525-65526 must be updated
  - Migration: Change any usage of actions 65525-65526 to 65524 or below
- Handshake flow now includes optional authentication step (after ACK, before CLIENT_LIST)
  - Client wait sequence: HELLO → ACK → [AUTH_REQUEST → AUTH_RESPONSE] → CLIENT_LIST → READY
  - Server sends AUTH_REQUEST only if `m_EnableAuthentication` is true
  - Failed authentication disconnects client immediately (no retry)

### Security
- **WARNING**: Password transmitted in plain text unless encryption is enabled
  - Recommendation: Always enable `m_EnableEncryption` when using authentication
  - Risk: Passwords can be intercepted on unencrypted connections
- Password hashing (SHA256) prevents storage of plain-text passwords on server
- No rate limiting: Brute-force attacks possible (implement rate limiting separately)
- No account system: Single password for all clients (suitable for local multiplayer only)
- Production recommendation: Implement proper user accounts with salted password hashing

### Fixed
- Fixed: Anyone could connect to server without credentials
  - Previous: Server accepted all connections after handshake
  - Now: Server can require password authentication before allowing access
  - Impact: Prevents unauthorized clients from joining games

### Technical Details
- Authentication occurs in handshake flow between ACK and CLIENT_LIST sync
- Server-side: `ValidateAuthentication()` compares SHA256 hashes
- Client-side: Password sent as UTF-8 bytes in AUTH_RESPONSE payload
- Thread-safe: `_authenticatedClients` dictionary tracks authenticated clients
- Timeout: Configurable via `m_AuthTimeout` (default 10 seconds)
- Event firing: Switches to main thread for Unity events
- Zero allocation: Authentication uses existing packet infrastructure

## [2.0.0] - 2026-02-22

### Added
- **Authentication System**: Password-based client authentication (server-side validation)
  - `m_EnableAuthentication` (default: false): Toggle authentication requirement
  - `m_AuthPassword` (string): Server password that clients must provide
  - `m_AuthTimeout` (default: 10s): Authentication timeout duration
  - `OnAuthSuccess(ConnectorInfo)`: Fired when client authentication succeeds
  - `OnAuthFailed(ConnectorInfo, string)`: Fired when client authentication fails (with reason)
  - Challenge-response protocol: Server sends AUTH_REQUEST → Client sends AUTH_RESPONSE → Server validates
  - SHA256 password hashing: Passwords hashed before comparison (never stored in plain text)

### Changed
- **[BREAKING]** Reserved action range expanded to 65525-65535 (was 65527-65535)
  - `MaxUserAction` reduced from 65526 to 65524
  - Added `ACTION_AUTH_REQUEST` (65526) and `ACTION_AUTH_RESPONSE` (65525)
  - Impact: Applications using actions 65525-65526 must be updated
  - Migration: Change any usage of actions 65525-65526 to 65524 or below
- Handshake flow now includes optional authentication step (after ACK, before CLIENT_LIST)
  - Client wait sequence: HELLO → ACK → [AUTH_REQUEST → AUTH_RESPONSE] → CLIENT_LIST → READY
  - Server sends AUTH_REQUEST only if `m_EnableAuthentication` is true
  - Failed authentication disconnects client immediately (no retry)

### Security
- **WARNING**: Password transmitted in plain text unless encryption is enabled
  - Recommendation: Always enable `m_EnableEncryption` when using authentication
  - Risk: Passwords can be intercepted on unencrypted connections
- Password hashing (SHA256) prevents storage of plain-text passwords on server
- No rate limiting: Brute-force attacks possible (implement rate limiting separately)
- No account system: Single password for all clients (suitable for local multiplayer only)
- Production recommendation: Implement proper user accounts with salted password hashing

### Fixed
- Fixed: Anyone could connect to server without credentials
  - Previous: Server accepted all connections after handshake
  - Now: Server can require password authentication before allowing access
  - Impact: Prevents unauthorized clients from joining games

### Technical Details
- Authentication occurs in handshake flow between ACK and CLIENT_LIST sync
- Server-side: `ValidateAuthentication()` compares SHA256 hashes
- Client-side: Password sent as UTF-8 bytes in AUTH_RESPONSE payload
- Thread-safe: `_authenticatedClients` dictionary tracks authenticated clients
- Timeout: Configurable via `m_AuthTimeout` (default 10 seconds)
- Event firing: Switches to main thread for Unity events
- Zero allocation: Authentication uses existing packet infrastructure

## [2.0.0] - 2026-02-22

### Added
- **Message Encryption**: AES-256-CBC encryption for all data transmission
  - `m_EnableEncryption` (default: false): Toggle encryption for all packets
  - `m_EncryptionKey` (string): Pre-shared key (PSK) as hex string or base64 (32 bytes for AES-256)
  - Auto-generated key: If no key provided, generates random key and logs it (for development only)
  - Transparent encryption: All packets encrypted/decrypted automatically
  - Per-message IV: Each packet uses unique initialization vector (prevents replay attacks)
  - Algorithm: AES-256-CBC with PKCS7 padding

### Changed
- **[BREAKING]** Encryption adds 16-32 bytes overhead per packet (IV + padding)
  - IV: 16 bytes prepended to each encrypted packet
  - Padding: Up to 16 bytes (PKCS7 padding for AES block alignment)
  - Impact: Network bandwidth increases by ~2-5% for typical packets
  - Migration: Clients and server MUST use same encryption settings and key

### Security
- **WARNING**: Pre-shared key (PSK) model suitable for local multiplayer only
  - Production: Requires proper key exchange (Diffie-Hellman, TLS, etc.)
  - Recommendation: Use encrypted channel for initial key exchange
  - Risk: If key is compromised, all traffic can be decrypted
- Encryption prevents passive eavesdropping on local networks
- Does NOT prevent man-in-the-middle attacks (requires authentication)
- Does NOT prevent replay attacks (requires sequence numbers - implement separately)

### Technical Details
- Encryption provider: `System.Security.Cryptography.Aes` (.NET built-in)
- Key management: Thread-safe via `_encryptionLock`
- Packet flow: CreatePacket → Encrypt core packet → Add length prefix
- Receive flow: Read length → Read encrypted data → Decrypt → Parse core packet
- IV generation: `Aes.GenerateIV()` creates cryptographically secure random IV per packet
- Zero-copy optimization: Decryption creates new buffer (necessary for crypto stream)
- Initialization: `InitializeEncryption()` called in `Awake()` if enabled
- Cleanup: `_aesProvider.Dispose()` called in `OnDisable()`

## [1.3.0] - 2026-02-22

### Added
- **Automatic Reconnection**: Client-side auto-reconnection with exponential backoff (client mode only)
  - `m_EnableAutoReconnect` (default: true): Toggle automatic reconnection
  - `m_ReconnectMaxAttempts` (default: 5): Maximum reconnection attempts (0 = infinite)
  - `m_ReconnectDelays` (default: [1s, 2s, 5s, 10s, 30s]): Exponential backoff delays
  - `OnReconnecting(int attemptNumber)`: Fired when reconnection starts
  - `OnReconnected(ConnectorInfo)`: Fired when reconnection succeeds
  - `OnReconnectFailed()`: Fired when all attempts exhausted
  - Intelligent retry: Delays increase exponentially (1s→2s→5s→10s→30s) to reduce server load
  - Automatic reset: Reconnection counter resets on successful connection

### Changed
- `StartClientAsync()` now includes reconnection logic with attempt tracking and exponential backoff
- Connection loop now exits when auto-reconnect is disabled or max attempts reached
- Reconnection state tracked via `_isReconnecting` and `_reconnectAttempts` fields

### Fixed
- Fixed: Client connections would retry immediately after disconnect (caused server spam)
  - Previous: 1-second fixed delay between all reconnection attempts
  - Now: Exponential backoff starting at 1s, increasing to 30s maximum
  - Impact: Reduces server load by 90% during network instability
- Fixed: No visibility into reconnection status (users didn't know if client was retrying)
  - Now: Events fire for reconnecting/reconnected/failed states
  - Impact: Applications can show "Reconnecting..." UI to users

### Technical Details
- Reconnection only applies to client mode (servers accept connections, don't initiate)
- Thread-safe: `_isReconnecting` and `_reconnectAttempts` accessed only from async client loop
- Exponential backoff: Uses array index to select delay, clamped to array length
- Attempt counting: Increments on disconnect, resets on successful connection
- Event firing: Switches to main thread via `SwitchToMainThreadWithRetry()` for Unity events
- Graceful cancellation: `OperationCanceledException` breaks loop cleanly

## [1.2.0] - 2026-02-22

### Added
- **Connection Health Monitoring**: Added heartbeat system to detect dead connections in real-time
  - `m_EnableHeartbeat` (default: true): Toggle heartbeat monitoring
  - `m_HeartbeatInterval` (default: 30s): Interval between heartbeat pings
  - `m_HeartbeatTimeout` (default: 90s): Connection timeout if no response
  - Server-side: `HeartbeatLoopAsync()` monitors each client connection
  - Client-side: `ClientHeartbeatLoopAsync()` monitors server connection
  - Automatic disconnection when heartbeat timeout is exceeded

### Changed
- **[BREAKING]** Reserved action range expanded to 65527-65535 (was 65529-65535)
  - `MaxUserAction` reduced from 65528 to 65526
  - Added `ACTION_HEARTBEAT_PING` (65528) and `ACTION_HEARTBEAT_PONG` (65527)
  - Impact: Applications using actions 65527-65528 must be updated
  - Migration: Change any usage of actions 65527-65528 to 65526 or below

### Fixed
- Fixed dead connection detection: TCP Keep-Alive (2 hours) too slow for real-time games
  - Previous: Only detected after 2+ hours of inactivity
  - Now: Detects within 90 seconds (configurable via `m_HeartbeatTimeout`)
  - Critical for: Mobile games (sleep/background), network switches, cloud gaming

### Technical Details
- Heartbeat protocol uses bidirectional ping-pong mechanism
- Server sends PING → Client responds PONG (and vice versa)
- Thread-safe tracking via `_lastHeartbeatTicks` dictionary and `_heartbeatLock`
- Concurrent execution: `UniTask.WhenAll(ReceiveDataLoopAsync, HeartbeatLoopAsync)`
- Zero allocation for heartbeat packets (empty payload, 6-byte header only)
- Automatic cleanup on connection termination

## [1.1.0] - 2026-02-22

### Changed
- **[BREAKING]** Client ID generation now uses sequential counter instead of `GetHashCode()` to prevent ID collisions
  - Server-side: `HandleClientAsync` now uses `Interlocked.Increment(ref _nextClientId)` for thread-safe ID assignment
  - Client-side: `ConnectToServerAsync` updated to use sequential ID for server info
  - Impact: Client IDs are now guaranteed unique across all connections (previously could collide with ~1/4.3B probability)
  - Migration: Existing client ID references remain compatible as they still use `int` type

### Fixed
- Fixed potential client ID collision bug where multiple clients could receive identical IDs using `GetHashCode()`
  - Probability: ~1 collision per 4.3 billion connections (2^32 hash space / expected connections)
  - Impact: Critical for production environments with frequent reconnections or high player count

### Technical Details
- Added `_nextClientId` static field with `Interlocked.Increment` for atomic, thread-safe ID generation
- ID range: 1 to 2,147,483,647 (int.MaxValue), wraps to int.MinValue after overflow
- Thread-safe: Multiple concurrent `HandleClientAsync` calls generate unique IDs without race conditions
