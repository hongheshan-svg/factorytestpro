using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UTF.Vision.Algorithms
{
    /// <summary>
    /// 机器视觉算法接口
    /// </summary>
    public interface IVisionAlgorithm
    {
        /// <summary>
        /// 算法ID
        /// </summary>
        string AlgorithmId { get; }
        
        /// <summary>
        /// 算法名称
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// 算法描述
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// 算法版本
        /// </summary>
        string Version { get; }
        
        /// <summary>
        /// 支持的图像格式
        /// </summary>
        List<string> SupportedFormats { get; }
        
        /// <summary>
        /// 初始化算法
        /// </summary>
        Task<bool> InitializeAsync(Dictionary<string, object> parameters);
        
        /// <summary>
        /// 执行算法处理
        /// </summary>
        Task<AlgorithmResult> ProcessAsync(VisionImage image, Dictionary<string, object> parameters);
        
        /// <summary>
        /// 获取算法配置参数
        /// </summary>
        Dictionary<string, AlgorithmParameter> GetParameters();
        
        /// <summary>
        /// 验证参数有效性
        /// </summary>
        bool ValidateParameters(Dictionary<string, object> parameters);
        
        /// <summary>
        /// 释放资源
        /// </summary>
        void Dispose();
    }
    
    /// <summary>
    /// 算法结果
    /// </summary>
    public class AlgorithmResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public TimeSpan ProcessingTime { get; set; }
        public Dictionary<string, object> Results { get; set; } = new();
        public List<DetectedObject> Objects { get; set; } = new();
        public VisionImage? ProcessedImage { get; set; }
        public Dictionary<string, double> Measurements { get; set; } = new();
        public double Confidence { get; set; }
    }
    
    /// <summary>
    /// 算法参数定义
    /// </summary>
    public class AlgorithmParameter
    {
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public Type ParameterType { get; set; } = typeof(object);
        public object DefaultValue { get; set; } = new();
        public object MinValue { get; set; } = new();
        public object MaxValue { get; set; } = new();
        public bool IsRequired { get; set; }
        public List<object> AllowedValues { get; set; } = new();
    }
    
    /// <summary>
    /// 算法类型枚举
    /// </summary>
    public enum AlgorithmType
    {
        ImageProcessing,    // 图像处理
        ObjectDetection,    // 目标检测
        Measurement,        // 测量
        Classification,     // 分类
        OCR,               // 光学字符识别
        QualityInspection, // 质量检测
        Calibration,       // 校准
        Custom            // 自定义
    }
}
