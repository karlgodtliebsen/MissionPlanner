namespace MissionPlanner.Core.ConfigTuning.VendorDevices;

/// <summary>Describes a confirmed apply or rollback result.</summary>
/// <typeparam name="TConfiguration">The device-specific configuration type.</typeparam>
/// <param name="Success">Whether apply and readback succeeded.</param>
/// <param name="Message">The user-facing result.</param>
/// <param name="ConfirmedSnapshot">The confirmed snapshot, when available.</param>
/// <param name="RollbackAttempted">Whether rollback was attempted.</param>
/// <param name="RollbackSucceeded">Whether rollback was confirmed.</param>
public sealed record VendorDeviceApplyResult<TConfiguration>(
    bool Success,
    string Message,
    VendorDeviceSnapshot<TConfiguration>? ConfirmedSnapshot,
    bool RollbackAttempted,
    bool RollbackSucceeded)
    where TConfiguration : IVendorDeviceConfiguration;
