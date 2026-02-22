# TCPConnector Changelog

## Version 2.0 - 2026-02-22

Complete rewrite based on Old/TCPConnector.cs (v1.x) with significant improvements and simplifications.

### 📊 File Size Comparison
- **Old Version**: 2,842 lines
- **New Version**: 3,008 lines (+166 lines, +5.8%)
- Net increase due to new enterprise features (Heartbeat, Auto-Reconnection, Enhanced Handshake) exceeding Real-time Mode removal

---

## 🎉 Added Features

### 1. **Heartbeat Monitoring System**
- **New Setting**: `m_EnableHeartbeat` (default: true)
- **New Settings**: `m_HeartbeatInterval` (15000ms), `m_HeartbeatTimeout` (15000ms)
- **Protocol**: ACTION_HEARTBEAT_PING (65528), ACTION_HEARTBEAT_PONG (65527)
- **Server-side**: Monitors each client connection with automatic timeout detection
- **Client-side**: Monitors server connection with automatic timeout detection
- **Benefits**: 
  - Detects dead connections within 15 seconds
  - Prevents ghost clients from staying in session
  - Complements TCP keep-alive with application-level monitoring

**Usage**:
```csharp
// Enable/disable heartbeat
m_EnableHeartbeat = true;
m_HeartbeatInterval = 10000; // Send PING every 10 seconds
m_HeartbeatTimeout = 15000;  // Timeout if no PONG within 15 seconds
```

### 2. **Auto Reconnection System (Client Mode)**
- **New Settings**: 
  - `m_EnableAutoReconnect` (default: true)
  - `m_ReconnectMaxAttempts` (default: 5, 0 = infinite)
  - `m_ReconnectDelays` (exponential backoff: 1s, 2s, 5s, 10s, 30s)
- **New Events**:
  - `m_OnReconnecting(int attemptNumber)` - Fired when reconnection starts
  - `m_OnReconnected(ConnectorInfo)` - Fired on successful reconnection
  - `m_OnReconnectFailed()` - Fired when all attempts exhausted
- **Smart Backoff**: Uses exponential delay pattern to avoid server overload
- **State Tracking**: Maintains reconnection state across disconnect events

**Benefits**:
- Automatic recovery from temporary network issues
- No manual reconnection code needed
- Configurable retry strategy

**Usage**:
```csharp
// Listen for reconnection events
m_OnReconnecting.AddListener((attempt) => {
    Debug.Log($"Reconnecting... Attempt {attempt}/{m_ReconnectMaxAttempts}");
});

m_OnReconnected.AddListener((serverInfo) => {
    Debug.Log($"Reconnected to {serverInfo.ipAddress}:{serverInfo.port}");
});

m_OnReconnectFailed.AddListener(() => {
    Debug.LogError("Failed to reconnect after all attempts");
});
```

### 3. **Enhanced 3-Way Handshake Protocol**
- **Old Behavior**: Simple TCP accept → immediate data transfer
- **New Behavior**: Structured 3-step handshake
  1. **HELLO** (65535): Server sends client ID assignment
  2. **ACK** (65534): Client acknowledges receipt
  3. **READY** (65533): Server confirms ready for data
- **New Constants**: ACTION_HELLO, ACTION_ACK, ACTION_READY
- **Timeout**: 5-second handshake timeout (HANDSHAKE_TIMEOUT_MS)
- **State Tracking**: ConnectionState enum (Pending → HelloSent → Acknowledged → Ready)
- **Pending Management**: _pendingConnections dictionary tracks handshake progress

**Benefits**:
- Reliable connection establishment
- Prevents premature data transmission
- Clear connection lifecycle states
- Better error detection during connection setup

### 4. **Thread-Safe Send Operations**
- **New System**: Per-stream send locks (_streamSendLocks)
- **New Lock**: _streamSendLocksLock for dictionary access
- **Prevents**: Race conditions from concurrent sends (heartbeat + user data + PONG response)
- **Memory Safety**: Automatic cleanup in finally blocks (v2.7.3 leak fix)

**Impact**:
- Eliminates data corruption from simultaneous writes
- Safe for multi-threaded send operations
- Critical for heartbeat system stability

### 5. **Send Timeout System**
- **New Setting**: `m_SendTimeout` (default: 30000ms)
- **Enforcement**: All send operations have configurable timeout
- **Exception**: Throws TimeoutException when timeout exceeded
- **Progress**: Detailed progress reporting during timeout

**Benefits**:
- Prevents indefinite hangs on slow/dead connections
- Configurable per-project needs
- Better error diagnostics

### 6. **Improved Error Handling**
- **Handshake Errors**: Specific timeout and validation errors
- **Send Errors**: Timeout vs network errors clearly separated
- **Cleanup**: Comprehensive finally blocks ensure resource cleanup
- **Logging**: Detailed error messages with context

---

## 🗑️ Removed Features

### 1. **Real-time Mode (UDP-based)**
Removed entire UDP-based real-time communication system.

