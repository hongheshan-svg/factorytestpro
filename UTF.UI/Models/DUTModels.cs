using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;

namespace UTF.UI.Models
{
    /// <summary>
    /// DUT监控台数据模型
    /// </summary>
    public class DUTMonitorItem : INotifyPropertyChanged
    {
        private string _dutId = "";
        private string _dutName = "";
        private string _deviceType = "";
        private string _serialNumber = "";
        private DUTMonitorStatus _overallStatus = DUTMonitorStatus.Idle;
        private string _currentStepText = "";
        private DateTime? _startTime;
        private DateTime? _endTime;
        private ObservableCollection<DUTTestStep> _testSteps = new();
        private ObservableCollection<DUTLogItem> _recentLogs = new();
        private ObservableCollection<string> _logs = new();
        private string _latestLog = "";
        
        // 批量更新标志
        private bool _isUpdating = false;
        private HashSet<string> _pendingUpdates = new();

        // 日志批量更新
        private List<DUTLogItem> _pendingLogs = new();
        private DateTime _lastLogFlush = DateTime.Now;

        public string DutId
        {
            get => _dutId;
            set { _dutId = value; OnPropertyChanged(nameof(DutId)); }
        }

        public string DutName
        {
            get => _dutName;
            set { _dutName = value; OnPropertyChanged(nameof(DutName)); }
        }

        public string DeviceType
        {
            get => _deviceType;
            set { _deviceType = value; OnPropertyChanged(nameof(DeviceType)); }
        }

        public string SerialNumber
        {
            get => _serialNumber;
            set { _serialNumber = value; OnPropertyChanged(nameof(SerialNumber)); }
        }

        public DUTMonitorStatus OverallStatus
        {
            get => _overallStatus;
            set
            {
                _overallStatus = value;
                OnPropertyChanged(nameof(OverallStatus));
                OnPropertyChanged(nameof(OverallStatusText));
                OnPropertyChanged(nameof(OverallResultText));
                OnPropertyChanged(nameof(OverallStatusBrush));
            }
        }

        public string CurrentStepText
        {
            get => _currentStepText;
            set { _currentStepText = value; OnPropertyChanged(nameof(CurrentStepText)); }
        }

        public DateTime? StartTime
        {
            get => _startTime;
            set 
            { 
                _startTime = value; 
                OnPropertyChanged(nameof(StartTime)); 
                OnPropertyChanged(nameof(Duration));
            }
        }

        public DateTime? EndTime
        {
            get => _endTime;
            set 
            { 
                _endTime = value; 
                OnPropertyChanged(nameof(EndTime)); 
                OnPropertyChanged(nameof(Duration));
            }
        }

        public ObservableCollection<DUTTestStep> TestSteps
        {
            get => _testSteps;
            set { _testSteps = value; OnPropertyChanged(nameof(TestSteps)); }
        }

        public ObservableCollection<DUTLogItem> RecentLogs
        {
            get => _recentLogs;
            set { _recentLogs = value; OnPropertyChanged(nameof(RecentLogs)); }
        }

        public ObservableCollection<string> Logs
        {
            get => _logs;
            set { _logs = value; OnPropertyChanged(nameof(Logs)); }
        }

        public string LatestLog
        {
            get => _latestLog;
            set { _latestLog = value; OnPropertyChanged(nameof(LatestLog)); }
        }

        // UI绑定属性
        /// <summary>
        /// 测试持续时间
        /// </summary>
        public string Duration
        {
            get
            {
                if (StartTime.HasValue)
                {
                    var endTime = EndTime ?? DateTime.Now;
                    var elapsed = endTime - StartTime.Value;
                    return $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
                }
                return "00:00:00";
            }
        }
        public string OverallStatusText => OverallStatus switch
        {
            DUTMonitorStatus.Idle => "待机",
            DUTMonitorStatus.Running => "测试中",
            DUTMonitorStatus.Passed => "PASS",
            DUTMonitorStatus.Failed => "FAIL",
            DUTMonitorStatus.Error => "错误",
            DUTMonitorStatus.Timeout => "超时",
            _ => "未知"
        };

