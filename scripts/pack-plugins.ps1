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

    Copy-Item -Path $manifestFile.FullName -Destination (Join-Path $destinationDir "plugin.manifest.json") -Force

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
    $filesToCopy = Get-ChildItem -Path $assemblyOutputDir -File

    foreach ($file in $filesToCopy) {
        Copy-Item -Path $file.FullName -Destination (Join-Path $destinationDir $file.Name) -Force
    }

    Write-Host "[PluginPack] Packed plugin '$($manifest.pluginId)' from '$assemblyOutputDir' to '$destinationDir'"
}

Write-Host "[PluginPack] Done."
