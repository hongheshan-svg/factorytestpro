using System.Threading.Tasks;
using UTF.Business;
using UTF.HAL;
using Xunit;

namespace UTF.Business.Tests;

/// <summary>
/// Smoke tests for <see cref="DeviceManager"/>. Validates construction and
/// the empty-state behavior of the device registry. Deeper scenarios
/// (allocation, health checks, disconnect recovery) belong in follow-up tests.
/// </summary>
public class DeviceManagerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void RegisteredDevices_NoDevicesRegistered_ReturnsEmpty()
    {
        // Arrange
        using var manager = new DeviceManager();

        // Act
        var devices = manager.RegisteredDevices;

        // Assert
        Assert.NotNull(devices);
        Assert.Empty(devices);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDevicesByTypeAsync_NoDevicesRegistered_ReturnsEmpty()
    {
        // Arrange
        using var manager = new DeviceManager();

        // Act
        var devices = await manager.GetDevicesByTypeAsync(DeviceType.Instrument);

        // Assert
        Assert.NotNull(devices);
        Assert.Empty(devices);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RegisterDeviceAsync_NullDevice_ReturnsFalse()
    {
        // Arrange: DeviceManager catches internal NullReferenceException and reports
        // failure as a boolean (see UTF.Business/DeviceManager.cs). A null device
        // therefore surfaces as a `false` result rather than an ArgumentNullException.
        using var manager = new DeviceManager();

        // Act
        var result = await manager.RegisterDeviceAsync(null!);

        // Assert
        Assert.False(result);
    }
}
