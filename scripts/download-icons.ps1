<#
.SYNOPSIS
Downloads Papirus icon SVGs for the FM-DN project.
Fetches 8 folder SVGs (for main grid) and 11 sidebar SVGs.
All go into Assets\Icons\.
#>

$scriptDir = Split-Path -Parent $PSCommandPath
$out = Join-Path $scriptDir "Assets\Icons"
$base = "https://raw.githubusercontent.com/PapirusDevelopmentTeam/papirus-icon-theme/master/Papirus"

$icons = @{
    # Sidebar (16x16)
    "16x16/places/user-home.svg"                  = "sidebar-home.svg"
    "16x16/places/user-desktop.svg"               = "sidebar-desktop.svg"
    "16x16/places/folder-documents.svg"           = "sidebar-documents.svg"
    "16x16/places/folder-music.svg"               = "sidebar-music.svg"
    "16x16/places/folder-pictures.svg"            = "sidebar-pictures.svg"
    "16x16/places/folder-video.svg"              = "sidebar-videos.svg"
    "16x16/places/folder-download.svg"            = "sidebar-downloads.svg"
    "16x16/places/folder-open-recent.svg"         = "sidebar-recent.svg"
    "16x16/devices/drive-harddisk.svg"            = "sidebar-filesystem.svg"
    "16x16/places/user-trash.svg"                 = "sidebar-trash.svg"
    "16x16/places/folder-network.svg"          = "sidebar-network.svg"
}

Write-Host "Creating output directory: $out"
New-Item -ItemType Directory -Force -Path $out | Out-Null

foreach ($src in $icons.Keys) {
    $url = "$base/$src"
    $dest = Join-Path $out $icons[$src]
    Write-Host "Downloading $url -> $dest"
    try {
        Invoke-WebRequest -Uri $url -OutFile $dest -ErrorAction Stop
    } catch {
        Write-Warning "Failed to download $url : $_"
    }
}

Write-Host "Done. Downloaded $($icons.Count) icons to $out"
