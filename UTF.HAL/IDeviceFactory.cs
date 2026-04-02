namespace UTF.HAL;

public interface IDeviceFactory
{
    IDevice CreateDevice(DeviceInfo info, ICommunicationChannel channel);
    bool CanCreate(DeviceType type);
}
