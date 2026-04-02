param(
    [string]$Root = 'D:\',
    [int]$TopExtensions = 30,
    [int]$TopLargestFiles = 25
)

$ErrorActionPreference = 'SilentlyContinue'

$topLevelItems = Get-ChildItem $Root -Force |
    Select-Object Name, Mode, Length, LastWriteTime

$files = Get-ChildItem $Root -Recurse -Force -File

$extensionStats = $files |
    Group-Object Extension |
    Sort-Object Count -Descending |
    Select-Object -First $TopExtensions @{
        Name = 'Extension'
        Expression = { $_.Name }
    }, Count, @{
        Name = 'SizeGB'
        Expression = {
            [math]::Round((($_.Group | Measure-Object Length -Sum).Sum / 1GB), 2)
        }
    }

$directoryStats = Get-ChildItem $Root -Force -Directory |
    ForEach-Object {
        $dirFiles = Get-ChildItem $_.FullName -Recurse -Force -File
        [pscustomobject]@{
            Name = $_.Name
            FileCount = $dirFiles.Count
            SizeGB = [math]::Round((($dirFiles | Measure-Object Length -Sum).Sum / 1GB), 2)
        }
    } |
    Sort-Object SizeGB -Descending

$largestFiles = $files |
    Sort-Object Length -Descending |
    Select-Object -First $TopLargestFiles @{
        Name = 'FullName'
        Expression = { $_.FullName }
    }, @{
        Name = 'SizeGB'
        Expression = { [math]::Round(($_.Length / 1GB), 2) }
    }, LastWriteTime

$packageFiles = $files |
    Where-Object {
        $_.Extension -in '.zip', '.7z', '.rar', '.iso', '.msi', '.exe'
    } |
    Sort-Object Length -Descending |
    Select-Object -First $TopLargestFiles @{
        Name = 'FullName'
        Expression = { $_.FullName }
    }, Extension, @{
        Name = 'SizeGB'
        Expression = { [math]::Round(($_.Length / 1GB), 2) }
    }, LastWriteTime

Write-Output '=== TopLevelItems ==='
$topLevelItems | Format-Table -AutoSize

Write-Output '=== DirectoryStats ==='
$directoryStats | Format-Table -AutoSize

Write-Output '=== ExtensionStats ==='
$extensionStats | Format-Table -AutoSize

Write-Output '=== LargestFiles ==='
$largestFiles | Format-Table -AutoSize

Write-Output '=== PackageFiles ==='
$packageFiles | Format-Table -AutoSize
