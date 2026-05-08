using ConSecOrg.Domain.ValueObjects;

namespace ConSecOrg.Domain.Interfaces.Services;

public interface IDeviceService
{
    DeviceFingerprint GetFingerprint();
    bool Matches(string storedFingerprint);
}
