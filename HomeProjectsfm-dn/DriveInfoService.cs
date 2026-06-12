using System;
using System.Collections.Generic;
using System.IO;

namespace FmDn;

/// <summary>
/// Single responsibility: builds FolderItem representations of system drives.
/// Used by NavigationService for the My Computer view.
/// </summary>
public static class DriveInfoService
{
    /// <summary>
    /// Returns one FolderItem per ready, fixed/removable/network drive.
    /// </summary>
    public static List<FolderItem> GetDriveItems()
    {
        var items = new List<FolderItem>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable or DriveType.Network))
                continue;

            if (!drive.IsReady)
                continue;

            string root = drive.Name.TrimEnd('\\');
            string displayName = string.IsNullOrEmpty(drive.VolumeLabel)
                ? root
                : $"{drive.VolumeLabel} ({root})";

            double freeGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            double totalGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);

            var item = new FolderItem
            {
                Name = displayName,
                ItemCount = $"{freeGB:F1} GB free of {totalGB:F0} GB",
                FullPath = drive.Name,
                IsFolder = true
            };

            // Try to get native drive icon via shell
            var nativeIcon = NativeIconHelper.GetIconForFile(drive.Name);
            if (nativeIcon != null)
                item.NativeIcon = nativeIcon;

            items.Add(item);
        }

        return items;
    }
}