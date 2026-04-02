param(
    [string]$Root = 'D:\',
    [double]$MinSizeMB = 100
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

function Get-PathRank {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $normalized = $Path.ToLowerInvariant()

    if ($normalized.StartsWith('d:\aitoy\')) { return 10 }
    if ($normalized.StartsWith('d:\郑山\')) { return 20 }
    if ($normalized.StartsWith('d:\toolsource\')) { return 30 }
    if ($normalized.StartsWith('d:\_organized\archives\')) { return 60 }
    if ($normalized.StartsWith('d:\baidunetdiskdownload\')) { return 70 }
    if ($normalized.StartsWith('d:\迅雷下载\')) { return 80 }
    if ($normalized.StartsWith('d:\360downloads\')) { return 90 }
    if ($normalized.StartsWith('d:\virtual machines\sharefile\')) { return 95 }
    return 50
}

function Is-MovableDuplicate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $normalized = $Path.ToLowerInvariant()

    return (
        $normalized.StartsWith('d:\_organized\archives\') -or
        $normalized.StartsWith('d:\baidunetdiskdownload\') -or
        $normalized.StartsWith('d:\迅雷下载\') -or
        $normalized.StartsWith('d:\360downloads\') -or
        $normalized.StartsWith('d:\virtual machines\sharefile\')
    )
}

function New-SafeName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $invalidChars = [System.IO.Path]::GetInvalidFileNameChars()
    $safe = $Value
    foreach ($char in $invalidChars) {
        $safe = $safe.Replace($char, '_')
    }

    return $safe
}

function Move-FileWithUniqueName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    Ensure-Directory -Path $DestinationDirectory

    $fileName = [System.IO.Path]::GetFileName($SourcePath)
    $destinationPath = Join-Path $DestinationDirectory $fileName

    if (-not (Test-Path -LiteralPath $destinationPath)) {
        Move-Item -LiteralPath $SourcePath -Destination $destinationPath
        return $destinationPath
    }

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($fileName)
    $extension = [System.IO.Path]::GetExtension($fileName)
    $index = 1

    do {
        $candidate = '{0} ({1}){2}' -f $baseName, $index, $extension
        $destinationPath = Join-Path $DestinationDirectory $candidate
        $index++
    }
    while (Test-Path -LiteralPath $destinationPath)

    Move-Item -LiteralPath $SourcePath -Destination $destinationPath
    return $destinationPath
}

$reviewRoot = Join-Path $Root '_Organized\DuplicatesReview'
$reportRoot = Join-Path $Root '_Organized\Reports'
Ensure-Directory -Path $reviewRoot
Ensure-Directory -Path $reportRoot

$packageExtensions = @('.zip', '.7z', '.rar', '.iso', '.msi', '.exe')
$allPackages = Get-ChildItem -LiteralPath $Root -Recurse -Force -File -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Extension.ToLowerInvariant() -in $packageExtensions -and
        $_.Length -ge ($MinSizeMB * 1MB) -and
        -not $_.FullName.ToLowerInvariant().StartsWith('d:\_organized\duplicatesreview\')
    }

$duplicateGroups = $allPackages |
    Group-Object Name, Length |
    Where-Object { $_.Count -gt 1 }

$movedItems = New-Object System.Collections.Generic.List[object]
$skippedGroups = New-Object System.Collections.Generic.List[object]

foreach ($group in $duplicateGroups) {
    $items = $group.Group | Sort-Object @{ Expression = { Get-PathRank -Path $_.FullName } }, @{ Expression = { $_.LastWriteTime }; Descending = $true }
    $keeper = $items | Select-Object -First 1

    $movableItems = $items | Where-Object {
        $_.FullName -ne $keeper.FullName -and
        (Is-MovableDuplicate -Path $_.FullName)
    }

    if (-not $movableItems) {
        $skippedGroups.Add([pscustomobject]@{
            Name = $keeper.Name
            SizeMB = [math]::Round(($keeper.Length / 1MB), 2)
            Keeper = $keeper.FullName
            Reason = 'No movable duplicates in transient folders'
        }) | Out-Null
        continue
    }

    $groupDirectory = Join-Path $reviewRoot (New-SafeName -Value ([System.IO.Path]::GetFileNameWithoutExtension($keeper.Name)))

    foreach ($item in $movableItems) {
        $destinationPath = Move-FileWithUniqueName -SourcePath $item.FullName -DestinationDirectory $groupDirectory
        $movedItems.Add([pscustomobject]@{
            Name = $item.Name
            SizeMB = [math]::Round(($item.Length / 1MB), 2)
            KeptPath = $keeper.FullName
            MovedFrom = $item.FullName
            MovedTo = $destinationPath
            LastWriteTime = $item.LastWriteTime
        }) | Out-Null
    }
}

$movedReportPath = Join-Path $reportRoot 'duplicate-packages-moved-report.csv'
$skippedReportPath = Join-Path $reportRoot 'duplicate-packages-skipped-report.csv'

$movedItems | Export-Csv -LiteralPath $movedReportPath -NoTypeInformation -Encoding UTF8
$skippedGroups | Export-Csv -LiteralPath $skippedReportPath -NoTypeInformation -Encoding UTF8

Write-Output '=== DuplicateMoveSummary ==='
$movedItems | Sort-Object Name, MovedFrom | Format-Table -AutoSize

Write-Output '=== Reports ==='
[pscustomobject]@{
    MovedReport = $movedReportPath
    SkippedReport = $skippedReportPath
    MovedCount = $movedItems.Count
    SkippedGroupCount = $skippedGroups.Count
} | Format-List
