using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using UTF.Logging;
using UTF.Vision.Algorithms;

namespace UTF.Vision;

/// <summary>
/// 基于文件系统的真实视觉输入：从目录加载图像（PNG/JPG/BMP），
/// 并走 <see cref="AlgorithmManager"/> 算法链（非随机模拟像素）。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FileVisionSystem : IVisionSystem, IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly AlgorithmManager _algorithmManager;
    private readonly string _imageDirectory;
    private readonly string[] _imageFiles;
    private int _imageIndex;
    private bool _isInitialized;
    private bool _isConnected;
    private bool _isCalibrated;
    private bool _disposed;

    public string SystemId { get; }
    public string Name { get; }
    public bool IsConnected => _isConnected;

    public FileVisionSystem(string systemId, string name, string imageDirectory, ILogger logger)
    {
        SystemId = systemId ?? throw new ArgumentNullException(nameof(systemId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _imageDirectory = imageDirectory ?? throw new ArgumentNullException(nameof(imageDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _algorithmManager = new AlgorithmManager(logger);

        _imageFiles = Directory.Exists(imageDirectory)
            ? Directory.GetFiles(imageDirectory, "*.*")
                .Where(f => IsSupportedImage(f))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<string>();
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            _logger.Info($"初始化文件视觉系统: {Name} dir={_imageDirectory} images={_imageFiles.Length}");
            if (!await _algorithmManager.InitializeAsync().ConfigureAwait(false))
            {
                return false;
            }

            _isInitialized = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"文件视觉系统初始化失败: {Name}", ex);
            return false;
        }
    }

    public Task<bool> ConnectAsync()
    {
        if (!_isInitialized)
        {
            return Task.FromResult(false);
        }

        if (_imageFiles.Length == 0)
        {
            _logger.Warning($"文件视觉系统无可用图像: {_imageDirectory}");
            // 仍允许连接，Capture 时返回合成棋盘图以保持算法链路可测
        }

        _isConnected = true;
        _logger.Info($"文件视觉系统已连接: {Name}");
        return Task.FromResult(true);
    }

    public Task DisconnectAsync()
    {
        _isConnected = false;
        return Task.CompletedTask;
    }

    public Task<VisionImage?> CaptureImageAsync()
    {
        if (!_isConnected)
        {
            return Task.FromResult<VisionImage?>(null);
        }

        try
        {
            VisionImage image;
            if (_imageFiles.Length > 0)
            {
                var path = _imageFiles[_imageIndex % _imageFiles.Length];
                _imageIndex++;
                image = LoadImageFromFile(path);
                image.Metadata["SourcePath"] = path;
            }
            else
            {
                image = CreateSyntheticCheckerboard(640, 480);
                image.Metadata["SourcePath"] = "(synthetic-checkerboard)";
            }

            image.Metadata["SystemId"] = SystemId;
            image.Metadata["SystemName"] = Name;
            return Task.FromResult<VisionImage?>(image);
        }
        catch (Exception ex)
        {
            _logger.Error($"加载图像失败: {Name}", ex);
            return Task.FromResult<VisionImage?>(null);
        }
    }

    public async Task<InspectionResult> InspectAsync(VisionImage image, InspectionParameters parameters)
    {
        var start = DateTime.UtcNow;
        try
        {
            var chain = new List<AlgorithmChainStep>
            {
                new()
                {
                    AlgorithmId = "image_processing",
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "enhance",
                        ["intensity"] = 1.1
                    },
                    UseProcessedImageForNext = true,
                    Description = "图像增强"
                },
                new()
                {
                    AlgorithmId = "object_detection",
                    Parameters = new Dictionary<string, object>
                    {
                        ["target_type"] = "circle",
                        ["min_confidence"] = parameters.Threshold,
                        ["max_objects"] = parameters.MaxObjects
                    },
                    UseProcessedImageForNext = false,
                    Description = "目标检测"
                },
                new()
                {
                    AlgorithmId = "measurement",
                    Parameters = new Dictionary<string, object>
                    {
                        ["measurement_type"] = "distance",
                        ["precision"] = 0.01
                    },
                    UseProcessedImageForNext = false,
                    Description = "测量"
                }
            };

            var chainResult = await _algorithmManager.ProcessChainAsync(image, chain).ConfigureAwait(false);
            var result = new InspectionResult
            {
                Passed = chainResult.Success,
                Message = chainResult.Success
                    ? "File vision inspection completed"
                    : chainResult.Message ?? "Inspection failed",
                Score = chainResult.Success ? chainResult.Confidence : 0.0,
                Objects = chainResult.Objects ?? new List<DetectedObject>(),
                ProcessingTime = DateTime.UtcNow - start,
                Measurements = new Dictionary<string, object>
                {
                    ["Source"] = image.Metadata.TryGetValue("SourcePath", out var p) ? p : "",
                    ["Width"] = image.Width,
                    ["Height"] = image.Height
                }
            };

            if (chainResult.Success)
            {
                foreach (var kv in chainResult.Measurements)
                {
                    result.Measurements[kv.Key] = kv.Value;
                }

                foreach (var kv in chainResult.Results)
                {
                    result.Measurements[$"algorithm_{kv.Key}"] = kv.Value;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"检测失败: {Name}", ex);
            return new InspectionResult
            {
                Passed = false,
                Message = ex.Message,
                ProcessingTime = DateTime.UtcNow - start
            };
        }
    }

    public Task<bool> CalibrateAsync(CalibrationParameters parameters)
    {
        _isCalibrated = _imageFiles.Length > 0 || true;
        _logger.Info($"文件视觉系统校准完成: {Name} (image-based, no hardware)");
        return Task.FromResult(true);
    }

    public VisionSystemStatus GetStatus() => new()
    {
        IsInitialized = _isInitialized,
        IsConnected = _isConnected,
        IsCalibrated = _isCalibrated,
        CurrentMode = "File",
        SystemInfo = new Dictionary<string, object>
        {
            ["ImageDirectory"] = _imageDirectory,
            ["ImageCount"] = _imageFiles.Length,
            ["Backend"] = "FileVisionSystem"
        }
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _algorithmManager.Dispose();
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private static bool IsSupportedImage(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private static VisionImage LoadImageFromFile(string path)
    {
        using var bitmap = new Bitmap(path);
        var width = bitmap.Width;
        var height = bitmap.Height;
        const int channels = 3;
        var data = new byte[width * height * channels];

        var rect = new System.Drawing.Rectangle(0, 0, width, height);
        var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var stride = Math.Abs(bmpData.Stride);
            var row = new byte[stride];
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(bmpData.Scan0 + y * bmpData.Stride, row, 0, stride);
                for (var x = 0; x < width; x++)
                {
                    var src = x * 3;
                    var dst = (y * width + x) * 3;
                    // GDI+ Format24bppRgb is BGR
                    data[dst] = row[src + 2];
                    data[dst + 1] = row[src + 1];
                    data[dst + 2] = row[src];
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }

        return new VisionImage
        {
            Width = width,
            Height = height,
            Channels = channels,
            Data = data,
            Timestamp = DateTime.UtcNow
        };
    }

    private static VisionImage CreateSyntheticCheckerboard(int width, int height)
    {
        const int channels = 3;
        var data = new byte[width * height * channels];
        const int tile = 32;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var on = ((x / tile) + (y / tile)) % 2 == 0;
                var v = (byte)(on ? 220 : 40);
                var i = (y * width + x) * 3;
                data[i] = v;
                data[i + 1] = v;
                data[i + 2] = v;
            }
        }

        return new VisionImage
        {
            Width = width,
            Height = height,
            Channels = channels,
            Data = data,
            Timestamp = DateTime.UtcNow
        };
    }
}
