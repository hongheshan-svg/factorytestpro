#!/bin/bash

# .NET 10.0 迁移验证脚本
# 用于验证所有项目文件已正确迁移到 .NET 10.0

echo "=========================================="
echo ".NET 10.0 迁移验证脚本"
echo "=========================================="
echo ""

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 计数器
TOTAL_CHECKS=0
PASSED_CHECKS=0
FAILED_CHECKS=0

# 检查函数
check_pass() {
    echo -e "${GREEN}✓${NC} $1"
    ((PASSED_CHECKS++))
    ((TOTAL_CHECKS++))
}

check_fail() {
    echo -e "${RED}✗${NC} $1"
    ((FAILED_CHECKS++))
    ((TOTAL_CHECKS++))
}

check_warn() {
    echo -e "${YELLOW}⚠${NC} $1"
    ((TOTAL_CHECKS++))
}

echo "1. 检查 .csproj 文件中的 TargetFramework..."
echo "-------------------------------------------"

# 检查所有 .csproj 文件
CSPROJ_FILES=(
    "UTF.HAL/UTF.HAL.csproj"
    "UTF.Logging/UTF.Logging.csproj"
    "UTF.Core/UTF.Core.csproj"
    "UTF.Configuration/UTF.Configuration.csproj"
    "UTF.Business/UTF.Business.csproj"
    "UTF.Reporting/UTF.Reporting.csproj"
    "UTF.Vision/UTF.Vision.csproj"
    "UTF.UI/UTF.UI.csproj"
)

for file in "${CSPROJ_FILES[@]}"; do
    if [ -f "$file" ]; then
        if [ "$file" == "UTF.UI/UTF.UI.csproj" ]; then
            # UTF.UI 应该是 net10.0-windows
            if grep -q "<TargetFramework>net10.0-windows</TargetFramework>" "$file"; then
                check_pass "$file: net10.0-windows"
            else
                check_fail "$file: 未找到 net10.0-windows"
            fi
        else
            # 其他项目应该是 net10.0
            if grep -q "<TargetFramework>net10.0</TargetFramework>" "$file"; then
                check_pass "$file: net10.0"
            else
                check_fail "$file: 未找到 net10.0"
            fi
        fi

        # 检查是否还有 net9.0 残留
        if grep -q "net9.0" "$file"; then
            check_fail "$file: 仍包含 net9.0 引用"
        fi
    else
        check_fail "$file: 文件不存在"
    fi
done

echo ""
echo "2. 检查 NuGet 包版本..."
echo "-------------------------------------------"

# 检查 System.IO.Ports
if grep -q 'System.IO.Ports.*Version="10.0.0"' "UTF.HAL/UTF.HAL.csproj"; then
    check_pass "System.IO.Ports: 10.0.0"
else
    check_fail "System.IO.Ports: 版本不是 10.0.0"
fi

# 检查 System.Drawing.Common
if grep -q 'System.Drawing.Common.*Version="10.0.0"' "UTF.Reporting/UTF.Reporting.csproj"; then
    check_pass "System.Drawing.Common: 10.0.0"
else
    check_fail "System.Drawing.Common: 版本不是 10.0.0"
fi

echo ""
echo "3. 检查 CLAUDE.md 文档..."
echo "-------------------------------------------"

if [ -f "CLAUDE.md" ]; then
    if grep -q "\.NET 10\.0" "CLAUDE.md"; then
        check_pass "CLAUDE.md: 包含 .NET 10.0 引用"
    else
        check_fail "CLAUDE.md: 未找到 .NET 10.0 引用"
    fi

    if grep -q "net10\.0-windows" "CLAUDE.md"; then
        check_pass "CLAUDE.md: 包含 net10.0-windows 路径"
    else
        check_fail "CLAUDE.md: 未找到 net10.0-windows 路径"
    fi

    if grep -q "net9\.0" "CLAUDE.md"; then
        check_fail "CLAUDE.md: 仍包含 net9.0 引用"
    fi
else
    check_fail "CLAUDE.md: 文件不存在"
fi

echo ""
echo "4. 检查旧的编译输出..."
echo "-------------------------------------------"

OLD_DIRS_FOUND=0
for dir in UTF.*/bin/Debug/net9.0* UTF.*/bin/Release/net9.0*; do
    if [ -d "$dir" ]; then
        check_warn "发现旧的编译输出: $dir (建议清理)"
        ((OLD_DIRS_FOUND++))
    fi
done

if [ $OLD_DIRS_FOUND -eq 0 ]; then
    check_pass "未发现旧的 net9.0 编译输出"
fi

echo ""
echo "5. 检查 obj 目录中的残留..."
echo "-------------------------------------------"

OLD_OBJ_FOUND=0
for dir in UTF.*/obj/Debug/net9.0* UTF.*/obj/Release/net9.0*; do
    if [ -d "$dir" ]; then
        ((OLD_OBJ_FOUND++))
    fi
done

if [ $OLD_OBJ_FOUND -gt 0 ]; then
    check_warn "发现 $OLD_OBJ_FOUND 个旧的 obj 目录 (建议运行 dotnet clean)"
else
    check_pass "未发现旧的 obj 目录"
fi

echo ""
echo "=========================================="
echo "验证结果汇总"
echo "=========================================="
echo "总检查项: $TOTAL_CHECKS"
echo -e "${GREEN}通过: $PASSED_CHECKS${NC}"
echo -e "${RED}失败: $FAILED_CHECKS${NC}"
echo ""

if [ $FAILED_CHECKS -eq 0 ]; then
    echo -e "${GREEN}✓ 迁移验证通过！${NC}"
    echo ""
    echo "建议的后续步骤:"
    echo "1. 运行 'dotnet clean' 清理旧的编译输出"
    echo "2. 运行 'dotnet restore' 恢复 NuGet 包"
    echo "3. 运行 'dotnet build' 编译项目"
    echo "4. 测试应用程序功能"
    exit 0
else
    echo -e "${RED}✗ 迁移验证失败！${NC}"
    echo ""
    echo "请检查上述失败项并修复后重新运行此脚本。"
    exit 1
fi
