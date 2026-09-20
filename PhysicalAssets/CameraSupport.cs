namespace MoneyMirror.PhysicalAssets;

/// <summary>
/// What the visitor's browser will allow before they tap anything, as reported
/// by <c>describeSupport</c> in camera-scanner.js.
/// </summary>
/// <param name="SecureContext">
/// False on a plain http:// origin. Safari withholds the camera API entirely
/// there, so the failure needs explaining rather than retrying.
/// </param>
/// <param name="HasMediaDevices">Whether navigator.mediaDevices.getUserMedia exists at all.</param>
/// <param name="IsIos">Whether to offer iOS-shaped recovery steps when permission is refused.</param>
public record CameraSupport(bool SecureContext, bool HasMediaDevices, bool IsIos);

/// <summary>
/// The outcome of asking the browser for a camera, as reported by
/// <c>openCamera</c> in camera-scanner.js.
/// </summary>
/// <param name="Code">
/// The DOMException name - <c>NotAllowedError</c> for a refusal,
/// <c>NotFoundError</c> for a device with no camera, and so on. Interop would
/// drop this if the browser threw instead of reporting, and a refusal is the one
/// failure the person can actually do something about.
/// </param>
public record CameraStartResult(bool Ok, string? Facing, string? Code, string? Message);
