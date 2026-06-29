<#
.EXAMPLE
.\pack-source.ps1 pack
.\pack-source.ps1 pack -Extensions @(".cs",".xaml")
.\pack-source.ps1 pack -ArchiveName "backup.7z" -Extensions ".cs",".xaml",".xml"
.\pack-source.ps1 pack -SourcePath "src" -Extensions @(".kt",".xml")
.\pack-source.ps1 pack -SourcePath "src" -ArchiveName "src-only.7z" -Extensions ".kt",".xml"

.EXAMPLE
.\pack-source.ps1 unpack
.\pack-source.ps1 unpack -ArchiveName "backup.7z"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("pack", "unpack")]
    [string]$Command,

    [Parameter(Mandatory=$false)]
    [string]$ArchiveName = "source.7z",

    [Parameter(Mandatory=$false)]
    [string[]]$Extensions = @(".cs"),

    [Parameter(Mandatory=$false)]
    [int]$MaxRetries = 3,

    [Parameter(Mandatory=$false)]
    [int]$RetryDelayMs = 500,

    [Parameter(Mandatory=$false)]
    [string]$SevenZipPath = "C:\Program Files\7-Zip\7z.exe",

    [Parameter(Mandatory=$false)]
    [string]$SourcePath = "."
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $PSCommandPath
$projectRoot = Split-Path -Parent $scriptDir
$archivePath = Join-Path $projectRoot $ArchiveName

function Invoke-WithRetry {
    param(
        [scriptblock]$ScriptBlock,
        [string]$Label
    )

    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        try {
            & $ScriptBlock
            return
        } catch {
            $ex = $_.Exception
            if ($attempt -lt $MaxRetries) {
                Write-Warning "$Label – attempt $attempt/$MaxRetries failed: $($ex.Message). Retrying in ${RetryDelayMs}ms..."
                Start-Sleep -Milliseconds $RetryDelayMs
            } else {
                Write-Error "$Label – all $MaxRetries attempts failed: $($ex.Message)"
                throw
            }
        }
    }
}

function Pack-Archive {
    $sourceDir = if ($SourcePath -eq "." -or $SourcePath -eq "") {
        $projectRoot
    } else {
        Join-Path $projectRoot $SourcePath
    }

    if (-not (Test-Path $sourceDir)) {
        Write-Error "Source path not found: $sourceDir"
        throw "Source path not found: $sourceDir"
    }

    Write-Host "Packing source files into '$archivePath'..."
    Write-Host "Source path: $sourceDir"
    Write-Host "Extensions: $($Extensions -join ', ')"

    $fileList = @()
    $rootNorm = $projectRoot.TrimEnd('\') + '\'
    foreach ($ext in $Extensions) {
        $matches = Get-ChildItem -Path $sourceDir -Recurse -File -Filter "*$ext" |
                   Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git|\.vs|node_modules)\\' } |
                   ForEach-Object { $_.FullName.Replace($rootNorm, '') }
        $fileList += $matches
    }

    if ($fileList.Count -eq 0) {
        Write-Warning "No matching source files found for extensions: $($Extensions -join ', ')"
        return
    }

    Write-Host "Found $($fileList.Count) source file(s)."

    $tempList = [System.IO.Path]::GetTempFileName()
    try {
        $fileList | Set-Content -Path $tempList -Encoding UTF8

        $sevenArgs = @("a", "-y", "-spf", "`"$archivePath`"", "@`"$tempList`"")
        $process = @{
            FilePath     = $SevenZipPath
            ArgumentList = $sevenArgs
            NoNewWindow  = $true
            WorkingDirectory = $projectRoot
            PassThru     = $true
        }

        Invoke-WithRetry -ScriptBlock {
            $proc = Start-Process @process -Wait
            if ($proc.ExitCode -ne 0) {
                throw "7z exited with code $($proc.ExitCode)"
            }
        } -Label "7z pack"
    } finally {
        if (Test-Path $tempList) { Remove-Item $tempList -Force }
    }

    Write-Host "Packed successfully: $archivePath"
}

function Unpack-Archive {
    if (-not (Test-Path $archivePath)) {
        Write-Error "Archive not found: $archivePath"
        throw "Archive not found: $archivePath"
    }

    Write-Host "Unpacking '$archivePath' into '$projectRoot'..."

    $sevenArgs = @("x", "-y", "-aoa", "-spf", "`"$archivePath`"", "-o`"$projectRoot`"")
    $process = @{
        FilePath     = $SevenZipPath
        ArgumentList = $sevenArgs
        NoNewWindow  = $true
        WorkingDirectory = $projectRoot
        PassThru     = $true
    }

    Invoke-WithRetry -ScriptBlock {
        $proc = Start-Process @process -Wait
        if ($proc.ExitCode -ne 0) {
            throw "7z exited with code $($proc.ExitCode)"
        }
    } -Label "7z unpack"

    Write-Host "Unpacked successfully."
}

try {
    switch ($Command) {
        "pack"   { Pack-Archive }
        "unpack" { Unpack-Archive }
    }
    exit 0
} catch {
    Write-Error "Fatal: $_"
    exit 1
}
