using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Domain.ValueObjects;
using ConSecOrg.Infrastructure.Crypto;
using System.Management;
using System.Text;

namespace ConSecOrg.Infrastructure.Device;

public sealed class WindowsDeviceService : IDeviceService
{
    private readonly StreebogHasher _hasher = new();
    private DeviceFingerprint? _cached;

    public DeviceFingerprint GetFingerprint()
    {
        if (_cached is not null) return _cached;

        var cpu = GetWmiValue("Win32_Processor", "ProcessorId");
        var disk = GetWmiValue("Win32_DiskDrive", "SerialNumber");
        var bios = GetWmiValue("Win32_BIOS", "SerialNumber");

        var raw = $"{cpu}|{disk}|{bios}";
        var hash = _hasher.Hash256(Encoding.UTF8.GetBytes(raw));
        _cached = new DeviceFingerprint(Convert.ToBase64String(hash));
        return _cached;
    }

    public bool Matches(string storedFingerprint)
    {
        var current = GetFingerprint();
        return string.Equals(current.Value, storedFingerprint, StringComparison.Ordinal);
    }

    private static string GetWmiValue(string wmiClass, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
            foreach (ManagementObject obj in searcher.Get())
            {
                var value = obj[property]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
        }
        catch
        {
            // WMI may fail in some environments; fall back to empty string
        }
        return "UNKNOWN";
    }
}
