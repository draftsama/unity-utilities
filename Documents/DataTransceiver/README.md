<!-- markdownlint-disable MD060 -->
# DataTransceiver

A Reliable UDP (RUDP) networking component for Unity. Provides per-message reliability modes, automatic fragmentation, 3-way handshake, heartbeat-based peer detection, auto-reconnect, LAN discovery, and server-side relay — designed for game development where both speed and reliability matter.

---

## Quick Start

### 1. Add to Scene

Attach `DataTransceiver` to a GameObject. Configure in the Inspector:

| Field | Server | Client |
|-------|--------|--------|
| `m_IsServer` | ✅ true | ❌ false |
| `m_Host` | bind address (empty = any) | server IP/hostname |
| `m_Port` | listen port | server port |
| `m_StartOnEnable` | auto-start on enable | auto-start on enable |

### 2. Listen for Messages

```csharp
// Via UnityEvent (Inspector wiring)
// Drag handler onto m_OnMessageReceived in the inspector.

// Via code — subscribe to a specific action
_sub = transceiver.Subscribe(action: 1, msg => {
    Debug.Log(msg.AsString());
});

// Via code — all messages
transceiver.OnMessageReceived.AddListener(msg => {
    Debug.Log($"From peer {msg.SenderPeerId}: action={msg.Action}");
});
```

### 3. Send Data

```csharp
// Client → Server  |  Server → all clients
await transceiver.SendAsync(action: 1, data: bytes);

// With explicit reliability mode
await transceiver.SendAsync(action: 1, bytes, ReliabilityMode.UnreliableSequenced);

// Generic JSON helper
await transceiver.SendAsync<PlayerState>(action: 10, playerState);

// Server → specific client
await transceiver.SendToAsync(peerId: 3, action: 1, bytes);

// Server → all except one
await transceiver.BroadcastAsync(action: 1, bytes, exceptPeerId: 3);

// Client → another client (via server relay)
await transceiver.RelayAsync(targetPeerId: 2, action: 1, bytes);
```

---

## Reliability Modes

Choose per-message based on your data type:

| Mode | Delivery | Order | Drop stale | Use for |
|------|----------|-------|------------|---------|
| `ReliableOrdered` | Guaranteed | ✅ In order | — | Chat, commands, state sync |
| `ReliableUnordered` | Guaranteed | ❌ Any order | — | Independent events |
| `UnreliableSequenced` | Best effort | Drop old | ✅ Yes | Position, rotation, transforms |
| `Unreliable` | Best effort | ❌ | ❌ | VFX triggers, non-critical |

```csharp
using Modules.Utilities;

// Command (must arrive, must be in order)
await dt.SendAsync(ActionIds.PlayerAction, bytes, ReliabilityMode.ReliableOrdered);

// Transform (only latest matters — drop stale)
await dt.SendAsync(ActionIds.Transform, transformBytes, ReliabilityMode.UnreliableSequenced);
```

---

## Events

| Event | Fires when |
|-------|-----------|
| `OnReady` | Server listener started / client handshake completed |
| `OnPeerConnected` | New peer connected (server: client joined; client: server acknowledged) |
| `OnPeerDisconnected` | Peer timed out or sent FIN |
| `OnMessageReceived` | Data message arrived |
| `OnReconnecting` | Client lost connection, auto-reconnect starting |
| `OnReconnected` | Auto-reconnect succeeded |
| `OnError` | Error with type, message, and optional exception |

```csharp
transceiver.OnReady.AddListener(() => Debug.Log("Connected!"));
transceiver.OnPeerConnected.AddListener(peer => Debug.Log($"Peer joined: {peer.IpAddress}:{peer.Port}"));
transceiver.OnPeerDisconnected.AddListener(peer => Debug.Log($"Peer left: #{peer.Id}"));
transceiver.OnError.AddListener(err => Debug.LogError($"[{err.Type}] {err.Message}"));
```

---

## Full API Reference

### Lifecycle

```csharp
await transceiver.StartAsync(CancellationToken ct = default);
await transceiver.StopAsync();
```

### Send

```csharp
// Auto-routes: client→server, server→all
UniTask SendAsync(ushort action, byte[] data, ReliabilityMode mode, CancellationToken ct);
UniTask SendAsync<T>(ushort action, T data, ReliabilityMode mode, CancellationToken ct);

// Server only
UniTask SendToAsync(int peerId, ushort action, byte[] data, ReliabilityMode mode, CancellationToken ct);
UniTask BroadcastAsync(ushort action, byte[] data, ReliabilityMode mode, int exceptPeerId, CancellationToken ct);

// Client only
UniTask RelayAsync(int targetPeerId, ushort action, byte[] data, ReliabilityMode mode, CancellationToken ct);
```