**Removed Settings**:
- `m_EnableRealTimeMode`
- `m_RealTimePort` (was 54323)

**Removed Variables**:
- `_realTimeServer` (UdpClient)
- `_realTimeClient` (UdpClient)
- `m_RealTimeClients` (Dictionary<IPEndPoint, ConnectorInfo>)

**Removed Events**:
- `m_OnRealTimeDataReceived`

**Removed Methods**:
- `StartRealTimeServerAsync()`
- `ReceiveRealTimeDataLoopAsync()`
- `SendRealTimeDataAsync()`
- `GetRealTimeClientInfoList()`
- `IsRealTimeEnabled` property
- `ActiveRealTimeClientCount` property

**Removed API Examples**:
```csharp
// ❌ NO LONGER AVAILABLE
if (connector.IsRealTimeEnabled) {
    await connector.SendRealTimeDataAsync(action, data);
}
connector.m_OnRealTimeDataReceived.AddListener(...);
```

**Impact**: ~400 lines removed

**Reason for Removal**:
- Added significant complexity (parallel UDP + TCP management)
- Rarely used in typical game scenarios
- TCP with optimizations sufficient for most use cases
- Reduced maintenance burden
- Modern games prefer WebRTC/custom solutions

**Migration**:
- For low-latency needs: Optimize TCP settings (NoDelay, buffer size)
- For unreliable messaging: Implement custom UDP solution
- For game state sync: Use dedicated netcode library

---

## 🔄 Modified Features

### 1. **Client List Synchronization**
- **Old**: Basic sync broadcast
- **New**: Enhanced with handshake integration
  - Only broadcasts to clients with completed handshake
  - Integrated with ConnectionState tracking
  - More reliable sync timing

### 2. **Connection Lifecycle**
- **Old**: Direct accept + immediate events
- **New**: Structured lifecycle with states
  1. TCP Accept
  2. Handshake (3-way: HELLO → ACK → READY)
  3. Event firing (m_OnClientConnected / m_OnServerConnected)
  4. Data transfer enabled
  5. Heartbeat monitoring active
  6. Graceful disconnect / Auto-reconnect

### 3. **Disconnect Detection**
- **Old**: Relied on TCP errors + manual timeout
- **New**: Multi-layered detection
  - TCP connection state
  - Heartbeat timeout (15s)
  - Send operation failures
  - Explicit disconnect calls
- **Result**: Faster and more reliable dead connection detection

### 4. **Buffer Pool Management**
- **Old**: Unlimited buffer pool growth
- **New**: Capped at MAX_BUFFER_POOL_SIZE (20 buffers)
- **Benefit**: Prevents memory bloat on long-running servers

---

## 🐛 Bug Fixes

### Critical Fixes

**v2.7.6**: GetAllConnectedClientsInfo() Stale Data
- **Problem**: Server mode returned stale m_SyncedClientList instead of real-time m_Clients
- **Fix**: 
  ```csharp
  // Now returns real-time data in server mode
  if (m_IsServer) {
      lock (m_Clients) {
          return m_Clients.Values.ToList();
      }
  }
  ```
- **Impact**: Real-time accurate client list in server mode

**v2.7.3**: Memory Leak in Send Locks
- **Problem**: _streamSendLocks never cleaned up after disconnect
- **Fix**: Added disposal in finally blocks
  ```csharp
  finally {
      lock (_streamSendLocksLock) {
          if (_streamSendLocks.TryGetValue(client, out var semaphore)) {
              semaphore.Dispose();
              _streamSendLocks.Remove(client);
          }
      }
  }
  ```

---

## 💔 Breaking Changes

### Removed Inspector Fields
- `m_EnableRealTimeMode`
- `m_RealTimePort`

### Removed Events
- `m_OnRealTimeDataReceived`

### Removed API Methods
- `SendRealTimeDataAsync(ushort action, byte[] data, ...)`
- `GetRealTimeClientInfoList()`
- `IsRealTimeEnabled` (property)
- `ActiveRealTimeClientCount` (property)

### Removed Internal Variables
Access to these was never public, no impact:
- `_realTimeServer`
- `_realTimeClient`
- `m_RealTimeClients`

---

## 📋 What Remains (Core Features)

### Connection Management
- ✅ TCP Client/Server modes
- ✅ UDP Auto-discovery (server broadcast on LAN)
- ✅ Heartbeat monitoring (15s timeout)
- ✅ Auto-reconnection (exponential backoff)
- ✅ 3-way handshake (HELLO → ACK → READY)
- ✅ Thread-safe operations

### Data Transfer
- ✅ Send/Receive with progress tracking
- ✅ Client list synchronization
- ✅ Client-to-client relay messages
- ✅ Packet fragmentation handling
- ✅ Buffer pooling for performance

### Event System
- ✅ Connection/Disconnection events
- ✅ Data received events
- ✅ Error events
- ✅ Client list update events
- ✅ Reconnection events (new)

