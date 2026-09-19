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

using System.Diagnostics;
using System.Runtime.InteropServices;
using LMC;
using LMC.Basic.Logging;
using LMCCore.Game.Launching.Configuration;
using LMCCore.Game.Launching.Execution;
using LMCCore.Java;

namespace LMCCore.Game.Launching.Steps.Startup;

public sealed class StartGameStepHandler : IGameLaunchStepHandler
{
    public GameLaunchProgressStep Step => GameLaunchProgressStep.StartGame;

    public async Task ExecuteAsync(GameLaunchContext context, CancellationToken cancellationToken)
    {
        context.Process = null;
        var process = new Process();
        var launchSettings = context.LaunchSettings ?? throw new InvalidOperationException(
            "Launch settings have not been resolved.");
        var java = await JavaManager.SelectJavaAsync(
            context.Config.JavaPaths,
            launchSettings.SelectedJavaPath,
            launchSettings.AutoSelectJava,
            context.Version,
            cancellationToken);
        var javaExecutablePath = Path.Combine(java.Path, "bin",
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java");
        var wrapperArguments = launchSettings.WrapperArguments;
        var startInfoFileName = javaExecutablePath;
        var argumentPrefix = string.Empty;
        if (!string.IsNullOrWhiteSpace(wrapperArguments))
        {
            var wrapperParts = CommandLineArgumentParser.Parse(wrapperArguments);
            if (wrapperParts.Count > 0)
            {
                startInfoFileName = wrapperParts[0];
                argumentPrefix = string.Join(' ', wrapperParts.Skip(1).Append($"\"{javaExecutablePath}\""));
            }
        }

        var gameArguments = string.Join(' ', context.CommandBuilder!.GetJvmArguments()) +
                            $" {context.Version.VersionInfo!.MainClass} " +
                            string.Join(' ', context.CommandBuilder.GetGameArguments());
        var psi = new ProcessStartInfo(startInfoFileName)
            {
                Arguments = string.IsNullOrEmpty(argumentPrefix)
                    ? gameArguments
                    : $"{argumentPrefix} {gameArguments}",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = context.Version.VersionDirectory
            };
        await File.WriteAllTextAsync(Path.Combine(context.Version.VersionDirectory, "launch.bat"), psi.Arguments, cancellationToken);
        new Logger("Args").Debug(psi.FileName + " " +psi.Arguments);
        process.StartInfo = psi;
        context.Process = process;
        process.Start();
    }
}
