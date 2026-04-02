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

function Move-ItemWithUniqueName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    Ensure-Directory -Path $DestinationDirectory

    $name = [System.IO.Path]::GetFileName($SourcePath.TrimEnd('\'))
    $destinationPath = Join-Path $DestinationDirectory $name

    if (-not (Test-Path -LiteralPath $destinationPath)) {
        Move-Item -LiteralPath $SourcePath -Destination $destinationPath
        return $destinationPath
    }

    if (Test-Path -LiteralPath $SourcePath -PathType Leaf) {
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($name)
        $extension = [System.IO.Path]::GetExtension($name)
    }
    else {
        $baseName = $name
        $extension = ''
    }

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

$target = Get-ChildItem -LiteralPath $DriveRoot -Directory -Force |
    Where-Object { $_.Name -like '*下载*' -and $_.Name -ne 'BaiduNetdiskDownload' } |
    Select-Object -First 1

if ($null -eq $target) {
    throw 'Download directory not found.'
}

$organizedRoot = Join-Path $target.FullName '_Organized'
$installers = Join-Path $organizedRoot 'Installers'
$folders = Join-Path $organizedRoot 'ExtractedFolders'
$reports = Join-Path $organizedRoot 'Reports'

foreach ($path in @($organizedRoot, $installers, $folders, $reports)) {
    Ensure-Directory -Path $path
}

$moves = New-Object System.Collections.Generic.List[object]
$topLevelItems = Get-ChildItem -LiteralPath $target.FullName -Force

foreach ($item in $topLevelItems) {
    if ($item.FullName -eq $organizedRoot) {
        continue
    }

    if ($item.PSIsContainer) {
        $newPath = Move-ItemWithUniqueName -SourcePath $item.FullName -DestinationDirectory $folders
        $moves.Add([pscustomobject]@{
            Name = $item.Name
            Category = 'ExtractedFolders'
            OriginalPath = $item.FullName
            NewPath = $newPath
        }) | Out-Null
        continue
    }

    $newPath = Move-ItemWithUniqueName -SourcePath $item.FullName -DestinationDirectory $installers
    $moves.Add([pscustomobject]@{
        Name = $item.Name
        Category = 'Installers'
        OriginalPath = $item.FullName
        NewPath = $newPath
    }) | Out-Null
}

$reportPath = Join-Path $reports 'move-report.csv'
$moves | Export-Csv -LiteralPath $reportPath -NoTypeInformation -Encoding UTF8

Write-Output '=== XunleiMoveSummary ==='
$moves | Format-Table -AutoSize
