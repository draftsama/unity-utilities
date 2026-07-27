<!-- markdownlint-disable MD060 -->
# FileTransfer

TCP file transfer for Unity. Built for 20–200 MB payloads — video, asset bundles — streamed to and from disk with progress, cancellation, and SHA-256 verification.

One connection carries one transfer, then closes. There is no resume: a failed transfer is retried in full.

---

## Quick Start

### Server

Attach `FileTransfer`, press **Server**, and set `m_RootFolder`. It starts on enable and listens on port 55600.

Everything it serves and everything it accepts lives under that root. Nothing outside it is reachable.

### Client

Attach `FileTransfer`, press **Client**, and point `m_Host` at the server.

```csharp
// upload
var result = await fileTransfer.UploadAsync(
    localPath: "/path/to/clip.mp4",
    remoteFolder: "videos",
    progress: new Progress<FileTransferProgress>(p => bar.value = p.Percent));

if (!result.IsSuccess) Debug.LogError($"{result.Status}: {result.Error}");

// download
var down = await fileTransfer.DownloadAsync("clip.mp4", "videos");
Debug.Log($"saved to {down.LocalPath}");
```

Neither method throws for network or protocol failures — check `result.IsSuccess`.

---

## Direction

The server listens; the client dials. The server never opens a connection.

| Goal | Call |
|------|------|
| Client sends a file to the server | `client.UploadAsync(localPath, remoteFolder)` |
| Client fetches a file from the server | `client.DownloadAsync(remoteName, remoteFolder)` |
| Server wants a client to take a file | `server.NotifyFileAvailableAsync(peerId, name)` → client's `OnFileOffered` fires → client calls `DownloadAsync` |

Only the server needs an open port.

---

## Status codes

`FileTransferResult.Status`:

| Status | Meaning |
|--------|---------|
| `Ok` | Transfer completed and verified |
| `Rejected` | `m_AllowUpload` or `m_AllowDownload` is off |
| `NotFound` | File missing — remotely on download, locally on upload |
| `ServerError` | Unexpected server-side failure; see `Error` |
| `TooLarge` | Above `m_MaxFileSizeMB` |
| `Busy` | `m_MaxConcurrentTransfers` reached |
| `BadRequest` | Malformed frame, unsafe path, or shared-secret mismatch |
| `HashMismatch` | Bytes did not survive the trip |
| `Cancelled` | Cancelled through the `CancellationToken` |
| `ConnectionFailed` | Could not reach the server |

---

## Events

| Event | Payload | Fires |
|-------|---------|-------|
| `OnServerReady` | — | Listener bound |
| `OnTransferStarted` | `FileTransferProgress` | Transfer begins |
| `OnTransferProgress` | `FileTransferProgress` | At most every 100 ms |
| `OnTransferCompleted` | `FileTransferResult` | Success |
| `OnTransferFailed` | `FileTransferResult` | Any failure, cancellation included |
| `OnFileOffered` | `FileOffer` | A bridge notification arrived |

All events fire on the main thread — the component drains a queue in `Update()`, the same way `DataTransceiver` does.

---

## Inspector

### Connection
| Field | Default | Description |
|-------|---------|-------------|
| `m_IsServer` | false | Mode toggle |
| `m_Host` | `127.0.0.1` | Client: server address. Empty inherits from the control channel |
| `m_Port` | `55600` | TCP port |
| `m_BindAddress` | empty | Server: bind interface. Empty binds all |
| `m_ConnectTimeoutMs` | `5000` | Client dial timeout |

### Folders
| Field | Default | Description |
|-------|---------|-------------|
| `m_RootFolder` | `FileTransfer` | Server root. Relative paths resolve under `Application.persistentDataPath` |
| `m_DownloadFolder` | `Downloads` | Client save folder, same resolution |

### Limits
| Field | Default | Description |
|-------|---------|-------------|
| `m_MaxFileSizeMB` | `512` | Rejected with `TooLarge` before any disk write |
| `m_MaxConcurrentTransfers` | `4` | Beyond this the server replies `Busy` |
| `m_BufferSizeKB` | `80` | Copy buffer. Peak memory per transfer, regardless of file size |
| `m_IdleTimeoutMs` | `30000` | Per socket operation, not per transfer — a slow 200 MB transfer is never killed |

### Policy
| Field | Default | Description |
|-------|---------|-------------|
| `m_AllowUpload` | true | Server accepts uploads |
| `m_AllowDownload` | true | Server serves downloads |
| `m_VerifyHash` | true | SHA-256 trailer. Off saves CPU on trusted links |
| `m_SharedSecret` | empty | Optional. See Security below |
| `m_ControlChannel` | none | Optional `DataTransceiver`. See below |

