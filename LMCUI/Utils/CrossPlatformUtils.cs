namespace LMCUI.Utils;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using LMC.Basic.Logging;

public static class CrossPlatformUtils {
    static readonly Logger s_logger = new("XPlatformUtils");

    public static void OpenFolderInExplorer(string folderPath) {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", $"\"{folderPath}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", $"\"{folderPath}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    Process.Start("xdg-open", $"\"{folderPath}\"");
                }
                catch
                {
                    var managers = new[]
                    {
                        "dolphin", "nemo"
                    };
                    foreach (var manager in managers)
                    {
                        try
                        {
                            Process.Start(manager, $"\"{folderPath}\"");
                            return;
                        }
                        catch {}
                    }
                    throw;
                }
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, $"打开文件夹 {folderPath}");
            throw;
        }
    }

    public async static Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerAsync(FolderPickerOpenOptions options) {
        var storage = MainWindow.Instance.StorageProvider;
        if (storage.CanPickFolder)
        {
            return await storage.OpenFolderPickerAsync(options);
        }
        else
        {
            s_logger.Error("当前平台无法选择文件夹");
            return Array.Empty<IStorageFolder>();
        }
    }

    public async static Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options) {
        var storage = MainWindow.Instance.StorageProvider;
        if (storage.CanPickFolder)
        {
            return await storage.OpenFilePickerAsync(options);
        }
        else
        {
            s_logger.Error("当前平台无法选择文件");
            return Array.Empty<IStorageFile>();
        }
    }
}
