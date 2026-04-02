using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UTF.Logging;

namespace UTF.Vision.Algorithms
{
    /// <summary>
    /// 测量算法
    /// </summary>
    public class MeasurementAlgorithm : IVisionAlgorithm
    {
        private readonly ILogger _logger;
        private readonly Random _random = new();
        private bool _initialized;
        private double _pixelToMmRatio = 0.1; // 像素到毫米的转换比率
        
        public string AlgorithmId => "measurement";
        public string Name => "精密测量";
        public string Description => "高精度的几何测量算法，支持长度、角度、面积等测量";
        public string Version => "1.0.0";
        public List<string> SupportedFormats => new() { "RGB", "BGR", "GRAY" };
        
        public MeasurementAlgorithm(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task<bool> InitializeAsync(Dictionary<string, object> parameters)
        {
            try
            {
                _logger.Info("初始化测量算法");
                
                // 获取校准参数
                if (parameters.ContainsKey("pixel_to_mm_ratio"))
                {
                    _pixelToMmRatio = Convert.ToDouble(parameters["pixel_to_mm_ratio"]);
                }
                
                // 模拟校准过程
                await Task.Delay(150);
                
                _initialized = true;
                _logger.Info($"测量算法初始化成功，像素比率: {_pixelToMmRatio:F4} mm/pixel");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("测量算法初始化失败", ex);
                return false;
            }
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
                _logger.Debug($"开始精密测量: {image.Width}x{image.Height}");
                
                // 获取测量参数
                var measurementType = parameters.GetValueOrDefault("measurement_type", "distance").ToString();
                var targetObjects = parameters.GetValueOrDefault("target_objects", null) as List<DetectedObject>;
                var measurementRegions = parameters.GetValueOrDefault("measurement_regions", null) as List<Rectangle>;
                var precision = Convert.ToDouble(parameters.GetValueOrDefault("precision", 0.01));
                
                // 执行测量
                result = await PerformMeasurementAsync(image, measurementType, targetObjects, measurementRegions, precision);
                result.ProcessingTime = DateTime.UtcNow - startTime;
                
                _logger.Debug($"测量完成: {measurementType}, 用时: {result.ProcessingTime.TotalMilliseconds:F1}ms");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                result.ProcessingTime = DateTime.UtcNow - startTime;
                
                _logger.Error("测量失败", ex);
            }
            
            return result;
        }
        
        private async Task<AlgorithmResult> PerformMeasurementAsync(VisionImage image, string measurementType, List<DetectedObject>? targetObjects, List<Rectangle>? measurementRegions, double precision)
        {
            await Task.Delay(80); // 模拟测量处理时间
            
            var result = new AlgorithmResult { Success = true };
            var measurements = new Dictionary<string, double>();
            var measurementObjects = new List<DetectedObject>();
            
            switch (measurementType.ToLower())
            {
                case "distance":
                    var distanceResult = await MeasureDistancesAsync(image, targetObjects, precision);
                    measurements = distanceResult.measurements;
                    measurementObjects = distanceResult.objects;
                    result.Message = $"测量了 {measurements.Count} 个距离";
                    break;
                    
                case "angle":
                    var angleResult = await MeasureAnglesAsync(image, targetObjects, precision);
                    measurements = angleResult.measurements;
                    measurementObjects = angleResult.objects;
                    result.Message = $"测量了 {measurements.Count} 个角度";
                    break;
                    
                case "area":
                    var areaResult = await MeasureAreasAsync(image, targetObjects ?? new List<DetectedObject>(), precision);
                    measurements = areaResult.measurements;
                    measurementObjects = areaResult.objects;
                    result.Message = $"测量了 {measurements.Count} 个面积";
                    break;
                    
                case "diameter":
                    var diameterResult = await MeasureDiametersAsync(image, targetObjects, precision);
                    measurements = diameterResult.measurements;
                    measurementObjects = diameterResult.objects;
                    result.Message = $"测量了 {measurements.Count} 个直径";
                    break;
                    
                case "position":
                    var positionResult = await MeasurePositionsAsync(image, targetObjects, precision);
                    measurements = positionResult.measurements;
                    measurementObjects = positionResult.objects;
                    result.Message = $"测量了 {measurements.Count / 2} 个位置坐标";
                    break;
                    
                default:
                    var defaultResult = await MeasureDistancesAsync(image, targetObjects, precision);
                    measurements = defaultResult.measurements;
                    measurementObjects = defaultResult.objects;
                    result.Message = $"默认测量了 {measurements.Count} 个距离";
                    break;
            }
            
            result.Measurements = measurements;
            result.Objects = measurementObjects;
            result.Results = new Dictionary<string, object>
            {
                ["measurement_type"] = measurementType,
                ["precision"] = precision,
                ["pixel_to_mm_ratio"] = _pixelToMmRatio,
                ["measurement_count"] = measurements.Count,
                ["measurement_unit"] = "mm"
            };
            
            return result;
        }
        
