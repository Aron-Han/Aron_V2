Pinned LMI Gocator runtime (AMD64), Windows 10/11

GoSdk.dll / GoSdkNet.dll: 6.5.2.5
kApi.dll / kApiNet.dll: 10.3.3.22 (matching vendor-supplied pair)
vcruntime140.dll / ucrtbase.dll: from the SAME GO Utilities package.
SHA-256 and original source provenance: runtime-manifest.json.

Keep this complete folder beside Visual Inspection.exe. No GoSDK installation,
PATH changes, registration, or SDK directory input is required for this bundle.
Windows API sets and .NET 4.7.2+ remain OS prerequisites; VisionPro 9.6 SR2 and
its licensing still need their normal installation. This is NOT a replacement
for the VisionPro runtime, a camera firmware update, or an Accelerator service.

Do not copy these DLLs into the EXE root or the Cognex installation directory.
Do not combine DLLs from other GoSDK releases. Build validates the pinned hashes
and automatically copies this folder to each output (including custom builds).
Legacy XML SdkDirectory should be empty to use this portable bundle.
The original API-set forwarding DLLs in the vendor win64 folder were x86;
they are intentionally excluded. Supported Windows 10/11 supplies API sets.
