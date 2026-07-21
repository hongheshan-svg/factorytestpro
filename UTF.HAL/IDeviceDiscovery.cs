using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.HAL;

[Obsolete("Device abstraction is superseded by the plugin-based driver stack (UTF.Plugins.Drivers). Will be removed in a future version.")]
public interface IDeviceDiscovery
{
    Task<IEnumerable<DeviceInfo>> DiscoverAsync(DeviceType type, CancellationToken ct = default);
    Task<DeviceInfo?> FindByIdAsync(string deviceId, CancellationToken ct = default);
}
