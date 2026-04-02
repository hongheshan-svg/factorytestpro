using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.HAL;

public interface IDeviceDiscovery
{
    Task<IEnumerable<DeviceInfo>> DiscoverAsync(DeviceType type, CancellationToken ct = default);
    Task<DeviceInfo?> FindByIdAsync(string deviceId, CancellationToken ct = default);
}
