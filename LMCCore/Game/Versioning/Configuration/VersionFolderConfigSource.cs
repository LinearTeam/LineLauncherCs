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
using LMCCore.Game.Model;
using LMCCore.Game.Versioning.Configuration.Support;
using LMCCore.Utils;

namespace LMCCore.Game.Versioning.Configuration;

public class VersionFolderConfigSource : IVersionConfigSource
{
    private const string ConfigDirectoryName = "LMC";
    private const string ConfigFileName = "version_config.json";

    public VersionConfigSourceType SourceType => VersionConfigSourceType.VersionFolder;

    public JsonUtils? TryLoad(LocalGameVersionEntry version, VersionConfigFileCache cache)
    {
        var configPath = Path.Combine(version.VersionDirectory, ConfigDirectoryName, ConfigFileName);
        return cache.GetOrAdd(configPath);
    }
}
