using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.ValueObjects;

public sealed class DeviceFingerprint : ValueObject
{
    public string Value { get; }  // Base64 of Streebog-256(CPU|Disk|BIOS serials)

    public DeviceFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Fingerprint cannot be empty.", nameof(value));
        Value = value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
