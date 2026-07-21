namespace UTF.HAL;

[Obsolete("Device abstraction is superseded by the plugin-based driver stack (UTF.Plugins.Drivers). Will be removed in a future version.")]
public interface IDeviceFactory
{
    IDevice CreateDevice(DeviceInfo info, ICommunicationChannel channel);
    bool CanCreate(DeviceType type);
}