### Subscribe / Unsubscribe

```csharp
// Subscribe to a specific action — returns IDisposable
IDisposable sub = transceiver.Subscribe(action: 42, handler: OnMyMessage);

// Unsubscribe
sub.Dispose();

// Or using 'using' pattern
using var sub = transceiver.Subscribe(42, OnMyMessage);
```

### Status Properties

```csharp
bool  IsRunning      // socket is open
bool  IsConnected    // handshake complete (client) or listener up (server)
bool  IsHandshaking  // mid-handshake
bool  IsReconnecting // auto-reconnect in progress
bool  IsServer
int   LocalPeerId    // assigned by server (0 for server itself)
int   PeerCount
IReadOnlyCollection<PeerInfo> Peers
PeerStats GetStats(int peerId)  // RttMs, PendingAcks
```

### DataMessage

```csharp
msg.SenderPeerId    // int
msg.Action          // ushort
msg.Data            // byte[]
msg.Mode            // ReliabilityMode
msg.ReceivedTicks   // long (DateTime.UtcNow.Ticks)
msg.AsString()      // UTF-8 decode
msg.As<T>()         // JSON deserialize
```

---

## Inspector Settings

### Connection
| Field | Default | Description |
|-------|---------|-------------|
| `m_Host` | `127.0.0.1` | Server IP (client) or bind address (server, empty = 0.0.0.0) |
| `m_Port` | `55555` | UDP port |
| `m_IsServer` | false | Mode toggle |
| `m_StartOnEnable` | true | Auto-start |
| `m_IsDebug` | true | Console logging |

### Reliability
| Field | Default | Description |
|-------|---------|-------------|
| `m_InitialRtoMs` | 200 | Initial retransmit timeout (ms) |
| `m_MaxRtoMs` | 2000 | Maximum retransmit timeout after backoff |
| `m_MaxRetries` | 8 | Max retransmit attempts (0 = infinite for ReliableOrdered) |

### Heartbeat / Timeout
| Field | Default | Description |
|-------|---------|-------------|
| `m_HeartbeatMs` | 1000 | PING interval (ms) |
| `m_PeerTimeoutMs` | 5000 | Disconnect if no PING/data within this window |

### Auto Reconnect (client only)
| Field | Default | Description |
|-------|---------|-------------|
| `m_AutoReconnect` | true | Enable automatic reconnect |
| `m_ReconnectDelaysMs` | 1s 2s 5s 10s 30s | Exponential backoff delay sequence |

### Discovery (UDP broadcast)
| Field | Default | Description |
|-------|---------|-------------|
| `m_EnableDiscovery` | false | Server broadcasts; client listens to find server on LAN |
| `m_DiscoveryPort` | 55556 | Separate UDP port for broadcast |
| `m_DiscoveryIntervalMs` | 1000 | Broadcast interval (ms) |
| `m_DiscoveryTag` | `DT-DISCOVER` | Identifier prefix in broadcast payload |

> When `m_EnableDiscovery = true` and client `m_Host` is empty, the client auto-fills the server address from the first valid broadcast it receives.

### Performance
| Field | Default | Description |
|-------|---------|-------------|
| `m_MaxPayloadKB` | 256 | Hard cap on single-send payload (KB). Larger payloads are fragmented automatically up to this limit. |
| `m_FragmentTtlMs` | 5000 | Incomplete fragment reassembly TTL before GC (ms) |

---

## Wire Protocol

24-byte fixed header, little-endian:

```
Offset  Size  Field
0       2     magic    = 0xD7C0
2       1     version  = 1
3       1     packetType (Data/Ack/Syn/SynAck/Fin/Ping/Pong)
4       1     channel  (0=ReliableOrdered … 3=Unreliable)
5       1     flags    (bit0=fragmented, bit1=relay)
6       2     action   (user-defined 0–65000)
8       4     sequence
12      4     ackSequence (piggyback ACK)
16      4     fragmentId
20      2     fragIndex
22      2     fragTotal
24+     N     payload
```

MTU = 1200 bytes. Payloads > 1176 bytes are automatically fragmented and reassembled.

---

## Architecture

### Threading Model

