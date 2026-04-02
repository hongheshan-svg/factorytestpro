using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UTF.Vision
{
    /// <summary>
    /// 机器视觉系统接口
    /// </summary>
    public interface IVisionSystem : IDisposable
    {
        /// <summary>
        /// 系统ID
        /// </summary>
        string SystemId { get; }
        
        /// <summary>
        /// 系统名称
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// 初始化系统
        /// </summary>
        Task<bool> InitializeAsync();
        
        /// <summary>
        /// 连接相机
        /// </summary>
        Task<bool> ConnectAsync();
        
        /// <summary>
        /// 断开连接
        /// </summary>
        Task DisconnectAsync();
        
        /// <summary>
        /// 获取图像
        /// </summary>
        Task<VisionImage?> CaptureImageAsync();
        
        /// <summary>
        /// 执行检测
        /// </summary>
        Task<InspectionResult> InspectAsync(VisionImage image, InspectionParameters parameters);
        
        /// <summary>
        /// 校准系统
        /// </summary>
        Task<bool> CalibrateAsync(CalibrationParameters parameters);
        
        /// <summary>
        /// 获取系统状态
        /// </summary>
        VisionSystemStatus GetStatus();
    }
    
    /// <summary>
    /// 视觉图像
    /// </summary>
    public class VisionImage
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Channels { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    /// <summary>
    /// 检测结果
    /// </summary>
    public class InspectionResult
    {
        public bool Passed { get; set; }
        public string Message { get; set; } = "";
        public double Score { get; set; }
        public List<DetectedObject> Objects { get; set; } = new();
        public Dictionary<string, object> Measurements { get; set; } = new();
        public TimeSpan ProcessingTime { get; set; }
    }
    
    /// <summary>
    /// 检测对象
    /// </summary>
    public class DetectedObject
    {
        public string Name { get; set; } = "";
        public Rectangle BoundingBox { get; set; } = new();
        public double Confidence { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }
    
    /// <summary>
    /// 矩形区域
    /// </summary>
    public struct Rectangle
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
    
    /// <summary>
    /// 检测参数
    /// </summary>
    public class InspectionParameters
    {
        public List<Rectangle> ROIs { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
        public double Threshold { get; set; } = 0.5;
        public int MaxObjects { get; set; } = 100;
    }
    
    /// <summary>
    /// 校准参数
    /// </summary>
    public class CalibrationParameters
    {
        public int BoardWidth { get; set; } = 9;
        public int BoardHeight { get; set; } = 6;
        public double SquareSize { get; set; } = 25.0; // mm
        public int ImageCount { get; set; } = 10;
        public Dictionary<string, object> Settings { get; set; } = new();
    }
    
    /// <summary>
    /// 视觉系统状态
    /// </summary>
    public class VisionSystemStatus
    {
        public bool IsInitialized { get; set; }
        public bool IsConnected { get; set; }
        public bool IsCalibrated { get; set; }
        public string CurrentMode { get; set; } = "";
        public Dictionary<string, object> SystemInfo { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}