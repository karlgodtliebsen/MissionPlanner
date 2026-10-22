using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.Library.MacOs;

/// <summary>
/// Stores planner secrets as generic passwords in the user's login keychain through Security.framework.
/// The application is not sandboxed, so no keychain entitlement is required.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacOsKeychainSecretStore : IPlannerSecretStore
{
    private const string Service = "MissionPlanner.PlannerSecrets";
    private const int Success = 0;
    private const int ItemNotFound = -25300;
    private const string SecurityFramework = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>Gets the secret stored under the key, or null when there is none.</summary>
    public async ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Find(key);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Stores a secret under the key, replacing any existing secret.</summary>
    public async ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Delete(key);
            Add(key, value);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Removes the secret stored under the key.</summary>
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Delete(key);
        }
        finally
        {
            gate.Release();
        }
    }

    private static unsafe string? Find(string key)
    {
        var service = Encoding.UTF8.GetBytes(Service);
        var account = Encoding.UTF8.GetBytes(key);
        uint length = 0;
        void* data = null;
        void* item = null;
        int status;
        fixed (byte* servicePointer = service)
        fixed (byte* accountPointer = account)
        {
            status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)service.Length, servicePointer,
                (uint)account.Length, accountPointer, &length, &data, &item);
        }

        if (status == ItemNotFound)
        {
            return null;
        }

        ThrowIfFailed(status, "read");
        try
        {
            return Encoding.UTF8.GetString((byte*)data, checked((int)length));
        }
        finally
        {
            SecKeychainItemFreeContent(IntPtr.Zero, data);
            if (item != null)
            {
                CFRelease(item);
            }
        }
    }

    private static unsafe void Add(string key, string value)
    {
        var service = Encoding.UTF8.GetBytes(Service);
        var account = Encoding.UTF8.GetBytes(key);
        var password = Encoding.UTF8.GetBytes(value);
        int status;
        fixed (byte* servicePointer = service)
        fixed (byte* accountPointer = account)
        fixed (byte* passwordPointer = password)
        {
            status = SecKeychainAddGenericPassword(IntPtr.Zero, (uint)service.Length, servicePointer,
                (uint)account.Length, accountPointer, (uint)password.Length, passwordPointer, null);
        }

        Array.Clear(password);
        ThrowIfFailed(status, "store");
    }

    private static unsafe void Delete(string key)
    {
        var service = Encoding.UTF8.GetBytes(Service);
        var account = Encoding.UTF8.GetBytes(key);
        void* item = null;
        int status;
        fixed (byte* servicePointer = service)
        fixed (byte* accountPointer = account)
        {
            status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)service.Length, servicePointer,
                (uint)account.Length, accountPointer, null, null, &item);
        }

        if (status == ItemNotFound)
        {
            return;
        }

        ThrowIfFailed(status, "find");
        try
        {
            ThrowIfFailed(SecKeychainItemDelete(item), "remove");
        }
        finally
        {
            CFRelease(item);
        }
    }

    private static void ThrowIfFailed(int status, string operation)
    {
        if (status != Success)
        {
            throw new IOException($"The macOS keychain could not {operation} the secret (OSStatus {status}).");
        }
    }

    [DllImport(SecurityFramework)]
    private static unsafe extern int SecKeychainAddGenericPassword(IntPtr keychain, uint serviceNameLength, byte* serviceName,
        uint accountNameLength, byte* accountName, uint passwordLength, byte* passwordData, void** itemRef);

    [DllImport(SecurityFramework)]
    private static unsafe extern int SecKeychainFindGenericPassword(IntPtr keychainOrArray, uint serviceNameLength, byte* serviceName,
        uint accountNameLength, byte* accountName, uint* passwordLength, void** passwordData, void** itemRef);

    [DllImport(SecurityFramework)]
    private static unsafe extern int SecKeychainItemFreeContent(IntPtr attrList, void* data);

    [DllImport(SecurityFramework)]
    private static unsafe extern int SecKeychainItemDelete(void* itemRef);

    [DllImport(CoreFoundation)]
    private static unsafe extern void CFRelease(void* item);
}
