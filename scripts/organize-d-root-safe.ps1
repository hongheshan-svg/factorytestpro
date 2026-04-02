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

function Get-TargetDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$File
    )

    $extension = $File.Extension.ToLowerInvariant()

    switch ($extension) {
        '.zip' { return 'Archives' }
        '.7z' { return 'Archives' }
        '.rar' { return 'Archives' }
        '.iso' { return 'Archives' }
        '.pdf' { return 'Documents' }
        '.ppt' { return 'Documents' }
        '.pptx' { return 'Documents' }
        '.doc' { return 'Documents' }
        '.docx' { return 'Documents' }
        '.csv' { return 'Data' }
        '.txt' { return 'Data' }
        '.ini' { return 'Data' }
        '.log' { return 'Logs' }
        '.exe' { return 'Installers' }
        '.msi' { return 'Installers' }
        '.cab' { return 'Installers' }
        '.dll' { return 'Installers' }
        '.bmp' { return 'Misc' }
        default { return 'Misc' }
    }
}

function Move-FileSafely {
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
        $candidateName = '{0} ({1}){2}' -f $baseName, $index, $extension
        $destinationPath = Join-Path $DestinationDirectory $candidateName
        $index++
    }
    while (Test-Path -LiteralPath $destinationPath)

    Move-Item -LiteralPath $File.FullName -Destination $destinationPath
    return $destinationPath
}

$organizeRoot = Join-Path $Root '_Organized'
$reportRoot = Join-Path $organizeRoot 'Reports'

Ensure-Directory -Path $organizeRoot
Ensure-Directory -Path $reportRoot

$categoryDirectories = @(
    'Archives',
    'Documents',
    'Data',
    'Logs',
    'Installers',
    'Misc'
)

foreach ($category in $categoryDirectories) {
    Ensure-Directory -Path (Join-Path $organizeRoot $category)
}

$movedItems = New-Object System.Collections.Generic.List[object]
$resolvedRoot = (Resolve-Path -LiteralPath $Root).Path.TrimEnd('\')
$rootFiles = Get-ChildItem -LiteralPath $Root -Force -File

foreach ($file in $rootFiles) {
    if ($file.DirectoryName.TrimEnd('\') -ne $resolvedRoot) {
        continue
    }

    if ($file.Name -like 'pagefile.sys' -or $file.Name -like 'hiberfil.sys' -or $file.Name -like 'swapfile.sys') {
        continue
    }

    $category = Get-TargetDirectory -File $file
    $destinationDirectory = Join-Path $organizeRoot $category
    $destinationPath = Move-FileSafely -File $file -DestinationDirectory $destinationDirectory

    $movedItems.Add([pscustomobject]@{
        Name = $file.Name
        Category = $category
        OriginalPath = $file.FullName
        NewPath = $destinationPath
        SizeMB = [math]::Round(($file.Length / 1MB), 2)
        LastWriteTime = $file.LastWriteTime
    }) | Out-Null
}

$packageExtensions = @('.zip', '.7z', '.rar', '.iso', '.msi', '.exe')
$duplicatePackages = Get-ChildItem -LiteralPath $Root -Recurse -Force -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension.ToLowerInvariant() -in $packageExtensions } |
    Group-Object Name, Length |
    Where-Object { $_.Count -gt 1 } |
    ForEach-Object {
        foreach ($item in $_.Group) {
            [pscustomobject]@{
                Name = $item.Name
                SizeMB = [math]::Round(($item.Length / 1MB), 2)
                LastWriteTime = $item.LastWriteTime
                FullName = $item.FullName
            }
        }
    } |
    Sort-Object Name, FullName

$moveReportPath = Join-Path $reportRoot 'root-file-move-report.csv'
$duplicateReportPath = Join-Path $reportRoot 'duplicate-packages-report.csv'

$movedItems | Export-Csv -LiteralPath $moveReportPath -NoTypeInformation -Encoding UTF8
$duplicatePackages | Export-Csv -LiteralPath $duplicateReportPath -NoTypeInformation -Encoding UTF8

Write-Output '=== MoveSummary ==='
$movedItems | Sort-Object Category, Name | Format-Table -AutoSize

Write-Output '=== Reports ==='
[pscustomobject]@{
    MoveReport = $moveReportPath
    DuplicateReport = $duplicateReportPath
    MovedCount = $movedItems.Count
    DuplicatePackageCount = ($duplicatePackages | Measure-Object).Count
} | Format-List
