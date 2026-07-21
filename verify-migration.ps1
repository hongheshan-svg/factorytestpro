[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$failures = [System.Collections.Generic.List[string]]::new()
$projects = Get-ChildItem -Path $root -Recurse -Filter *.csproj -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }

if ($projects.Count -eq 0) { $failures.Add("No project files were found.") }

foreach ($project in $projects) {
    [xml]$xml = Get-Content -LiteralPath $project.FullName -Raw
    $frameworks = @($xml.Project.PropertyGroup.TargetFramework) + @($xml.Project.PropertyGroup.TargetFrameworks) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $expected = if ($project.Name -eq 'UTF.UI.csproj') { 'net10.0-windows' } else { 'net10.0' }
    $relative = $project.FullName.Substring($root.Length).TrimStart('\', '/')
    if ($frameworks -notcontains $expected) { $failures.Add("$relative must target $expected.") }
    if (($frameworks -join ';') -match 'net9\.0') { $failures.Add("$relative still references net9.0.") }
}

$requiredProjects = @(
    'UTF.Core\UTF.Core.csproj',
    'UTF.Plugin.Host\UTF.Plugin.Host.csproj',
    'UTF.UI\UTF.UI.csproj',
    'tests\UTF.Core.Tests\UTF.Core.Tests.csproj'
)
foreach ($relativePath in $requiredProjects) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relativePath))) {
        $failures.Add("Required project is missing: $relativePath")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Migration validation passed for $($projects.Count) projects."
