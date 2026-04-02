using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Vision.Algorithms
{
    /// <summary>
    /// 算法管理器
    /// </summary>
    public class AlgorithmManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, IVisionAlgorithm> _algorithms;
        private readonly Dictionary<string, AlgorithmType> _algorithmTypes;
        
        public AlgorithmManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _algorithms = new Dictionary<string, IVisionAlgorithm>();
            _algorithmTypes = new Dictionary<string, AlgorithmType>();
        }
        
        /// <summary>
        /// 初始化算法管理器
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                _logger.Info("初始化算法管理器");
                
                // 注册内置算法
                await RegisterBuiltInAlgorithmsAsync();
                
                _logger.Info($"算法管理器初始化完成，已注册 {_algorithms.Count} 个算法");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("算法管理器初始化失败", ex);
                return false;
            }
        }
        
        /// <summary>
        /// 注册内置算法
        /// </summary>
        private async Task RegisterBuiltInAlgorithmsAsync()
        {
            // 注册图像处理算法
            var imageProcessing = new ImageProcessingAlgorithm(_logger);
            await RegisterAlgorithmAsync(imageProcessing, AlgorithmType.ImageProcessing);
            
            // 注册目标检测算法
            var objectDetection = new ObjectDetectionAlgorithm(_logger);
            await RegisterAlgorithmAsync(objectDetection, AlgorithmType.ObjectDetection);
            
            // 注册测量算法
            var measurement = new MeasurementAlgorithm(_logger);
            await RegisterAlgorithmAsync(measurement, AlgorithmType.Measurement);
            
            _logger.Info("内置算法注册完成");
        }
        
        /// <summary>
        /// 注册算法
        /// </summary>
        public async Task<bool> RegisterAlgorithmAsync(IVisionAlgorithm algorithm, AlgorithmType algorithmType, Dictionary<string, object>? initParameters = null)
        {
            try
            {
                if (_algorithms.ContainsKey(algorithm.AlgorithmId))
                {
                    _logger.Warning($"算法已存在，将被替换: {algorithm.AlgorithmId}");
                    _algorithms[algorithm.AlgorithmId].Dispose();
                }
                
                // 初始化算法
                var initialized = await algorithm.InitializeAsync(initParameters ?? new Dictionary<string, object>());
                if (!initialized)
                {
                    _logger.Error($"算法初始化失败: {algorithm.AlgorithmId}");
                    return false;
                }
                
                _algorithms[algorithm.AlgorithmId] = algorithm;
                _algorithmTypes[algorithm.AlgorithmId] = algorithmType;
                
                _logger.Info($"算法注册成功: {algorithm.Name} ({algorithm.AlgorithmId})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"注册算法失败: {algorithm?.AlgorithmId}", ex);
                return false;
            }
        }
        
        /// <summary>
        /// 获取算法
        /// </summary>
        public IVisionAlgorithm? GetAlgorithm(string algorithmId)
        {
            return _algorithms.TryGetValue(algorithmId, out var algorithm) ? algorithm : null;
        }
        
        /// <summary>
        /// 执行算法处理
        /// </summary>
        public async Task<AlgorithmResult> ProcessAsync(string algorithmId, VisionImage image, Dictionary<string, object>? parameters = null)
        {
            try
            {
                var algorithm = GetAlgorithm(algorithmId);
                if (algorithm == null)
                {
                    return new AlgorithmResult
                    {
                        Success = false,
                        Message = $"算法不存在: {algorithmId}"
                    };
                }
                
                parameters ??= new Dictionary<string, object>();
                
                // 验证参数
                if (!algorithm.ValidateParameters(parameters))
                {
                    return new AlgorithmResult
                    {
                        Success = false,
                        Message = "参数验证失败"
                    };
                }
                
                _logger.Debug($"执行算法: {algorithm.Name}");
                var result = await algorithm.ProcessAsync(image, parameters);
                
                _logger.Debug($"算法执行完成: {algorithm.Name}, 成功: {result.Success}, 用时: {result.ProcessingTime.TotalMilliseconds:F1}ms");
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"执行算法异常: {algorithmId}", ex);
                return new AlgorithmResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }
        
        /// <summary>
        /// 批量执行算法
        /// </summary>
        public async Task<Dictionary<string, AlgorithmResult>> ProcessBatchAsync(VisionImage image, Dictionary<string, Dictionary<string, object>> algorithmParameters)
        {
            var results = new Dictionary<string, AlgorithmResult>();
            
            foreach (var kvp in algorithmParameters)
            {
                var algorithmId = kvp.Key;
                var parameters = kvp.Value;
                
                try
                {
                    var result = await ProcessAsync(algorithmId, image, parameters);
                    results[algorithmId] = result;
                }
                catch (Exception ex)
                {
                    _logger.Error($"批量执行算法异常: {algorithmId}", ex);
                    results[algorithmId] = new AlgorithmResult
                    {
                        Success = false,
                        Message = ex.Message
                    };
                }
            }
            
            return results;
        }
        
        /// <summary>
        /// 创建算法处理链
        /// </summary>
        public async Task<AlgorithmResult> ProcessChainAsync(VisionImage image, List<AlgorithmChainStep> chainSteps)
        {
            try
            {
                _logger.Debug($"执行算法处理链，步骤数: {chainSteps.Count}");
                
                var currentImage = image;
                var chainResult = new AlgorithmResult
                {
                    Success = true,
                    Message = "算法链执行完成",
                    Results = new Dictionary<string, object>(),
                    Measurements = new Dictionary<string, double>(),
                    Objects = new List<DetectedObject>()
                };
                
                var totalStartTime = DateTime.UtcNow;
                
                for (int i = 0; i < chainSteps.Count; i++)
                {
                    var step = chainSteps[i];
                    _logger.Debug($"执行算法链步骤 {i + 1}: {step.AlgorithmId}");
                    
                    var stepResult = await ProcessAsync(step.AlgorithmId, currentImage, step.Parameters);
                    
                    if (!stepResult.Success)
                    {
                        chainResult.Success = false;
                        chainResult.Message = $"算法链在步骤 {i + 1} 失败: {stepResult.Message}";
                        break;
                    }
                    
                    // 合并结果
                    foreach (var kvp in stepResult.Results)
                    {
                        chainResult.Results[$"step_{i + 1}_{kvp.Key}"] = kvp.Value;
                    }
                    
                    foreach (var kvp in stepResult.Measurements)
                    {
                        chainResult.Measurements[$"step_{i + 1}_{kvp.Key}"] = kvp.Value;
                    }
                    
                    chainResult.Objects.AddRange(stepResult.Objects);
                    
                    // 如果步骤产生了处理后的图像，用作下一步的输入
                    if (stepResult.ProcessedImage != null && step.UseProcessedImageForNext)
                    {
                        currentImage = stepResult.ProcessedImage;
                    }
                }
                
                chainResult.ProcessingTime = DateTime.UtcNow - totalStartTime;
                chainResult.ProcessedImage = currentImage;
                
                _logger.Debug($"算法处理链完成，总用时: {chainResult.ProcessingTime.TotalMilliseconds:F1}ms");
                
                return chainResult;
            }
            catch (Exception ex)
            {
                _logger.Error("算法处理链执行异常", ex);
                return new AlgorithmResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }
        
        public void Dispose()
        {
            try
            {
                _logger.Info("释放算法管理器");
                
                foreach (var algorithm in _algorithms.Values)
                {
                    try
                    {
                        algorithm.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"释放算法失败: {algorithm.AlgorithmId}", ex);
                    }
                }
                
                _algorithms.Clear();
                _algorithmTypes.Clear();
                
                _logger.Info("算法管理器已释放");
            }
            catch (Exception ex)
            {
                _logger.Error("释放算法管理器异常", ex);
            }
        }
    }
    
    /// <summary>
    /// 算法信息
    /// </summary>
    public class AlgorithmInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Version { get; set; } = "";
        public AlgorithmType Type { get; set; }
        public List<string> SupportedFormats { get; set; } = new();
        public Dictionary<string, AlgorithmParameter> Parameters { get; set; } = new();
    }
    
    /// <summary>
    /// 算法链步骤
    /// </summary>
    public class AlgorithmChainStep
    {
        public string AlgorithmId { get; set; } = "";
        public Dictionary<string, object> Parameters { get; set; } = new();
        public bool UseProcessedImageForNext { get; set; } = true;
        public string Description { get; set; } = "";
    }
}
