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

using System.Collections.ObjectModel;

namespace LMC;

using Basic.Configs;

public static class Current
{
    public const string Version = "3.0.0";

    public const string BuildNumber = "0009";

    public const string VersionType = "alpha";
    
    public readonly static string LMCPath =  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LMC");
    public static AppConfig? Config { get; set; }

    public static ReadOnlyDictionary<string, string> Arguments = null!;
    public static void SaveConfig() {
        ConfigManager.Save("app", Config);
    }
}