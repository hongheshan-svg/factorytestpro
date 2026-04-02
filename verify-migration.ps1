# .NET 10.0 迁移验证脚本 (PowerShell)
# 用于验证所有项目文件已正确迁移到 .NET 10.0

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ".NET 10.0 迁移验证脚本" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# 计数器
$script:TotalChecks = 0
$script:PassedChecks = 0
$script:FailedChecks = 0

# 检查函数
function Check-Pass {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
    $script:PassedChecks++
    $script:TotalChecks++
}

function Check-Fail {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
    $script:FailedChecks++
    $script:TotalChecks++
}

function Check-Warn {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor Yellow
    $script:TotalChecks++
}

Write-Host "1. 检查 .csproj 文件中的 TargetFramework..." -ForegroundColor White
Write-Host "-------------------------------------------" -ForegroundColor Gray

# 检查所有 .csproj 文件
$csprojFiles = @(
    "UTF.HAL\UTF.HAL.csproj",
    "UTF.Logging\UTF.Logging.csproj",
    "UTF.Core\UTF.Core.csproj",
    "UTF.Configuration\UTF.Configuration.csproj",
    "UTF.Business\UTF.Business.csproj",
    "UTF.Reporting\UTF.Reporting.csproj",
    "UTF.Vision\UTF.Vision.csproj",
    "UTF.UI\UTF.UI.csproj"
)

foreach ($file in $csprojFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw

        if ($file -eq "UTF.UI\UTF.UI.csproj") {
            # UTF.UI 应该是 net10.0-windows
            if ($content -match "<TargetFramework>net10\.0-windows</TargetFramework>") {
                Check-Pass "$file`: net10.0-windows"
            } else {
                Check-Fail "$file`: 未找到 net10.0-windows"
            }
        } else {
            # 其他项目应该是 net10.0
            if ($content -match "<TargetFramework>net10\.0</TargetFramework>") {
                Check-Pass "$file`: net10.0"
            } else {
                Check-Fail "$file`: 未找到 net10.0"
            }
        }

        # 检查是否还有 net9.0 残留
        if ($content -match "net9\.0") {
            Check-Fail "$file`: 仍包含 net9.0 引用"
        }
    } else {
        Check-Fail "$file`: 文件不存在"
    }
}

Write-Host ""
Write-Host "2. 检查 NuGet 包版本..." -ForegroundColor White
Write-Host "-------------------------------------------" -ForegroundColor Gray

# 检查 System.IO.Ports
$halContent = Get-Content "UTF.HAL\UTF.HAL.csproj" -Raw
if ($halContent -match 'System\.IO\.Ports.*Version="10\.0\.0"') {
    Check-Pass "System.IO.Ports: 10.0.0"
} else {
    Check-Fail "System.IO.Ports: 版本不是 10.0.0"
}

# 检查 System.Drawing.Common
$reportingContent = Get-Content "UTF.Reporting\UTF.Reporting.csproj" -Raw
if ($reportingContent -match 'System\.Drawing\.Common.*Version="10\.0\.0"') {
    Check-Pass "System.Drawing.Common: 10.0.0"
} else {
    Check-Fail "System.Drawing.Common: 版本不是 10.0.0"
}

Write-Host ""
Write-Host "3. 检查 CLAUDE.md 文档..." -ForegroundColor White
Write-Host "-------------------------------------------" -ForegroundColor Gray

if (Test-Path "CLAUDE.md") {
    $claudeContent = Get-Content "CLAUDE.md" -Raw

    if ($claudeContent -match "\.NET 10\.0") {
        Check-Pass "CLAUDE.md: 包含 .NET 10.0 引用"
    } else {
        Check-Fail "CLAUDE.md: 未找到 .NET 10.0 引用"
    }

    if ($claudeContent -match "net10\.0-windows") {
        Check-Pass "CLAUDE.md: 包含 net10.0-windows 路径"
    } else {
        Check-Fail "CLAUDE.md: 未找到 net10.0-windows 路径"
    }

    if ($claudeContent -match "net9\.0") {
        Check-Fail "CLAUDE.md: 仍包含 net9.0 引用"
    }
} else {
    Check-Fail "CLAUDE.md: 文件不存在"
}

Write-Host ""
Write-Host "4. 检查旧的编译输出..." -ForegroundColor White
Write-Host "-------------------------------------------" -ForegroundColor Gray

$oldDirs = Get-ChildItem -Path "UTF.*\bin\*\net9.0*" -Directory -ErrorAction SilentlyContinue
if ($oldDirs) {
    foreach ($dir in $oldDirs) {
        Check-Warn "发现旧的编译输出: $($dir.FullName) (建议清理)"
    }
} else {
    Check-Pass "未发现旧的 net9.0 编译输出"
}

Write-Host ""
Write-Host "5. 检查 obj 目录中的残留..." -ForegroundColor White
Write-Host "-------------------------------------------" -ForegroundColor Gray

$oldObjDirs = Get-ChildItem -Path "UTF.*\obj\*\net9.0*" -Directory -ErrorAction SilentlyContinue
if ($oldObjDirs) {
    Check-Warn "发现 $($oldObjDirs.Count) 个旧的 obj 目录 (建议运行 dotnet clean)"
} else {
    Check-Pass "未发现旧的 obj 目录"
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "验证结果汇总" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "总检查项: $TotalChecks"
Write-Host "通过: $PassedChecks" -ForegroundColor Green
Write-Host "失败: $FailedChecks" -ForegroundColor Red
Write-Host ""

if ($FailedChecks -eq 0) {
    Write-Host "✓ 迁移验证通过！" -ForegroundColor Green
    Write-Host ""
    Write-Host "建议的后续步骤:"
    Write-Host "1. 运行 'dotnet clean' 清理旧的编译输出"
    Write-Host "2. 运行 'dotnet restore' 恢复 NuGet 包"
    Write-Host "3. 运行 'dotnet build' 编译项目"
    Write-Host "4. 测试应用程序功能"
    exit 0
} else {
    Write-Host "✗ 迁移验证失败！" -ForegroundColor Red
    Write-Host ""
    Write-Host "请检查上述失败项并修复后重新运行此脚本。"
    exit 1
}