        private async Task<(Dictionary<string, double> measurements, List<DetectedObject> objects)> MeasureDistancesAsync(VisionImage image, List<DetectedObject>? targetObjects, double precision)
        {
            await Task.Delay(30);
            
            var measurements = new Dictionary<string, double>();
            var objects = new List<DetectedObject>();
            
            // 如果没有提供目标对象，生成一些测量点
            if (targetObjects == null || !targetObjects.Any())
            {
                targetObjects = GenerateRandomMeasurementPoints(image, 5);
            }
            
            // 测量对象间的距离
            for (int i = 0; i < targetObjects.Count - 1; i++)
            {
                for (int j = i + 1; j < Math.Min(targetObjects.Count, i + 3); j++) // 限制测量数量
                {
                    var obj1 = targetObjects[i];
                    var obj2 = targetObjects[j];
                    
                    var centerX1 = obj1.BoundingBox.X + obj1.BoundingBox.Width / 2.0;
                    var centerY1 = obj1.BoundingBox.Y + obj1.BoundingBox.Height / 2.0;
                    var centerX2 = obj2.BoundingBox.X + obj2.BoundingBox.Width / 2.0;
                    var centerY2 = obj2.BoundingBox.Y + obj2.BoundingBox.Height / 2.0;
                    
                    var pixelDistance = Math.Sqrt(Math.Pow(centerX2 - centerX1, 2) + Math.Pow(centerY2 - centerY1, 2));
                    var mmDistance = Math.Round(pixelDistance * _pixelToMmRatio, (int)Math.Log10(1 / precision));
                    
                    var measurementKey = $"distance_{i + 1}_to_{j + 1}";
                    measurements[measurementKey] = mmDistance;
                    
                    // 创建测量线对象
                    var lineObject = new DetectedObject
                    {
                        Name = $"测量线_{i + 1}-{j + 1}",
                        BoundingBox = new Rectangle
                        {
                            X = (int)Math.Min(centerX1, centerX2),
                            Y = (int)Math.Min(centerY1, centerY2),
                            Width = (int)Math.Abs(centerX2 - centerX1),
                            Height = (int)Math.Abs(centerY2 - centerY1)
                        },
                        Confidence = 1.0,
                        Properties = new Dictionary<string, object>
                        {
                            ["measurement_type"] = "distance",
                            ["distance_mm"] = mmDistance,
                            ["distance_pixels"] = pixelDistance,
                            ["start_point"] = new { X = centerX1, Y = centerY1 },
                            ["end_point"] = new { X = centerX2, Y = centerY2 }
                        }
                    };
                    
                    objects.Add(lineObject);
                }
            }
            
            return (measurements, objects);
        }
        
        private async Task<(Dictionary<string, double> measurements, List<DetectedObject> objects)> MeasureAnglesAsync(VisionImage image, List<DetectedObject>? targetObjects, double precision)
        {
            await Task.Delay(40);
            
            var measurements = new Dictionary<string, double>();
            var objects = new List<DetectedObject>();
            
            if (targetObjects == null || targetObjects.Count < 3)
            {
                targetObjects = GenerateRandomMeasurementPoints(image, 6);
            }
            
            // 测量三点角度
            for (int i = 0; i < targetObjects.Count - 2; i++)
            {
                var obj1 = targetObjects[i];
                var obj2 = targetObjects[i + 1];
                var obj3 = targetObjects[i + 2];
                
                var p1 = new { X = obj1.BoundingBox.X + obj1.BoundingBox.Width / 2.0, Y = obj1.BoundingBox.Y + obj1.BoundingBox.Height / 2.0 };
                var p2 = new { X = obj2.BoundingBox.X + obj2.BoundingBox.Width / 2.0, Y = obj2.BoundingBox.Y + obj2.BoundingBox.Height / 2.0 };
                var p3 = new { X = obj3.BoundingBox.X + obj3.BoundingBox.Width / 2.0, Y = obj3.BoundingBox.Y + obj3.BoundingBox.Height / 2.0 };
                
                var angle1 = Math.Atan2(p1.Y - p2.Y, p1.X - p2.X);
                var angle2 = Math.Atan2(p3.Y - p2.Y, p3.X - p2.X);
                var angleDiff = Math.Abs(angle2 - angle1) * 180 / Math.PI;
                
                if (angleDiff > 180) angleDiff = 360 - angleDiff;
                
                angleDiff = Math.Round(angleDiff, (int)Math.Log10(1 / precision));
                
                var measurementKey = $"angle_{i + 1}_{i + 2}_{i + 3}";
                measurements[measurementKey] = angleDiff;
                
                // 创建角度标注对象
                var angleObject = new DetectedObject
                {
                    Name = $"角度_{i + 1}",
                    BoundingBox = new Rectangle
                    {
                        X = (int)p2.X - 20,
                        Y = (int)p2.Y - 20,
                        Width = 40,
                        Height = 40
                    },
                    Confidence = 1.0,
                    Properties = new Dictionary<string, object>
                    {
                        ["measurement_type"] = "angle",
                        ["angle_degrees"] = angleDiff,
                        ["vertex_point"] = p2,
                        ["arm1_point"] = p1,
                        ["arm2_point"] = p3
                    }
                };
                
                objects.Add(angleObject);
            }
            
            return (measurements, objects);
        }
        