| Task | Thread | Purpose |
|------|--------|---------|
| `ReceiveLoop` | Thread pool | `UdpClient.ReceiveAsync` → parse → dispatch |
| `RetransmitLoop` | Thread pool | Walks pending ACKs every 50ms; retransmits expired entries |
| `HeartbeatLoop` | Thread pool | Sends PING at `m_HeartbeatMs` to all peers |
| `TimeoutLoop` | Thread pool | Evicts peers silent longer than `m_PeerTimeoutMs` |
| `FragmentGcLoop` | Thread pool | Evicts incomplete fragment buffers past their TTL |
| `Update()` | Main thread | Drains `ConcurrentQueue<Action>` — fires all Unity events |

Events always fire on the main thread via a `ConcurrentQueue` drained in `Update()`. No `SwitchToMainThread()` overhead per-message.

### Handshake (client)

```
Client                Server
  |--- SYN ----------->|   (retry every 500ms until SYN_ACK or timeout)
  |<-- SYN_ACK --------|   (contains assigned peerId)
  |    [OnReady fires] |
  |--- PING ---------->|   (heartbeat begins)
```

### Fragmentation

Messages larger than 1176 bytes are split into MTU-sized chunks, each with a shared `fragmentId`. The receiver reassembles by `fragmentId` + `fragIndex`. Incomplete reassemblies are garbage-collected after `m_FragmentTtlMs`.

---

## Usage Examples

### Chat System (ReliableOrdered)

```csharp
// Sender
await dt.SendAsync<ChatMessage>(action: 1, new ChatMessage { Text = "Hello!" });

// Receiver
dt.Subscribe(action: 1, msg => {
    var chat = msg.As<ChatMessage>();
    chatUI.Append(chat.Text);
});
```

### Transform Sync (UnreliableSequenced)

```csharp
// Send at 20Hz — only latest matters
private void Update()
{
    _sendTimer += Time.deltaTime;
    if (_sendTimer >= 0.05f)
    {
        var data = SerializeTransform(transform);
        dt.SendAsync(action: 2, data, ReliabilityMode.UnreliableSequenced).Forget();
        _sendTimer = 0f;
    }
}

// Receiver — stale packets are dropped automatically
dt.Subscribe(action: 2, msg => ApplyTransform(msg.Data));
```

### LAN Multiplayer Discovery

```csharp
// Server: enable discovery
serverDT.m_EnableDiscovery = true;
await serverDT.StartAsync();

// Client: clear host, enable discovery — auto-finds server on LAN
clientDT.m_EnableDiscovery = true;
clientDT.m_Host = "";
await clientDT.StartAsync();
// Once broadcast is received, m_Host is filled and handshake begins automatically.
```

### Client-to-Client Relay

```csharp
// Client A sends to Client B via server (server routes automatically)
await clientA.RelayAsync(targetPeerId: 3, action: 5, data);

// Client B receives normally via OnMessageReceived or Subscribe
// msg.SenderPeerId reflects Client A's peer id
```

---

## Custom Inspector

The `DataTransceiverEditor` provides:

- **Server / Client** toggle buttons at the top
- **Foldout sections** for all config groups (collapsed by default)
- **Status panel** (runtime) — peer table with Id, IP:Port, RTT ms, pending ACKs
- **Test Harness** (runtime only):
  - Action id input + ReliabilityMode dropdown
  - `Send` / `Broadcast` button (auto-routes by mode)
  - Per-peer `Send To` buttons (server mode)
  - `Relay` button with target peer id (client mode)

---

## Comparison: DataTransceiver vs TCPConnector vs UDPConnector

| Feature | DataTransceiver | TCPConnector | UDPConnector |
|---------|----------------|--------------|--------------|
| Protocol | RUDP (UDP) | TCP | UDP (raw) |
| Reliable delivery | ✅ Per-message | ✅ Always | ❌ |
| Unreliable / sequenced | ✅ Per-message | ❌ | ❌ |
| Fragmentation | ✅ Auto | ✅ TCP handles | ⚠️ Partial |
| Head-of-line blocking | ❌ None | ✅ Blocks | ❌ |
| Handshake | ✅ SYN/SYN_ACK | ✅ 4-step | ❌ |
| Auto-reconnect | ✅ | ✅ | ✅ |
| LAN discovery | ✅ | ✅ | ❌ |
| Relay | ✅ | ✅ | ❌ |
| Latency | Low | Higher | Lowest |
| Best for | Games | File / tool transfer | Legacy |
