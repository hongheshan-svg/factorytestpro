using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UTF.HAL
{
    /// <summary>
    /// 简单的设备实现
    /// </summary>
    public class GenericDevice : IDevice
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DeviceType DeviceType { get; set; }
        public bool IsConnected { get; set; }
        public DeviceInfo Info { get; set; } = new();

        public event EventHandler<DeviceEventArgs>? StatusChanged;
        public event EventHandler<DeviceEventArgs>? ErrorOccurred;
        public event EventHandler<DeviceEventArgs>? DataReceived;

        protected virtual void OnStatusChanged(DeviceEventArgs e) => StatusChanged?.Invoke(this, e);
        protected virtual void OnErrorOccurred(DeviceEventArgs e) => ErrorOccurred?.Invoke(this, e);
        protected virtual void OnDataReceived(DeviceEventArgs e) => DataReceived?.Invoke(this, e);

        public async Task<DeviceOperationResult> ConnectAsync(CancellationToken cancellationToken = default)
        {
            var start = DateTime.Now;
            await Task.Delay(100, cancellationToken); // 模拟连接时间
            IsConnected = true;
            return DeviceOperationResult.CreateSuccess(true, DateTime.Now - start);
        }

        public async Task<DeviceOperationResult> DisconnectAsync(CancellationToken cancellationToken = default)
        {
            var start = DateTime.Now;
            await Task.Delay(100, cancellationToken); // 模拟断开时间
            IsConnected = false;
            return DeviceOperationResult.CreateSuccess(true, DateTime.Now - start);
        }

        public async Task<DeviceOperationResult> InitializeAsync(Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
        {
            var start = DateTime.Now;
            await Task.Delay(100, cancellationToken);
            return DeviceOperationResult.CreateSuccess(true, DateTime.Now - start);
        }

        public async Task<DeviceOperationResult> ResetAsync(CancellationToken cancellationToken = default)
        {
            var start = DateTime.Now;
            await Task.Delay(100, cancellationToken);
            return DeviceOperationResult.CreateSuccess(true, DateTime.Now - start);
        }

        public async Task<DeviceOperationResult> ExecuteCommandAsync(string command, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
        {
            var start = DateTime.Now;
            await Task.Delay(50, cancellationToken);
            return DeviceOperationResult.CreateSuccess($"Command '{command}' executed", DateTime.Now - start);
        }

        public async Task<DeviceOperationResult> ReadDataAsync(string dataType, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
        {
            var start = DateTime.Now;
            await Task.Delay(50, cancellationToken);
            return DeviceOperationResult.CreateSuccess($"Data of type '{dataType}'", DateTime.Now - start);
        }

        public async Task<DeviceOperationResult> WriteDataAsync(string dataType, object data, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
        {
            var start = DateTime.Now;
            await Task.Delay(50, cancellationToken);
            return DeviceOperationResult.CreateSuccess(true, DateTime.Now - start);
        }

        public async Task<DeviceOperationResult> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            var start = DateTime.Now;
            await Task.Delay(20, cancellationToken);
            return DeviceOperationResult.CreateSuccess(IsConnected ? DeviceStatus.Connected : DeviceStatus.Disconnected, DateTime.Now - start);
        }

        public async Task<DeviceOperationResult> ConfigureAsync(Dictionary<string, object> configuration, CancellationToken cancellationToken = default)
        {
            var start = DateTime.Now;
            await Task.Delay(50, cancellationToken);
            return DeviceOperationResult.CreateSuccess(true, DateTime.Now - start);
        }

        public async Task<DeviceOperationResult> CalibrateAsync(Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
        {
            var start = DateTime.Now;
            await Task.Delay(100, cancellationToken);
            return DeviceOperationResult.CreateSuccess(true, DateTime.Now - start);
        }

        public async Task<DeviceOperationResult> SelfTestAsync(CancellationToken cancellationToken = default)
        {
            var start = DateTime.Now;
            await Task.Delay(200, cancellationToken);
            return DeviceOperationResult.CreateSuccess(IsConnected, DateTime.Now - start);
        }

        public void Dispose()
        {
            IsConnected = false;
        }
    }
}