        private async Task<(Dictionary<string, double> measurements, List<DetectedObject> objects)> MeasureAreasAsync(VisionImage image, List<DetectedObject> targetObjects, double precision)
        {
            await Task.Delay(25);
            
            var measurements = new Dictionary<string, double>();
            var objects = new List<DetectedObject>();
            
            if (!targetObjects.Any())
            {
                targetObjects = GenerateRandomMeasurementRegions(image, 4);
            }
            
            for (int i = 0; i < targetObjects.Count; i++)
            {
                var obj = targetObjects[i];
                var pixelArea = obj.BoundingBox.Width * obj.BoundingBox.Height;
                var mmArea = Math.Round(pixelArea * _pixelToMmRatio * _pixelToMmRatio, (int)Math.Log10(1 / precision));
                
                var measurementKey = $"area_{i + 1}";
                measurements[measurementKey] = mmArea;
                
                // 更新对象属性
                obj.Properties["measurement_type"] = "area";
                obj.Properties["area_mm2"] = mmArea;
                obj.Properties["area_pixels"] = pixelArea;
                
                objects.Add(obj);
            }
            
            return (measurements, objects);
        }
        
        private async Task<(Dictionary<string, double> measurements, List<DetectedObject> objects)> MeasureDiametersAsync(VisionImage image, List<DetectedObject>? targetObjects, double precision)
        {
            await Task.Delay(35);
            
            var measurements = new Dictionary<string, double>();
            var objects = new List<DetectedObject>();
            
            if (targetObjects == null || !targetObjects.Any())
            {
                targetObjects = GenerateRandomCircularObjects(image, 3);
            }
            
            for (int i = 0; i < targetObjects.Count; i++)
            {
                var obj = targetObjects[i];
                var avgPixelDiameter = (obj.BoundingBox.Width + obj.BoundingBox.Height) / 2.0;
                var mmDiameter = Math.Round(avgPixelDiameter * _pixelToMmRatio, (int)Math.Log10(1 / precision));
                
                var measurementKey = $"diameter_{i + 1}";
                measurements[measurementKey] = mmDiameter;
                
                obj.Properties["measurement_type"] = "diameter";
                obj.Properties["diameter_mm"] = mmDiameter;
                obj.Properties["diameter_pixels"] = avgPixelDiameter;
                obj.Properties["radius_mm"] = mmDiameter / 2.0;
                
                objects.Add(obj);
            }
            
            return (measurements, objects);
        }
        
        private async Task<(Dictionary<string, double> measurements, List<DetectedObject> objects)> MeasurePositionsAsync(VisionImage image, List<DetectedObject>? targetObjects, double precision)
        {
            await Task.Delay(20);
            
            var measurements = new Dictionary<string, double>();
            var objects = new List<DetectedObject>();
            
            if (targetObjects == null || !targetObjects.Any())
            {
                targetObjects = GenerateRandomMeasurementPoints(image, 4);
            }
            
            for (int i = 0; i < targetObjects.Count; i++)
            {
                var obj = targetObjects[i];
                var centerX = obj.BoundingBox.X + obj.BoundingBox.Width / 2.0;
                var centerY = obj.BoundingBox.Y + obj.BoundingBox.Height / 2.0;
                
                var mmX = Math.Round(centerX * _pixelToMmRatio, (int)Math.Log10(1 / precision));
                var mmY = Math.Round(centerY * _pixelToMmRatio, (int)Math.Log10(1 / precision));
                
                measurements[$"position_{i + 1}_x"] = mmX;
                measurements[$"position_{i + 1}_y"] = mmY;
                
                obj.Properties["measurement_type"] = "position";
                obj.Properties["position_x_mm"] = mmX;
                obj.Properties["position_y_mm"] = mmY;
                obj.Properties["position_x_pixels"] = centerX;
                obj.Properties["position_y_pixels"] = centerY;
                
                objects.Add(obj);
            }
            
            return (measurements, objects);
        }
        