### Quality of Life
- ✅ Inspector-friendly settings
- ✅ Retry mechanisms
- ✅ Debug logging controls
- ✅ Performance optimizations
- ✅ Memory leak prevention

---

## 🎯 Migration Guide (v1.x → v2.0)

### If You Used Real-time Mode:

**Old Code**:
```csharp
// Real-time mode in v1.x
connector.m_EnableRealTimeMode = true;
connector.m_RealTimePort = 54323;

// Sending real-time data
await connector.SendRealTimeDataAsync(ACTION_POSITION, positionData);

// Receiving real-time data
connector.m_OnRealTimeDataReceived.AddListener((packet) => {
    Vector3 pos = packet.GetData<Vector3>();
    UpdatePlayerPosition(pos);
});
```

**New Code (Migration Options)**:

**Option 1**: Use TCP for all data (recommended for most cases)
```csharp
// v2.0 - Use regular TCP send (optimized)
await connector.SendDataAsync(ACTION_POSITION, positionData);

connector.m_OnDataReceived.AddListener((packet) => {
    if (packet.action == ACTION_POSITION) {
        Vector3 pos = packet.GetData<Vector3>();
        UpdatePlayerPosition(pos);
    }
});

// Optimize TCP for low latency
connector.m_BufferSize = 4096; // Smaller buffer for lower latency
```

**Option 2**: Implement custom UDP (for specialized needs)
```csharp
// Create separate UDP client for real-time data
using (var udpClient = new UdpClient()) {
    udpClient.Connect(serverIP, customUdpPort);
    byte[] data = SerializePosition(position);
    await udpClient.SendAsync(data, data.Length);
}
```

**Option 3**: Use third-party netcode library
```csharp
// Consider libraries like:
// - Mirror Networking
// - Netcode for GameObjects
// - Photon Unity Networking
```

### If You Checked IsRealTimeEnabled:

**Old Code**:
```csharp
if (connector.IsRealTimeEnabled) {
    // Real-time specific logic
}
```

**New Code**:
```csharp
// Remove the check, or replace with your own flag
if (useOptimizedMode) {
    // Your optimized logic
}
```

### If You Tracked ActiveRealTimeClientCount:

**Old Code**:
```csharp
int realtimeClients = connector.ActiveRealTimeClientCount;
```

**New Code**:
```csharp
// Use regular connected client count
int connectedClients = connector.ConnectedClientCount;
```

---

## ⚡ Performance Notes

### Improvements
- **Reduced Complexity**: One protocol (TCP only) simplifies logic
- **Better Memory Management**: Fixed leaks, capped buffer pool
- **Faster Dead Connection Detection**: 15s heartbeat timeout vs manual checking
- **Thread Safety**: Eliminated race conditions in send operations

### Considerations
- **Latency**: TCP-only means slightly higher latency than UDP real-time mode
  - Mitigation: Optimize TCP settings, reduce message size
- **Bandwidth**: Heartbeat adds ~200 bytes/15s per connection (negligible)
- **CPU**: Auto-reconnection loops use minimal CPU during backoff delays

---

## 🔧 Recommended Settings

### For Low-Latency Games (Fast-paced)
```csharp
m_BufferSize = 4096;           // Smaller buffer
m_HeartbeatInterval = 5000;    // More frequent heartbeat
m_EnableAutoReconnect = true;  // Auto-recovery
```

### For Reliable Games (Turn-based, RPG)
```csharp
m_BufferSize = 8192;           // Default
m_HeartbeatInterval = 15000;   // Standard interval
m_EnableAutoReconnect = true;  // Auto-recovery
m_ReconnectMaxAttempts = 10;   // More retries
```

### For Server Performance (Many Clients)
```csharp
m_MaxConcurrentConnections = 50; // Adjust to capacity
m_EnableHeartbeat = true;        // Critical for cleanup
m_HeartbeatTimeout = 10000;      // Faster dead client removal
```

---

## 📚 Version History Summary

- **v2.0** (2026-02-22): 
  - ✅ Added: Heartbeat, Auto-Reconnection, Enhanced Handshake
  - ❌ Removed: Real-time Mode (UDP)
  - 🐛 Fixed: Memory leaks, stale client list
  - 📊 Size: 2,842 → 3,008 lines (+5.8%)

- **v1.x** (Old/TCPConnector.cs):
  - Basic TCP + UDP Real-time Mode
  - Simple connection lifecycle
  - Manual timeout management

---

## 🙏 Acknowledgments

This version represents a focused evolution toward production-ready networking:
- **Enterprise-grade reliability**: Heartbeat + Auto-reconnection
- **Simplified architecture**: Removed rarely-used UDP complexity
- **Better debuggability**: Clear lifecycle states and error messages
- **Memory safety**: Fixed leaks and race conditions

**Target Use Cases**: Unity multiplayer games with ≤50 concurrent connections needing reliable TCP communication with automatic recovery.
