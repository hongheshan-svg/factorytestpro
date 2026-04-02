param(
    [string]$Root = 'D:\'
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

$skippedReportPath = Join-Path $reportRoot 'duplicate-packages-skipped-report.csv'
$duplicateReportPath = Join-Path $reportRoot 'duplicate-packages-report.csv'

$targets = Import-Csv -LiteralPath $skippedReportPath
$allDuplicates = Import-Csv -LiteralPath $duplicateReportPath

$movedItems = New-Object System.Collections.Generic.List[object]
$missingItems = New-Object System.Collections.Generic.List[object]

foreach ($target in $targets) {
    $keeperPath = $target.Keeper
    $name = $target.Name

    if (-not (Test-Path -LiteralPath $keeperPath)) {
        $missingItems.Add([pscustomobject]@{
            Name = $name
            MissingPath = $keeperPath
            Reason = 'Keeper missing'
        }) | Out-Null
        continue
    }

    $matches = $allDuplicates | Where-Object {
        $_.Name -eq $name -and
        $_.FullName -ne $keeperPath
    }

    if (-not $matches) {
        $missingItems.Add([pscustomobject]@{
            Name = $name
            MissingPath = $keeperPath
            Reason = 'No duplicate rows found'
        }) | Out-Null
        continue
    }

    foreach ($match in $matches) {
        $sourcePath = $match.FullName

        if (-not (Test-Path -LiteralPath $sourcePath)) {
            $missingItems.Add([pscustomobject]@{
                Name = $name
                MissingPath = $sourcePath
                Reason = 'Duplicate missing'
            }) | Out-Null
            continue
        }

        $destinationDirectory = Join-Path $reviewRoot (New-SafeName -Value ([System.IO.Path]::GetFileNameWithoutExtension($name)))
        $destinationPath = Move-FileWithUniqueName -SourcePath $sourcePath -DestinationDirectory $destinationDirectory
        $fileInfo = Get-Item -LiteralPath $destinationPath

        $movedItems.Add([pscustomobject]@{
            Name = $name
            SizeMB = [math]::Round(($fileInfo.Length / 1MB), 2)
            KeptPath = $keeperPath
            MovedFrom = $sourcePath
            MovedTo = $destinationPath
        }) | Out-Null
    }
}

$movedReportPath = Join-Path $reportRoot 'selected-duplicate-packages-moved-report.csv'
$missingReportPath = Join-Path $reportRoot 'selected-duplicate-packages-missing-report.csv'

$movedItems | Export-Csv -LiteralPath $movedReportPath -NoTypeInformation -Encoding UTF8
$missingItems | Export-Csv -LiteralPath $missingReportPath -NoTypeInformation -Encoding UTF8

Write-Output '=== SelectedDuplicateMoveSummary ==='
$movedItems | Format-Table -AutoSize

Write-Output '=== Reports ==='
[pscustomobject]@{
    MovedReport = $movedReportPath
    MissingReport = $missingReportPath
    MovedCount = $movedItems.Count
    MissingCount = $missingItems.Count
} | Format-List
