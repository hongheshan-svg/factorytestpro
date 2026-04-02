param(
    [string[]]$Targets = @(
        'D:\BaiduNetdiskDownload',
        'D:\迅雷下载'
    )
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

function Get-FileCategory {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$File
    )

    switch ($File.Extension.ToLowerInvariant()) {
        '.zip' { return 'Archives' }
        '.7z' { return 'Archives' }
        '.rar' { return 'Archives' }
        '.iso' { return 'Archives' }
        '.exe' { return 'Installers' }
        '.msi' { return 'Installers' }
        '.pdf' { return 'Documents' }
        '.doc' { return 'Documents' }
        '.docx' { return 'Documents' }
        '.ppt' { return 'Documents' }
        '.pptx' { return 'Documents' }
        '.txt' { return 'Documents' }
        default { return 'Misc' }
    }
}

$allMoves = New-Object System.Collections.Generic.List[object]

foreach ($target in $Targets) {
    if (-not (Test-Path -LiteralPath $target)) {
        continue
    }

    $organizedRoot = Join-Path $target '_Organized'
    $reportDir = Join-Path $organizedRoot 'Reports'
    $categories = @('Archives', 'Installers', 'Documents', 'ExtractedFolders', 'Misc', 'Reports')

    foreach ($category in $categories) {
        Ensure-Directory -Path (Join-Path $organizedRoot $category)
    }

    $topLevelItems = Get-ChildItem -LiteralPath $target -Force

    foreach ($item in $topLevelItems) {
        if ($item.FullName -eq $organizedRoot) {
            continue
        }

        if ($item.Name -eq '.accelerate') {
            continue
        }

        if ($item.PSIsContainer) {
            $destinationDirectory = Join-Path $organizedRoot 'ExtractedFolders'
            $newPath = Move-ItemWithUniqueName -SourcePath $item.FullName -DestinationDirectory $destinationDirectory
            $allMoves.Add([pscustomobject]@{
                Root = $target
                Name = $item.Name
                Type = 'Directory'
                Category = 'ExtractedFolders'
                OriginalPath = $item.FullName
                NewPath = $newPath
            }) | Out-Null
            continue
        }

        $category = Get-FileCategory -File $item
        $destinationDirectory = Join-Path $organizedRoot $category
        $newPath = Move-ItemWithUniqueName -SourcePath $item.FullName -DestinationDirectory $destinationDirectory
        $allMoves.Add([pscustomobject]@{
            Root = $target
            Name = $item.Name
            Type = 'File'
            Category = $category
            OriginalPath = $item.FullName
            NewPath = $newPath
        }) | Out-Null
    }

    $reportPath = Join-Path $reportDir 'move-report.csv'
    $allMoves |
        Where-Object { $_.Root -eq $target } |
        Export-Csv -LiteralPath $reportPath -NoTypeInformation -Encoding UTF8
}

Write-Output '=== DownloadDirMoveSummary ==='
$allMoves | Sort-Object Root, Category, Name | Format-Table -AutoSize
