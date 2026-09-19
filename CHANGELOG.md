## [1.0.0] - 2025-04-12
### First Release
- Initial release of Unity Utilities module.

## [1.0.1] - 2025-04-15
### Fixed
- Fixed a issue VideoController

## [1.0.2] - 2025-04-20
### Added
- Added UIPage slide transition effect.

## [1.0.3] - 2025-04-25
### Fixed
- Fixed a bug in VideoController that fade in was not working correctly.

## [1.0.4] - 2025-04-30
### Added
- Added check and require install dependencies package.

## [1.0.5] - 2025-05-05
### Fixed
- Fixed a bug in TCPConnector where SendDataAsync was not handling progress reporting correctly.
### Added
- Added overload for SendDataAsync in TCPConnector to simplify data sending.

## [1.0.6] - 2025-05-10
### Fixed
- Fixed a bug in ResourceTextureLoader


## [1.0.8] - 2025-05-20
### Fixed
- Fixed TextureUtilties

## [1.0.9] - 2026-07-27
### Added
- Added FileTransfer, a TCP component for moving large files (20-200 MB) between a server and clients on a LAN.
- FileTransfer streams to and from disk, so memory use is bounded by the copy buffer rather than the file size.
- FileTransfer reports progress, supports cancellation, and verifies payloads with SHA-256 via a trailer so neither side reads the file twice.
- FileTransfer enforces server-side path containment, a maximum file size, and a concurrent transfer limit.
- FileTransfer core (FileTransferProtocol, FileTransferServer, FileTransferClient) has no UnityEngine dependency and can be driven from editor tooling or tests without a GameObject.
- FileTransfer supports an optional DataTransceiver control channel for host inheritance and file-available notifications on action id 65100.

## [1.0.10] - 2026-09-19
### Added
- Added UIMaskSoft, a container UI mask (modeled on Unity's built-in Mask) that modulates child Graphic alpha by a black-white (grayscale) texture, with an invert option and soft edges from the texture gradient.
- UIMaskSoft takes the mask texture from the Image or RawImage on the same GameObject, and Show Mask toggles whether that mask graphic is drawn (like Mask.showMaskGraphic).
- UIMaskSoft routes standard children through a shared material via IMaterialModifier (UI/UIMaskSoft shader) and hides content outside the mask rect.
- UIMaskSoft supports TextMeshPro children via a per-instance SDF material variant (TextMeshPro/UIMaskSoft shader) that preserves font atlas, outline and underlay settings.
