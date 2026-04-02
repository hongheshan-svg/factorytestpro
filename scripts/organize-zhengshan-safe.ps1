param(
    [string]$DriveRoot = 'D:\'
)

$ErrorActionPreference = 'Stop'

function Ensure-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Get-CategoryForFile {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$File
    )

    switch ($File.Extension.ToLowerInvariant()) {
        '.pem' { return 'Security' }
        '.key' { return 'Security' }
        '.pfx' { return 'Security' }
        '.zip' { return 'Archives' }
        '.7z' { return 'Archives' }
        '.rar' { return 'Archives' }
        '.iso' { return 'Archives' }
        '.pdf' { return 'Documents' }
        '.doc' { return 'Documents' }
        '.docx' { return 'Documents' }
        '.ppt' { return 'Documents' }
        '.pptx' { return 'Documents' }
        '.xlsx' { return 'Documents' }
        '.xls' { return 'Documents' }
        '.txt' { return 'Documents' }
        default { return 'Misc' }
    }
}

function Move-FileWithUniqueName {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$File,

        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    Ensure-Directory -Path $DestinationDirectory

    $destinationPath = Join-Path $DestinationDirectory $File.Name
    if (-not (Test-Path -LiteralPath $destinationPath)) {
        Move-Item -LiteralPath $File.FullName -Destination $destinationPath
        return $destinationPath
    }

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($File.Name)
    $extension = $File.Extension
    $index = 1
    do {
        $candidate = '{0} ({1}){2}' -f $baseName, $index, $extension
        $destinationPath = Join-Path $DestinationDirectory $candidate
        $index++
    }
    while (Test-Path -LiteralPath $destinationPath)

    Move-Item -LiteralPath $File.FullName -Destination $destinationPath
    return $destinationPath
}

$root = Get-ChildItem -LiteralPath $DriveRoot -Directory -Force |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'newapi.pem') } |
    Select-Object -First 1 -ExpandProperty FullName

if ([string]::IsNullOrWhiteSpace($root)) {
    throw 'Target directory with newapi.pem not found.'
}

$organizedRoot = Join-Path $root '_Organized'
$reportRoot = Join-Path $organizedRoot 'Reports'

foreach ($path in @(
    $organizedRoot,
    $reportRoot,
    (Join-Path $organizedRoot 'Security'),
    (Join-Path $organizedRoot 'Archives'),
    (Join-Path $organizedRoot 'Documents'),
    (Join-Path $organizedRoot 'Misc')
)) {
    Ensure-Directory -Path $path
}

$movedItems = New-Object System.Collections.Generic.List[object]

$topFiles = Get-ChildItem -LiteralPath $root -Force -File
foreach ($file in $topFiles) {
    $category = Get-CategoryForFile -File $file
    $destinationDirectory = Join-Path $organizedRoot $category
    $newPath = Move-FileWithUniqueName -File $file -DestinationDirectory $destinationDirectory
    $movedItems.Add([pscustomobject]@{
        Name = $file.Name
        Category = $category
        OriginalPath = $file.FullName
        NewPath = $newPath
        SizeKB = [math]::Round(($file.Length / 1KB), 2)
        LastWriteTime = $file.LastWriteTime
    }) | Out-Null
}

$directoryStats = Get-ChildItem -LiteralPath $root -Force -Directory |
    Where-Object { $_.Name -ne '_Organized' } |
    ForEach-Object {
        $files = Get-ChildItem $_.FullName -Recurse -Force -File -ErrorAction SilentlyContinue
        [pscustomobject]@{
            Name = $_.Name
            FileCount = $files.Count
            SizeGB = [math]::Round((($files | Measure-Object Length -Sum).Sum / 1GB), 2)
        }
    } |
    Sort-Object SizeGB -Descending

$moveReportPath = Join-Path $reportRoot 'top-file-move-report.csv'
$dirReportPath = Join-Path $reportRoot 'top-directory-size-report.csv'

$movedItems | Export-Csv -LiteralPath $moveReportPath -NoTypeInformation -Encoding UTF8
$directoryStats | Export-Csv -LiteralPath $dirReportPath -NoTypeInformation -Encoding UTF8

Write-Output '=== ZhengShanMoveSummary ==='
$movedItems | Format-Table -AutoSize

Write-Output '=== ZhengShanDirectoryStats ==='
$directoryStats | Format-Table -AutoSize

Write-Output '=== Reports ==='
[pscustomobject]@{
    MoveReport = $moveReportPath
    DirectoryReport = $dirReportPath
    MovedCount = $movedItems.Count
    TopDirectoryCount = $directoryStats.Count
} | Format-List