---

## Control channel bridge

Assigning a `DataTransceiver` to `m_ControlChannel` is optional. Without it, `FileTransfer` is fully standalone.

With it:

1. **Address inheritance.** A client with an empty `m_Host` copies the transceiver's. The server's LAN discovery result is reused, so the IP is configured once.
2. **Notification.** `NotifyFileAvailableAsync(peerId, name, folder)` sends a `FileOffer` over `DataTransceiver` action **65100** with `ReliableOrdered`.
3. **Receipt.** Clients raise `OnFileOffered`. Nothing downloads automatically — your code decides, then calls `DownloadAsync`.

> Action id **65100** is reserved. Do not use it for game messages.

The file connection is a fresh TCP dial that does not involve `DataTransceiver`, so a client's `peerId` changing across reconnects has no effect on transfers.

---

## Security

**There is no authentication.** Anyone who can reach the port can read and write within the server root, subject to `m_AllowUpload` and `m_AllowDownload`. Run this on a trusted LAN.

What is enforced:

- **Path containment.** `name` and `folder` arrive from the network and are treated as hostile. Names may not contain `/`, `\`, or `:`, folders may not be rooted or contain `..`, and the resolved absolute path must sit under the root. Anything else is `BadRequest`. Separators are checked explicitly rather than through `Path.GetFileName`, because that call is platform-dependent — on Unix a backslash is a legal filename character, so `sub\evil.txt` would survive there and be read as a path on Windows.
- **Size ceiling.** Declared sizes above `m_MaxFileSizeMB` are refused before a byte is written.
- **Concurrency ceiling.** `m_MaxConcurrentTransfers` bounds simultaneous work; extras get `Busy`.
- **Partial files.** Data lands in `<name>.part` and is renamed only after the hash verifies. A partial file never appears under its final name, and failures delete it.

`m_SharedSecret`, when set, must match on both sides or the request is refused. It crosses the wire in plaintext. It stops two installations on the same LAN from talking to each other by accident; it is not a security boundary.

---

## Architecture

| File | Role | References Unity |
|------|------|------------------|
| `FileTransferProtocol.cs` | Wire format, path safety, hashing stream pump | no |
| `FileTransferServer.cs` | `TcpListener`, accept loop, request handling | no |
| `FileTransferClient.cs` | Dial, upload, download | no |
| `FileTransfer.cs` | Inspector, UnityEvents, `UniTask`, bridge | yes |

The three core files use BCL `Task` and no `UnityEngine`, so they can be driven from editor tooling, build scripts, or tests with no `GameObject` and no play mode. Only the MonoBehaviour exposes `UniTask`.

### Wire protocol

12-byte header, little-endian, then UTF-8 JSON metadata:

```
Offset  Size  Field
0       4     magic     = 0x31585446 ("FTX1")
4       1     version   = 1
5       1     code      request: opcode (1=Upload, 2=Download)
                        response: status
6       1     flags     bit0 = a 32-byte SHA-256 trailer follows the payload
7       1     reserved
8       4     metaLen   UTF-8 JSON length, max 65536
12      N     meta
```

The payload is exactly `size` raw bytes after the frame that declared it, optionally followed by the digest.

The digest is a trailer rather than a header field so the sender can hash incrementally while streaming. Neither side reads the file twice.

**Upload**

```
C: REQUEST { Upload, meta{ name, folder, size } }
S: RESPONSE { Ok }                → or Rejected|TooLarge|Busy|BadRequest, then close
C: <size bytes> <32-byte digest>
S: RESPONSE { Ok }                → or HashMismatch|ServerError
```

**Download**

```
C: REQUEST { Download, meta{ name, folder } }
S: RESPONSE { Ok, meta{ size } }  → or NotFound|Rejected|Busy|BadRequest, then close
S: <size bytes> <32-byte digest>
```

Upload ends with an acknowledgement because the sender needs to learn whether the server kept the bytes. Download does not — the client verifies locally.

---

## Comparison

| | FileTransfer | DataTransceiver | TCPConnector |
|---|---|---|---|
| Protocol | TCP | RUDP (UDP) | TCP |
| Built for | Files, 20–200 MB | Game messages | General purpose (legacy) |
| Max payload | `m_MaxFileSizeMB` | 4 MB | 256 MB |
| Streams to disk | ✅ | ❌ in-memory | ❌ in-memory |
| Resume | ❌ | ❌ | ❌ |
| Reliability modes | — always reliable | ✅ 4 modes | — always reliable |
| LAN discovery | via control channel | ✅ | ✅ |

Use `DataTransceiver` for game traffic and `FileTransfer` for files. They use different ports and different sockets, so a large transfer does not compete with gameplay messages.
