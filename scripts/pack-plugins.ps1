param(
    [Parameter(Mandatory = $true)]
    [string]$SolutionDir,

    [Parameter(Mandatory = $true)]
    [string]$OutDir,

    [Parameter(Mandatory = $false)]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseDir,
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    $normalized = $PathValue.Trim().Trim('"')

    if ([System.IO.Path]::IsPathRooted($normalized)) {
        return [System.IO.Path]::GetFullPath($normalized)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BaseDir $normalized))
}

$solutionPath = Resolve-AbsolutePath -BaseDir (Get-Location).Path -PathValue $SolutionDir
$outputPath = Resolve-AbsolutePath -BaseDir (Get-Location).Path -PathValue $OutDir
$pluginsRoot = Join-Path $solutionPath "plugins"

if (-not (Test-Path $pluginsRoot)) {
    Write-Host "[PluginPack] No plugins directory found: $pluginsRoot"
    exit 0
}

$manifestFiles = Get-ChildItem -Path $pluginsRoot -Recurse -File -Filter "plugin.manifest.json"
if (-not $manifestFiles) {
    Write-Host "[PluginPack] No plugin manifests found under $pluginsRoot"
    exit 0
}

$pluginProjectDirs = Get-ChildItem -Path $solutionPath -Directory -Filter "UTF.Plugins.*"

foreach ($manifestFile in $manifestFiles) {
    try {
        $manifest = Get-Content -Path $manifestFile.FullName -Raw | ConvertFrom-Json
    }
    catch {
        Write-Warning "[PluginPack] Invalid manifest JSON: $($manifestFile.FullName) - $($_.Exception.Message)"
        continue
    }

    $entryAssembly = [string]$manifest.entryAssembly
    if ([string]::IsNullOrWhiteSpace($entryAssembly)) {
        Write-Warning "[PluginPack] Missing entryAssembly: $($manifestFile.FullName)"
        continue
    }

    $relativePluginDir = $manifestFile.DirectoryName.Substring($pluginsRoot.Length).TrimStart('\', '/')
    $destinationDir = Join-Path (Join-Path $outputPath "plugins") $relativePluginDir
    New-Item -Path $destinationDir -ItemType Directory -Force | Out-Null

    $assemblySourceFile = $null
    foreach ($pluginProjectDir in $pluginProjectDirs) {
        $candidateRoot = Join-Path $pluginProjectDir.FullName ("bin\" + $Configuration)
        if (-not (Test-Path $candidateRoot)) {
            continue
        }

        $candidateAssembly = Get-ChildItem -Path $candidateRoot -Recurse -File -Filter $entryAssembly |
            Sort-Object FullName |
            Select-Object -First 1

        if ($candidateAssembly) {
            $assemblySourceFile = $candidateAssembly
            break
        }
    }

    if (-not $assemblySourceFile) {
        Write-Warning "[PluginPack] Could not find entry assembly '$entryAssembly' for manifest: $($manifestFile.FullName)"
        continue
    }

    $assemblyOutputDir = Split-Path -Path $assemblySourceFile.FullName -Parent
    $entryAssemblyName = $assemblySourceFile.Name

    # 仅复制插件自己的程序集 + 插件私有依赖（不以 UTF./System./Microsoft. 开头）。
    # 共享 UTF.*.dll 由宿主统一提供，不打包进插件目录，避免版本冲突。
    $pluginProjectName = $entryAssemblyName -replace '\.dll$', ''
    $sharedPrefixes = @('UTF.', 'System.', 'Microsoft.')

    $filesToCopy = Get-ChildItem -Path $assemblyOutputDir -File | Where-Object {
        $name = $_.Name

        # 清单单独复制（已复制），跳过
        if ($name -ieq 'plugin.manifest.json') { return $false }

        # 插件主程序集始终复制
        if ($name -ieq $entryAssemblyName) { return $true }

        # 其余 .dll/.exe：跳过共享框架/UTF 共享程序集，仅保留插件私有依赖
        $ext = $_.Extension
        if ($ext -ieq '.dll' -or $ext -ieq '.exe') {
            foreach ($prefix in $sharedPrefixes) {
                if ($name.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                    return $false
                }
            }
            return $true
        }

        # 非程序集文件（.json deps/.pdb 等）按原行为复制，但 .deps.json 仅在属于本插件时保留
        return $true
    }

    foreach ($file in $filesToCopy) {
        Copy-Item -Path $file.FullName -Destination (Join-Path $destinationDir $file.Name) -Force
    }

    $entryAssemblyPath = Join-Path $destinationDir $entryAssemblyName
    # 用 .NET SHA256 直接计算哈希，避免 Get-FileHash cmdlet 在某些 PowerShell 环境不可用的问题
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($entryAssemblyPath)
        try {
            $hashBytes = $sha.ComputeHash($stream)
        } finally {
            $stream.Dispose()
        }
    } finally {
        $sha.Dispose()
    }
    $assemblyHash = [BitConverter]::ToString($hashBytes).Replace("-", "").ToLowerInvariant()
    $manifest | Add-Member -NotePropertyName "sha256" -NotePropertyValue $assemblyHash -Force
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $destinationDir "plugin.manifest.json") -Encoding UTF8

    Write-Host "[PluginPack] Packed plugin '$($manifest.pluginId)' from '$assemblyOutputDir' to '$destinationDir' (private deps only)"
}

Write-Host "[PluginPack] Done."
