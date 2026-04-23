// Copyright 2025-2026 LinearTeam
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using Avalonia.Controls;

namespace LMCUI.Utils;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using LMC.Basic.Logging;

public static class CrossPlatformUtils {
    private readonly static Logger s_logger = new("XPlatformUtils");
    public static void OpenUrl(string url) => OpenUrl(new Uri(url));
    public static void OpenUrl(Uri url)
    {
        try
        {
            var launcher = TopLevel.GetTopLevel(MainWindow.Instance)?.Launcher;
            if (launcher != null)
            {
                launcher.LaunchUriAsync(url);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, $"打开链接 {url}");
            throw;
        }
    }
    public static void OpenFolderInExplorer(string folderPath) {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

        try
        {
            var launcher = TopLevel.GetTopLevel(MainWindow.Instance)?.Launcher;
            if (launcher != null)
            {
                launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folderPath));
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

    public static void OpenFileInExplorer(string filePath) {
        if (!Directory.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        try
        {
            var launcher = TopLevel.GetTopLevel(MainWindow.Instance)?.Launcher;
            if (launcher != null)
            {
                launcher.LaunchFileInfoAsync(new FileInfo(filePath));
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, $"打开文件夹 {filePath}");
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
