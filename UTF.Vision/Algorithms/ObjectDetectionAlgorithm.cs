using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Vision.Algorithms
{
    /// <summary>
    /// 目标检测算法
    /// </summary>
    public class ObjectDetectionAlgorithm : IVisionAlgorithm
    {
        private readonly ILogger _logger;
        private readonly Random _random = new();
        private bool _initialized;
        private Dictionary<string, ObjectTemplate> _objectTemplates = new();
        
        public string AlgorithmId => "object_detection";
        public string Name => "目标检测";
        public string Description => "基于模板匹配和特征检测的目标识别算法";
        public string Version => "1.0.0";
        public List<string> SupportedFormats => new() { "RGB", "BGR", "GRAY" };
        
        public ObjectDetectionAlgorithm(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task<bool> InitializeAsync(Dictionary<string, object> parameters)
        {
            try
            {
                _logger.Info("初始化目标检测算法");
                
                // 加载预定义的目标模板
                await LoadObjectTemplatesAsync();
                
                // 模拟模型加载
                await Task.Delay(200);
                
                _initialized = true;
                _logger.Info($"目标检测算法初始化成功，加载了 {_objectTemplates.Count} 个目标模板");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("目标检测算法初始化失败", ex);
                return false;
            }
        }
        
        private async Task LoadObjectTemplatesAsync()
        {
            await Task.Delay(100);
            
            // 预定义的目标模板
            _objectTemplates = new Dictionary<string, ObjectTemplate>
            {
                ["circle"] = new ObjectTemplate
                {
                    Id = "circle",
                    Name = "圆形",
                    Description = "检测圆形目标",
                    MinSize = new Size(10, 10),
                    MaxSize = new Size(500, 500),
                    ExpectedAspectRatio = 1.0,
                    Features = new List<string> { "roundness", "area", "perimeter" }
                },
                ["rectangle"] = new ObjectTemplate
                {
                    Id = "rectangle",
                    Name = "矩形",
                    Description = "检测矩形目标",
                    MinSize = new Size(20, 10),
                    MaxSize = new Size(800, 600),
                    ExpectedAspectRatio = 1.5,
                    Features = new List<string> { "corners", "area", "aspect_ratio" }
                },
                ["line"] = new ObjectTemplate
                {
                    Id = "line",
                    Name = "直线",
                    Description = "检测直线目标",
                    MinSize = new Size(50, 2),
                    MaxSize = new Size(2000, 20),
                    ExpectedAspectRatio = 10.0,
                    Features = new List<string> { "length", "angle", "straightness" }
                },
                ["defect"] = new ObjectTemplate
                {
                    Id = "defect",
                    Name = "缺陷",
                    Description = "检测产品缺陷",
                    MinSize = new Size(3, 3),
                    MaxSize = new Size(100, 100),
                    ExpectedAspectRatio = 1.2,
                    Features = new List<string> { "area", "irregularity", "contrast" }
                },
                ["component"] = new ObjectTemplate
                {
                    Id = "component",
                    Name = "元器件",
                    Description = "检测电子元器件",
                    MinSize = new Size(5, 5),
                    MaxSize = new Size(200, 200),
                    ExpectedAspectRatio = 1.0,
                    Features = new List<string> { "shape", "size", "orientation", "pins" }
                }
            };
        }
        
        public async Task<AlgorithmResult> ProcessAsync(VisionImage image, Dictionary<string, object> parameters)
        {
            if (!_initialized)
            {
                return new AlgorithmResult
                {
                    Success = false,
                    Message = "算法未初始化"
                };
            }
            
            var startTime = DateTime.UtcNow;
            var result = new AlgorithmResult();
            
            try
            {
                _logger.Debug($"开始目标检测: {image.Width}x{image.Height}");
                
                // 获取检测参数
                var targetType = parameters.GetValueOrDefault("target_type", "circle").ToString();
                var confidence = Convert.ToDouble(parameters.GetValueOrDefault("min_confidence", 0.7));
                var maxObjects = Convert.ToInt32(parameters.GetValueOrDefault("max_objects", 50));
                var searchRegions = parameters.GetValueOrDefault("search_regions", null) as List<Rectangle>;
                
                // 执行目标检测
                var detectedObjects = await DetectObjectsAsync(image, targetType, confidence, maxObjects, searchRegions);
                
                result = new AlgorithmResult
                {
                    Success = true,
                    Message = $"检测到 {detectedObjects.Count} 个 {targetType} 目标",
                    Objects = detectedObjects,
                    ProcessingTime = DateTime.UtcNow - startTime,
                    Confidence = detectedObjects.Any() ? detectedObjects.Average(o => o.Confidence) : 0.0,
                    Results = new Dictionary<string, object>
                    {
                        ["target_type"] = targetType,
                        ["detected_count"] = detectedObjects.Count,
                        ["search_regions"] = searchRegions?.Count ?? 0,
                        ["algorithm"] = "template_matching"
                    }
                };
                
                // 计算统计信息
                if (detectedObjects.Any())
                {
                    result.Measurements = CalculateStatistics(detectedObjects, targetType);
                }
                
                _logger.Debug($"目标检测完成: {targetType}, 检测到 {detectedObjects.Count} 个目标, 用时: {result.ProcessingTime.TotalMilliseconds:F1}ms");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                result.ProcessingTime = DateTime.UtcNow - startTime;
                
                _logger.Error("目标检测失败", ex);
            }
            
            return result;
        }
        
        private async Task<List<DetectedObject>> DetectObjectsAsync(VisionImage image, string targetType, double minConfidence, int maxObjects, List<Rectangle>? searchRegions)
        {
            await Task.Delay(100 + _random.Next(0, 200)); // 模拟处理时间
            
            var objects = new List<DetectedObject>();
            
            if (!_objectTemplates.TryGetValue(targetType, out var template))
            {
                _logger.Warning($"未找到目标模板: {targetType}");
                return objects;
            }
            
            // 确定搜索区域
            var regions = searchRegions ?? new List<Rectangle>
            {
                new Rectangle { X = 0, Y = 0, Width = image.Width, Height = image.Height }
            };
            
            // 在每个搜索区域中检测目标
            foreach (var region in regions)
            {
                var regionObjects = await DetectInRegionAsync(image, template, region, minConfidence);
                objects.AddRange(regionObjects);
                
                if (objects.Count >= maxObjects)
                    break;
            }
            
            // 限制返回的目标数量
            return objects.OrderByDescending(o => o.Confidence).Take(maxObjects).ToList();
        }
        
        private async Task<List<DetectedObject>> DetectInRegionAsync(VisionImage image, ObjectTemplate template, Rectangle region, double minConfidence)
        {
            await Task.Delay(20); // 模拟区域处理时间
            
            var objects = new List<DetectedObject>();
            var objectCount = _random.Next(0, 8); // 随机生成0-7个目标
            
            for (int i = 0; i < objectCount; i++)
            {
                var confidence = minConfidence + _random.NextDouble() * (1.0 - minConfidence);
                
                // 生成符合模板约束的目标
                var width = _random.Next(template.MinSize.Width, Math.Min(template.MaxSize.Width, region.Width));
                var height = template.Id == "circle" ? width : 
                            template.Id == "line" ? Math.Max(2, width / 10) :
                            (int)(width / template.ExpectedAspectRatio);
                
                height = Math.Max(template.MinSize.Height, Math.Min(template.MaxSize.Height, height));
                
                var x = region.X + _random.Next(0, Math.Max(1, region.Width - width));
                var y = region.Y + _random.Next(0, Math.Max(1, region.Height - height));
                
                var obj = new DetectedObject
                {
                    Name = $"{template.Name}_{i + 1}",
                    BoundingBox = new Rectangle { X = x, Y = y, Width = width, Height = height },
                    Confidence = confidence,
                    Properties = GenerateObjectProperties(template, width, height)
                };
                
                objects.Add(obj);
            }
            
            return objects;
        }
        
        private Dictionary<string, object> GenerateObjectProperties(ObjectTemplate template, int width, int height)
        {
            var properties = new Dictionary<string, object>();
            
            foreach (var feature in template.Features)
            {
                switch (feature)
                {
                    case "area":
                        properties["area"] = width * height;
                        break;
                    case "perimeter":
                        properties["perimeter"] = template.Id == "circle" ? Math.PI * Math.Max(width, height) : 2 * (width + height);
                        break;
                    case "aspect_ratio":
                        properties["aspect_ratio"] = (double)width / height;
                        break;
                    case "roundness":
                        properties["roundness"] = template.Id == "circle" ? 0.9 + _random.NextDouble() * 0.1 : _random.NextDouble() * 0.3;
                        break;
                    case "angle":
                        properties["angle"] = _random.NextDouble() * 360;
                        break;
                    case "length":
                        properties["length"] = Math.Sqrt(width * width + height * height);
                        break;
                    case "straightness":
                        properties["straightness"] = 0.85 + _random.NextDouble() * 0.15;
                        break;
                    case "irregularity":
                        properties["irregularity"] = _random.NextDouble() * 0.5;
                        break;
                    case "contrast":
                        properties["contrast"] = 0.3 + _random.NextDouble() * 0.7;
                        break;
                    case "corners":
                        properties["corners"] = template.Id == "rectangle" ? 4 : _random.Next(3, 8);
                        break;
                    case "pins":
                        properties["pins"] = _random.Next(2, 64);
                        break;
                    case "orientation":
                        properties["orientation"] = _random.NextDouble() * 360;
                        break;
                    case "shape":
                        properties["shape"] = template.Name;
                        break;
                    case "size":
                        properties["size"] = Math.Max(width, height);
                        break;
                }
            }
            
            return properties;
        }
        
        private Dictionary<string, double> CalculateStatistics(List<DetectedObject> objects, string targetType)
        {
            var stats = new Dictionary<string, double>
            {
                ["count"] = objects.Count,
                ["avg_confidence"] = objects.Average(o => o.Confidence),
                ["min_confidence"] = objects.Min(o => o.Confidence),
                ["max_confidence"] = objects.Max(o => o.Confidence)
            };
            
            if (objects.Any(o => o.Properties.ContainsKey("area")))
            {
                var areas = objects.Where(o => o.Properties.ContainsKey("area"))
                                 .Select(o => Convert.ToDouble(o.Properties["area"])).ToList();
                
                stats["total_area"] = areas.Sum();
                stats["avg_area"] = areas.Average();
                stats["min_area"] = areas.Min();
                stats["max_area"] = areas.Max();
            }
            
            if (objects.Any(o => o.Properties.ContainsKey("angle")))
            {
                var angles = objects.Where(o => o.Properties.ContainsKey("angle"))
                                  .Select(o => Convert.ToDouble(o.Properties["angle"])).ToList();
                
                stats["avg_angle"] = angles.Average();
                stats["angle_std"] = CalculateStandardDeviation(angles);
            }
            
            // 计算分布密度
            if (objects.Count > 0)
            {
                var minX = objects.Min(o => o.BoundingBox.X);
                var maxX = objects.Max(o => o.BoundingBox.X + o.BoundingBox.Width);
                var minY = objects.Min(o => o.BoundingBox.Y);
                var maxY = objects.Max(o => o.BoundingBox.Y + o.BoundingBox.Height);
                
                var totalArea = (maxX - minX) * (maxY - minY);
                stats["distribution_density"] = objects.Count / (double)totalArea * 1000000; // 每平方毫米的目标数
            }
            
            return stats;
        }
        
        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count <= 1) return 0.0;
            
            var mean = values.Average();
            var sumSquaredDiffs = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSquaredDiffs / (values.Count - 1));
        }
        
        public Dictionary<string, AlgorithmParameter> GetParameters()
        {
            return new Dictionary<string, AlgorithmParameter>
            {
                ["target_type"] = new AlgorithmParameter
                {
                    Name = "target_type",
                    DisplayName = "目标类型",
                    Description = "要检测的目标类型",
                    ParameterType = typeof(string),
                    DefaultValue = "circle",
                    IsRequired = true,
                    AllowedValues = _objectTemplates.Keys.Cast<object>().ToList()
                },
                ["min_confidence"] = new AlgorithmParameter
                {
                    Name = "min_confidence",
                    DisplayName = "最小置信度",
                    Description = "检测结果的最小置信度阈值",
                    ParameterType = typeof(double),
                    DefaultValue = 0.7,
                    MinValue = 0.1,
                    MaxValue = 1.0,
                    IsRequired = false
                },
                ["max_objects"] = new AlgorithmParameter
                {
                    Name = "max_objects",
                    DisplayName = "最大目标数",
                    Description = "返回的最大目标数量",
                    ParameterType = typeof(int),
                    DefaultValue = 50,
                    MinValue = 1,
                    MaxValue = 1000,
                    IsRequired = false
                }
            };
        }
        
        public bool ValidateParameters(Dictionary<string, object> parameters)
        {
            try
            {
                if (parameters.ContainsKey("target_type"))
                {
                    var targetType = parameters["target_type"].ToString();
                    if (!_objectTemplates.ContainsKey(targetType))
                        return false;
                }
                
                if (parameters.ContainsKey("min_confidence"))
                {
                    var confidence = Convert.ToDouble(parameters["min_confidence"]);
                    if (confidence < 0.1 || confidence > 1.0)
                        return false;
                }
                
                if (parameters.ContainsKey("max_objects"))
                {
                    var maxObjects = Convert.ToInt32(parameters["max_objects"]);
                    if (maxObjects < 1 || maxObjects > 1000)
                        return false;
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public void Dispose()
        {
            _logger.Info("目标检测算法已释放");
            _objectTemplates.Clear();
            _initialized = false;
        }
    }
    
    /// <summary>
    /// 目标模板定义
    /// </summary>
    public class ObjectTemplate
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public Size MinSize { get; set; }
        public Size MaxSize { get; set; }
        public double ExpectedAspectRatio { get; set; }
        public List<string> Features { get; set; } = new();
    }
    
    /// <summary>
    /// 尺寸结构
    /// </summary>
    public struct Size
    {
        public int Width { get; set; }
        public int Height { get; set; }
        
        public Size(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }
}
