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
using LMC.Basic.Logging;

namespace LMCCore.Java.Discovery;

internal sealed class JavaDirectoryScanner(
    Func<string, Task<bool>> validateJavaRootAsync,
    Logger logger,
    int maxDepth = 4)
{
    private readonly Func<string, Task<bool>> _validateJavaRootAsync = validateJavaRootAsync;
    private readonly Logger _logger = logger;
    private readonly int _maxDepth = maxDepth;

    public async Task ScanDirectoryRecursivelyAsync(string directory, ISet<string> paths, int depth = 0)
    {
        if (depth >= _maxDepth || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            if (await _validateJavaRootAsync(directory))
            {
                _logger.Info($"[Java 搜索] Dir: {directory}");
                paths.Add(JavaPathNormalizer.NormalizeRootPath(directory));
                return;
            }

            foreach (var subDirectory in Directory.GetDirectories(directory))
            {
                await ScanDirectoryRecursivelyAsync(subDirectory, paths, depth + 1);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Scan directory for Java");
        }
    }
}