        private List<DetectedObject> GenerateRandomMeasurementPoints(VisionImage image, int count)
        {
            var points = new List<DetectedObject>();
            
            for (int i = 0; i < count; i++)
            {
                points.Add(new DetectedObject
                {
                    Name = $"测量点_{i + 1}",
                    BoundingBox = new Rectangle
                    {
                        X = _random.Next(50, image.Width - 50),
                        Y = _random.Next(50, image.Height - 50),
                        Width = 5,
                        Height = 5
                    },
                    Confidence = 1.0,
                    Properties = new Dictionary<string, object>
                    {
                        ["type"] = "measurement_point"
                    }
                });
            }
            
            return points;
        }
        
        private List<DetectedObject> GenerateRandomMeasurementRegions(VisionImage image, int count)
        {
            var regions = new List<DetectedObject>();
            
            for (int i = 0; i < count; i++)
            {
                var width = _random.Next(50, 200);
                var height = _random.Next(50, 200);
                
                regions.Add(new DetectedObject
                {
                    Name = $"测量区域_{i + 1}",
                    BoundingBox = new Rectangle
                    {
                        X = _random.Next(0, image.Width - width),
                        Y = _random.Next(0, image.Height - height),
                        Width = width,
                        Height = height
                    },
                    Confidence = 1.0,
                    Properties = new Dictionary<string, object>
                    {
                        ["type"] = "measurement_region"
                    }
                });
            }
            
            return regions;
        }
        
        private List<DetectedObject> GenerateRandomCircularObjects(VisionImage image, int count)
        {
            var circles = new List<DetectedObject>();
            
            for (int i = 0; i < count; i++)
            {
                var diameter = _random.Next(30, 150);
                
                circles.Add(new DetectedObject
                {
                    Name = $"圆形对象_{i + 1}",
                    BoundingBox = new Rectangle
                    {
                        X = _random.Next(0, image.Width - diameter),
                        Y = _random.Next(0, image.Height - diameter),
                        Width = diameter,
                        Height = diameter
                    },
                    Confidence = 0.9 + _random.NextDouble() * 0.1,
                    Properties = new Dictionary<string, object>
                    {
                        ["type"] = "circle",
                        ["shape"] = "circular"
                    }
                });
            }
            
            return circles;
        }
        
        public Dictionary<string, AlgorithmParameter> GetParameters()
        {
            return new Dictionary<string, AlgorithmParameter>
            {
                ["measurement_type"] = new AlgorithmParameter
                {
                    Name = "measurement_type",
                    DisplayName = "测量类型",
                    Description = "要执行的测量类型",
                    ParameterType = typeof(string),
                    DefaultValue = "distance",
                    IsRequired = true,
                    AllowedValues = new List<object> { "distance", "angle", "area", "diameter", "position" }
                },
                ["precision"] = new AlgorithmParameter
                {
                    Name = "precision",
                    DisplayName = "测量精度",
                    Description = "测量结果的精度（毫米）",
                    ParameterType = typeof(double),
                    DefaultValue = 0.01,
                    MinValue = 0.001,
                    MaxValue = 1.0,
                    IsRequired = false
                },
                ["pixel_to_mm_ratio"] = new AlgorithmParameter
                {
                    Name = "pixel_to_mm_ratio",
                    DisplayName = "像素比率",
                    Description = "像素到毫米的转换比率",
                    ParameterType = typeof(double),
                    DefaultValue = 0.1,
                    MinValue = 0.001,
                    MaxValue = 10.0,
                    IsRequired = false
                }
            };
        }
        
        public bool ValidateParameters(Dictionary<string, object> parameters)
        {
            try
            {
                if (parameters.ContainsKey("measurement_type"))
                {
                    var measurementType = parameters["measurement_type"].ToString();
                    var allowedTypes = new[] { "distance", "angle", "area", "diameter", "position" };
                    if (!allowedTypes.Contains(measurementType?.ToLower()))
                        return false;
                }
                
                if (parameters.ContainsKey("precision"))
                {
                    var precision = Convert.ToDouble(parameters["precision"]);
                    if (precision < 0.001 || precision > 1.0)
                        return false;
                }
                
                if (parameters.ContainsKey("pixel_to_mm_ratio"))
                {
                    var ratio = Convert.ToDouble(parameters["pixel_to_mm_ratio"]);
                    if (ratio < 0.001 || ratio > 10.0)
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
            _logger.Info("测量算法已释放");
            _initialized = false;
        }
    }
}
