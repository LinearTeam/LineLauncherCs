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

using LMC;
using LMCCore.Java;

namespace LMCCore.Game.Download.Installation.Forge;

internal static class ForgeJavaSelector
{
    public async static Task<LocalJava> SelectJavaAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(Current.Config.SelectedJavaPath))
        {
            return await JavaManager.GetJavaInfo(Current.Config.SelectedJavaPath);
        }

        var allJavas = new List<LocalJava>();
        foreach (var path in Current.Config.JavaPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            allJavas.Add(await JavaManager.GetJavaInfo(path));
        }

        foreach (var preferredMajor in new[] { 21, 17, 8 })
        {
            var preferredJava = allJavas.FirstOrDefault(java => java.Version.Major == preferredMajor);
            if (preferredJava != null)
            {
                return preferredJava;
            }
        }

        var highestJava = allJavas
            .OrderByDescending(java => java.Version)
            .FirstOrDefault();

        return highestJava
               ?? throw new InvalidOperationException("No available Java runtime was found for Forge installation.");
    }

    public static string GetJavaExecutablePath(LocalJava java)
    {
        var javaExecutable = OperatingSystem.IsWindows() ? "java.exe" : "java";
        return Path.Combine(java.Path, "bin", javaExecutable);
    }
}
