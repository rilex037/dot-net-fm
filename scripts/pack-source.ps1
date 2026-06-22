<#
.SYNOPSIS
Packs or unpacks source files into/from a 7z archive, preserving relative folder structure.

.PARAMETER Command
Mandatory. "pack" or "unpack".

.PARAMETER ArchiveName
Archive file name. Default "source.7z" in the project root.

.PARAMETER Extensions
File extensions to include when packing. Default @(".cs").

.PARAMETER MaxRetries
Number of retry attempts on failure. Default 3.

.PARAMETER RetryDelayMs
Delay between retries in milliseconds. Default 500.

.EXAMPLE
.\pack-source.ps1 pack
Packs all *.cs files into source.7z.

.EXAMPLE
.\pack-source.ps1 pack -ArchiveName "myarchive.7z" -Extensions @(".cs",".xaml")
Packs *.cs and *.xaml files into myarchive.7z.

.EXAMPLE
.\pack-source.ps1 unpack
Extracts source.7z into the project root, overwriting without prompting.
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
    [string]$SevenZipPath = "C:\Program Files\7-Zip\7z.exe"
)

$ErrorActionPreference = "Stop"

# ── Resolve project root (where .gitignore lives) ──────────────────────────
$scriptDir = Split-Path -Parent $PSCommandPath
$projectRoot = Split-Path -Parent $scriptDir  # project root = parent of scripts/
$archivePath = Join-Path $projectRoot $ArchiveName

# ── Helper: retry wrapper ─────────────────────────────────────────────────
function Invoke-WithRetry {
    param(
        [scriptblock]$ScriptBlock,
        [string]$Label
    )

    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        try {
            & $ScriptBlock
            return  # success → exit function
        } catch {
            $ex = $_.Exception
            if ($attempt -lt $MaxRetries) {
                Write-Warning "$Label – attempt $attempt/$MaxRetries failed: $($ex.Message). Retrying in ${RetryDelayMs}ms..."
                Start-Sleep -Milliseconds $RetryDelayMs
            } else {
                Write-Error "$Label – all $MaxRetries attempts failed: $($ex.Message)"
                throw  # terminating error
            }
        }
    }
}

# ── Pack ──────────────────────────────────────────────────────────────────
function Pack-Archive {
    Write-Host "Packing source files into '$archivePath'..."
    Write-Host "Extensions: $($Extensions -join ', ')"

    # Collect matching files (relative paths)
    $fileList = @()
    $rootNorm = $projectRoot.TrimEnd('\') + '\'
    foreach ($ext in $Extensions) {
        $matches = Get-ChildItem -Path $projectRoot -Recurse -File -Filter "*$ext" |
                   Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git|\.vs|node_modules)\\' } |
                   ForEach-Object { $_.FullName.Replace($rootNorm, '') }
        $fileList += $matches
    }

    if ($fileList.Count -eq 0) {
        Write-Warning "No matching source files found for extensions: $($Extensions -join ', ')"
        return
    }

    Write-Host "Found $($fileList.Count) source file(s)."

    # Write temp file list for 7z (handles large number of files safely)
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

# ── Unpack ────────────────────────────────────────────────────────────────
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

# ── Main ──────────────────────────────────────────────────────────────────
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