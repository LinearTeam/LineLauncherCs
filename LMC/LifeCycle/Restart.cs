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

namespace LMC.LifeCycle;

public class Restart
{
    public static void Run()
    {
        var path = Environment.ProcessPath;
        var arguments = Environment.GetCommandLineArgs();
        if (!arguments.Contains("--restart"))
        {
            arguments = arguments.Append("--restart=" + Environment.ProcessId).ToArray();
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            Arguments = string.Join(" ", arguments.Skip(1)),
            UseShellExecute = true
        });
    }
}