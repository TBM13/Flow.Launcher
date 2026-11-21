using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Flow.Launcher.Interop.Programs;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Devices.Properties;
using Windows.Win32.Foundation;

namespace Flow.Launcher.Interop.Hardware;

/// <summary>
/// Represents a Windows Device Manager setup class (category).
/// </summary>
public sealed class DeviceClassInfo
{
    public const string DefaultIconPath = @"%SystemRoot%\System32\setupapi.dll,-18";

    internal DeviceClassInfo(Guid classGuid, string name, string iconPath, bool isNoDisplayClass)
    {
        ClassGuid = classGuid;
        Name = name;
        IconPath = iconPath;
        IsNoDisplayClass = isNoDisplayClass;
    }

    public Guid ClassGuid { get; }
    public string Name { get; }
    public string IconPath { get; }
    public bool IsNoDisplayClass { get; }

    public override string ToString() => Name;
}

/// <summary>
/// Represents a device enumerated from the Windows Configuration Manager (Device Manager).
/// </summary>
public sealed class DeviceInfo
{
    internal DeviceInfo(
        string instanceId,
        string name,
        string? manufacturer,
        string? locationInfo,
        DeviceClassInfo deviceClass,
        string iconPath,
        bool isPresent,
        CM_DEVNODE_STATUS_FLAGS status,
        CM_PROB problem,
        string? problemText)
    {
        InstanceId = instanceId;
        Name = name;
        Manufacturer = manufacturer;
        LocationInfo = locationInfo;
        Class = deviceClass;
        IconPath = iconPath;
        IsPresent = isPresent;

        HasProblem = isPresent && (status & (CM_DEVNODE_STATUS_FLAGS.DN_HAS_PROBLEM | CM_DEVNODE_STATUS_FLAGS.DN_PRIVATE_PROBLEM)) != 0;
        ProblemCode = HasProblem ? (uint)problem : 0;
        ProblemText = problemText;

        IsDisabled = HasProblem && problem is CM_PROB.CM_PROB_DISABLED or CM_PROB.CM_PROB_HARDWARE_DISABLED;
        HasWarning = HasProblem && !IsDisabled;
        CanDisable = isPresent && ((status & CM_DEVNODE_STATUS_FLAGS.DN_DISABLEABLE) != 0 || IsDisabled);

        bool noShowInDeviceManager = (status & CM_DEVNODE_STATUS_FLAGS.DN_NO_SHOW_IN_DM) != 0;
        IsHidden = !isPresent || ((noShowInDeviceManager || deviceClass.IsNoDisplayClass) && !HasProblem);
    }

    public string InstanceId { get; }
    public string Name { get; }
    public string? Manufacturer { get; }
    public string? LocationInfo { get; }
    public DeviceClassInfo Class { get; }
    public string IconPath { get; }
    public bool IsPresent { get; }
    public bool IsHidden { get; }
    public bool IsDisabled { get; }
    public bool CanDisable { get; }
    public bool HasProblem { get; }
    public bool HasWarning { get; }
    public uint ProblemCode { get; }
    public string? ProblemText { get; }

    public override string ToString() => $"{Name} ({InstanceId})";
}

/// <summary>
/// Helper methods for enumerating and interacting with Windows devices via CfgMgr32 and DevMgr.
/// </summary>
public static partial class DeviceHelper
{
    private const string RootDeviceInstanceId = @"HTREE\ROOT\0";