        /// <summary>
        /// 综合结果文本，用于区分单个测试项的PASS/FAIL
        /// </summary>
        public string OverallResultText => OverallStatus switch
        {
            DUTMonitorStatus.Idle => "⏸️ 待机中",
            DUTMonitorStatus.Running => "🔄 测试中",
            DUTMonitorStatus.Passed => "✅ 合格",
            DUTMonitorStatus.Failed => "❌ 不合格",
            DUTMonitorStatus.Error => "⚠️ 异常",
            DUTMonitorStatus.Timeout => "⏱️ 超时",
            _ => "❓ 未知"
        };

        public Brush OverallStatusBrush => OverallStatus switch
        {
            DUTMonitorStatus.Idle => new SolidColorBrush(Color.FromRgb(108, 117, 125)),
            DUTMonitorStatus.Running => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            DUTMonitorStatus.Passed => new SolidColorBrush(Color.FromRgb(40, 167, 69)),
            DUTMonitorStatus.Failed => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
            DUTMonitorStatus.Error => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
            DUTMonitorStatus.Timeout => new SolidColorBrush(Color.FromRgb(255, 87, 34)),
            _ => new SolidColorBrush(Color.FromRgb(108, 117, 125))
        };

        public string TestDuration
        {
            get
            {
                if (StartTime == null) return "";
                var endTime = EndTime ?? DateTime.Now;
                var duration = endTime - StartTime.Value;
                return $"{duration.TotalSeconds:F1}s";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (_isUpdating)
            {
                _pendingUpdates.Add(propertyName);
            }
            else
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        
        /// <summary>
        /// 开始批量更新（暂停PropertyChanged通知）
        /// </summary>
        public void BeginUpdate()
        {
            _isUpdating = true;
            _pendingUpdates.Clear();
        }
        
        /// <summary>
        /// 结束批量更新（批量触发PropertyChanged通知）
        /// </summary>
        public void EndUpdate()
        {
            _isUpdating = false;
            foreach (var propertyName in _pendingUpdates)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            _pendingUpdates.Clear();
        }

        public void AddLog(string message, UTF.Logging.LogLevel level = UTF.Logging.LogLevel.Info)
        {
            var logItem = new DUTLogItem
            {
                DisplayText = $"{DateTime.Now:HH:mm:ss} - {message}",
                Level = level
            };
            
            // 默认使用延迟更新模式，减少UI刷新频率
            _pendingLogs.Add(logItem);
            
            // 如果距离上次刷新超过200ms，自动刷新
            if ((DateTime.Now - _lastLogFlush).TotalMilliseconds > 200)
            {
                FlushPendingLogs();
            }
        }
        
        /// <summary>
        /// 刷新待处理的日志到UI
        /// </summary>
        public void FlushPendingLogs()
        {
            if (_pendingLogs.Count == 0) return;
            
            // 批量添加所有待处理的日志
            foreach (var logItem in _pendingLogs.TakeLast(5))
            {
                RecentLogs.Insert(0, logItem);
            }
            
            // 限制日志数量
            while (RecentLogs.Count > 5)
            {
                RecentLogs.RemoveAt(RecentLogs.Count - 1);
            }
            
            _pendingLogs.Clear();
            _lastLogFlush = DateTime.Now;
        }
        
        /// <summary>
        /// 开始批量日志更新
        /// </summary>
        public void BeginLogUpdate()
        {
            _pendingLogs.Clear();
        }

        /// <summary>
        /// 结束批量日志更新，一次性刷新所有日志
        /// </summary>
        public void EndLogUpdate()
        {
            // 批量添加所有待处理的日志
            foreach (var logItem in _pendingLogs)
            {
                RecentLogs.Insert(0, logItem);
            }
            
            // 限制日志数量
            while (RecentLogs.Count > 5)
            {
                RecentLogs.RemoveAt(RecentLogs.Count - 1);
            }
            
            _pendingLogs.Clear();
        }
    }

    /// <summary>
    /// DUT测试步骤
    /// </summary>
    public class DUTTestStep : INotifyPropertyChanged
    {
        private string _stepId = "";
        private string _stepName = "";
        private int _order;
        private DUTMonitorStepStatus _status = DUTMonitorStepStatus.Pending;
        private DateTime? _startTime;
        private DateTime? _endTime;
        private string _errorMessage = "";
        private Dictionary<string, object> _parameters = new();
        private Dictionary<string, object> _results = new();

        public string StepId
        {
            get => _stepId;
            set { _stepId = value; OnPropertyChanged(nameof(StepId)); }
        }

        public string StepName
        {
            get => _stepName;
            set { _stepName = value; OnPropertyChanged(nameof(StepName)); }
        }

        public int Order
        {
            get => _order;
            set { _order = value; OnPropertyChanged(nameof(Order)); }
        }

        public DUTMonitorStepStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBrush));
            }
        }

        public DateTime? StartTime
        {
            get => _startTime;
            set
            {
                _startTime = value;
                OnPropertyChanged(nameof(StartTime));
                OnPropertyChanged(nameof(Duration));
            }
        }

        public DateTime? EndTime
        {
            get => _endTime;
            set
            {
                _endTime = value;
                OnPropertyChanged(nameof(EndTime));
                OnPropertyChanged(nameof(Duration));
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(nameof(ErrorMessage)); }
        }

        public Dictionary<string, object> Parameters
        {
            get => _parameters;
            set { _parameters = value; OnPropertyChanged(nameof(Parameters)); }
        }

        public Dictionary<string, object> Results
        {
            get => _results;
            set { _results = value; OnPropertyChanged(nameof(Results)); }
        }

        // UI绑定属性 - 使用更小的符号区分单个测试步骤
        public string StatusText => Status switch
        {
            DUTMonitorStepStatus.Pending => "⏳",
            DUTMonitorStepStatus.Running => "▶️",
            DUTMonitorStepStatus.Passed => "✓",
            DUTMonitorStepStatus.Failed => "✗",
            DUTMonitorStepStatus.Skipped => "⏭️",
            DUTMonitorStepStatus.Error => "⚠️",
            _ => "❓"
        };

        public Brush StatusBrush => Status switch
        {
            DUTMonitorStepStatus.Pending => new SolidColorBrush(Color.FromRgb(108, 117, 125)),
            DUTMonitorStepStatus.Running => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            DUTMonitorStepStatus.Passed => new SolidColorBrush(Color.FromRgb(40, 167, 69)),
            DUTMonitorStepStatus.Failed => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
            DUTMonitorStepStatus.Skipped => new SolidColorBrush(Color.FromRgb(108, 117, 125)),
            DUTMonitorStepStatus.Error => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
            _ => new SolidColorBrush(Color.FromRgb(108, 117, 125))
        };

        public string Duration
        {
            get
            {
                if (StartTime == null) return "";
                var endTime = EndTime ?? (Status == DUTMonitorStepStatus.Running ? DateTime.Now : StartTime.Value);
                var duration = endTime - StartTime.Value;
                return $"{duration.TotalSeconds:F1}s";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// DUT监控台测试状态枚举
    /// </summary>
    public enum DUTMonitorStatus
    {
        Idle,       // 待机
        Running,    // 测试中
        Passed,     // 通过
        Failed,     // 失败
        Error,      // 错误
        Timeout     // 超时
    }

    /// <summary>
    /// DUT监控台测试步骤状态枚举
    /// </summary>
    public enum DUTMonitorStepStatus
    {
        Pending,    // 等待
        Running,    // 执行中
        Passed,     // 通过
        Failed,     // 失败
        Skipped,    // 跳过
        Error       // 错误
    }

    /// <summary>
    /// DUT日志项
    /// </summary>
    public class DUTLogItem
    {
        public string DisplayText { get; set; } = "";
        public UTF.Logging.LogLevel Level { get; set; } = UTF.Logging.LogLevel.Info;
    }
}