    [LibraryImport("devmgr.dll", EntryPoint = "DeviceProblemTextW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial uint DeviceProblemText(
        nint hMachine,
        uint dnDevInst,
        uint uProblemId,
        char* lpString,
        uint uMaxString);

    /// <summary>
    /// Enumerates all devices on the local machine, including non-present (phantom) and hidden devices.
    /// </summary>
    public static unsafe List<DeviceInfo> GetDevices()
    {
        CONFIGRET cr = PInvoke.CM_Get_Device_ID_List_Size(out uint bufferLen, null, 0);
        if (cr != CONFIGRET.CR_SUCCESS || bufferLen == 0)
            return [];

        char[] buffer = new char[bufferLen];
        fixed (char* bufferPtr = buffer)
        {
            cr = PInvoke.CM_Get_Device_ID_List(null, new PZZWSTR(bufferPtr), bufferLen, 0);
            if (cr != CONFIGRET.CR_SUCCESS)
                return [];
        }

        Dictionary<Guid, DeviceClassInfo> classes = [];
        List<DeviceInfo> devices = [];

        ReadOnlySpan<char> remaining = buffer.AsSpan(0, (int)bufferLen);
        while (!remaining.IsEmpty)
        {
            int nullIndex = remaining.IndexOf('\0');
            if (nullIndex <= 0)
                break;

            ReadOnlySpan<char> instanceIdSpan = remaining[..nullIndex];
            remaining = remaining[(nullIndex + 1)..];

            if (instanceIdSpan.Equals(RootDeviceInstanceId, StringComparison.OrdinalIgnoreCase))
                continue;

            string instanceId = instanceIdSpan.ToString();
            uint devInst;
            fixed (char* idPtr = instanceId)
            {
                cr = PInvoke.CM_Locate_DevNode(
                    out devInst,
                    new PWSTR(idPtr),
                    CM_LOCATE_DEVNODE_FLAGS.CM_LOCATE_DEVNODE_PHANTOM);
            }

            if (cr != CONFIGRET.CR_SUCCESS)
                continue;

            bool isPresent = PInvoke.CM_Get_DevNode_Status(
                out CM_DEVNODE_STATUS_FLAGS status,
                out CM_PROB problem,
                devInst,
                0) == CONFIGRET.CR_SUCCESS;

            string name = GetDeviceStringProperty(devInst, in PInvoke.DEVPKEY_NAME)
                ?? GetDeviceStringProperty(devInst, in PInvoke.DEVPKEY_Device_FriendlyName)
                ?? GetDeviceStringProperty(devInst, in PInvoke.DEVPKEY_Device_DeviceDesc)
                ?? instanceId;

            Guid classGuid = GetDeviceGuidProperty(devInst, in PInvoke.DEVPKEY_Device_ClassGuid);
            if (classGuid == Guid.Empty)
                classGuid = PInvoke.GUID_DEVCLASS_UNKNOWN;

            if (!classes.TryGetValue(classGuid, out DeviceClassInfo? classInfo))
            {
                classInfo = LoadDeviceClassInfo(classGuid);
                classes[classGuid] = classInfo;
            }

            string iconPath = GetDeviceStringProperty(devInst, in PInvoke.DEVPKEY_DrvPkg_Icon)
                ?? classInfo.IconPath;

            string? manufacturer = GetDeviceStringProperty(devInst, in PInvoke.DEVPKEY_Device_Manufacturer);
            string? locationInfo = GetDeviceStringProperty(devInst, in PInvoke.DEVPKEY_Device_LocationInfo);

            bool hasProblem = isPresent && (status & (CM_DEVNODE_STATUS_FLAGS.DN_HAS_PROBLEM | CM_DEVNODE_STATUS_FLAGS.DN_PRIVATE_PROBLEM)) != 0;
            string? problemText = hasProblem ? GetDeviceProblemText(devInst, (uint)problem) : null;

            devices.Add(new DeviceInfo(
                instanceId,
                name,
                manufacturer,
                locationInfo,
                classInfo,
                iconPath,
                isPresent,
                status,
                problem,
                problemText));
        }

        return devices;
    }

    /// <summary>
    /// Opens the native Windows Device Manager Properties dialog for the specified device instance ID.
    /// Does not require administrator privileges.
    /// </summary>
    public static void OpenDeviceProperties(string instanceId)
    {
        string rundll32Path = Path.Combine(Environment.SystemDirectory, "rundll32.exe");
        ProcessHelper.StartProcess(
            rundll32Path,
            arguments: ["devmgr.dll,DeviceProperties_RunDLL", "/DeviceID", instanceId],
            useShellExecute: false);
    }

    /// <summary>
    /// Registers a callback to be invoked whenever any device instance on the system is added, removed, or changes state.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> that unregisters the notification when disposed, or null if registration failed.</returns>
    public static unsafe IDisposable? RegisterDeviceNotification(Action onDevicesChanged)
    {
        ArgumentNullException.ThrowIfNull(onDevicesChanged);

        CM_NOTIFY_FILTER filter = new()
        {
            cbSize = (uint)sizeof(CM_NOTIFY_FILTER),
            Flags = PInvoke.CM_NOTIFY_FILTER_FLAG_ALL_DEVICE_INSTANCES,
            FilterType = CM_NOTIFY_FILTER_TYPE.CM_NOTIFY_FILTER_TYPE_DEVICEINSTANCE
        };

        GCHandle gcHandle = GCHandle.Alloc(onDevicesChanged);
        CONFIGRET cr = PInvoke.CM_Register_Notification(
            in filter,
            (void*)GCHandle.ToIntPtr(gcHandle),
            &OnDeviceNotification,
            out CM_Unregister_NotificationSafeHandle notifyHandle);

        if (cr != CONFIGRET.CR_SUCCESS)
        {
            notifyHandle.Dispose();
            gcHandle.Free();
            return null;
        }

        return new DeviceNotificationRegistration(notifyHandle, gcHandle);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe uint OnDeviceNotification(
        HCMNOTIFICATION hNotify,
        void* context,
        CM_NOTIFY_ACTION action,
        CM_NOTIFY_EVENT_DATA* eventData,
        uint eventDataSize)
    {
        try
        {
            if (context is not null)
            {
                GCHandle handle = GCHandle.FromIntPtr((nint)context);
                if (handle.Target is Action callback)
                    callback();
            }
        }
        catch
        {
            // Never let exceptions escape an unmanaged callback
        }

        return 0;
    }

    private static DeviceClassInfo LoadDeviceClassInfo(Guid classGuid)
    {
        string name = GetClassStringProperty(in classGuid, in PInvoke.DEVPKEY_NAME)
            ?? GetClassStringProperty(in classGuid, in PInvoke.DEVPKEY_DeviceClass_Name)
            ?? GetClassStringProperty(in classGuid, in PInvoke.DEVPKEY_DeviceClass_ClassName)
            ?? classGuid.ToString();

        string iconPath = GetClassStringProperty(in classGuid, in PInvoke.DEVPKEY_DeviceClass_IconPath)
            ?? DeviceClassInfo.DefaultIconPath;

        bool isNoDisplay = GetClassBoolProperty(in classGuid, in PInvoke.DEVPKEY_DeviceClass_NoDisplayClass);

        return new DeviceClassInfo(classGuid, name, iconPath, isNoDisplay);
    }

    private static string? GetDeviceStringProperty(uint devInst, in DEVPROPKEY key)
    {
        Span<byte> stackBuffer = stackalloc byte[512];
        uint size = (uint)stackBuffer.Length;
        CONFIGRET cr = PInvoke.CM_Get_DevNode_Property(devInst, in key, out _, stackBuffer, ref size, 0);
        if (cr == CONFIGRET.CR_SUCCESS)
            return DecodePropertyString(stackBuffer[..(int)size]);

        if (cr == CONFIGRET.CR_BUFFER_SMALL && size > 0)
        {
            byte[] heapBuffer = new byte[size];
            cr = PInvoke.CM_Get_DevNode_Property(devInst, in key, out _, heapBuffer, ref size, 0);
            if (cr == CONFIGRET.CR_SUCCESS)
                return DecodePropertyString(heapBuffer.AsSpan(0, (int)size));
        }

        return null;
    }

    private static Guid GetDeviceGuidProperty(uint devInst, in DEVPROPKEY key)
    {
        Span<byte> buffer = stackalloc byte[16];
        uint size = (uint)buffer.Length;
        CONFIGRET cr = PInvoke.CM_Get_DevNode_Property(devInst, in key, out _, buffer, ref size, 0);
        if (cr == CONFIGRET.CR_SUCCESS && size == 16)
            return MemoryMarshal.Read<Guid>(buffer);

        return Guid.Empty;
    }

    private static string? GetClassStringProperty(in Guid classGuid, in DEVPROPKEY key)
    {
        Span<byte> stackBuffer = stackalloc byte[512];
        uint size = (uint)stackBuffer.Length;
        CONFIGRET cr = PInvoke.CM_Get_Class_Property(in classGuid, in key, out _, stackBuffer, ref size, 0);
        if (cr == CONFIGRET.CR_SUCCESS)
            return DecodePropertyString(stackBuffer[..(int)size]);

        if (cr == CONFIGRET.CR_BUFFER_SMALL && size > 0)
        {
            byte[] heapBuffer = new byte[size];
            cr = PInvoke.CM_Get_Class_Property(in classGuid, in key, out _, heapBuffer, ref size, 0);
            if (cr == CONFIGRET.CR_SUCCESS)
                return DecodePropertyString(heapBuffer.AsSpan(0, (int)size));
        }

        return null;
    }

    private static bool GetClassBoolProperty(in Guid classGuid, in DEVPROPKEY key)
    {
        Span<byte> buffer = stackalloc byte[1];
        uint size = 1;
        CONFIGRET cr = PInvoke.CM_Get_Class_Property(in classGuid, in key, out _, buffer, ref size, 0);
        return cr == CONFIGRET.CR_SUCCESS && size == 1 && buffer[0] != 0;
    }

    private static string? DecodePropertyString(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < sizeof(char))
            return null;

        ReadOnlySpan<char> chars = MemoryMarshal.Cast<byte, char>(bytes);
        int nullIndex = chars.IndexOf('\0');
        ReadOnlySpan<char> slice = nullIndex >= 0 ? chars[..nullIndex] : chars;
        return slice.IsWhiteSpace() ? null : slice.ToString();
    }

    private static unsafe string? GetDeviceProblemText(uint devInst, uint problemCode)
    {
        Span<char> buffer = stackalloc char[512];
        fixed (char* ptr = buffer)
        {
            uint len = DeviceProblemText(nint.Zero, devInst, problemCode, ptr, (uint)buffer.Length);
            if (len > 0 && len < buffer.Length)
                return new string(ptr, 0, (int)len);
        }

        return null;
    }

    private sealed class DeviceNotificationRegistration(
        CM_Unregister_NotificationSafeHandle notifyHandle,
        GCHandle gcHandle) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            notifyHandle.Dispose();
            if (gcHandle.IsAllocated)
                gcHandle.Free();
        }
    }
}
