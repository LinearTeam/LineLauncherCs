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

using System.Text.Json.Nodes;
using LMCCore.Utils;

namespace LMCCore.Game.Download.Installation.OptiFine;

internal static class OptiFineVersionJsonMutator
{
    public static string Apply(string versionJson, OptiFineInstallationRuntimeState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionJson);
        ArgumentNullException.ThrowIfNull(state);

        var json = JsonUtils.Parse(versionJson);
        EnsureMainClass(json);
        AppendGameArguments(json);
        AppendLibraries(json, state);
        return json.ToString();
    }

    private static void EnsureMainClass(JsonUtils json)
    {
        json.Set("mainClass", "net.minecraft.launchwrapper.Launch");
    }

    private static void AppendGameArguments(JsonUtils json)
    {
        if (json.Node is not JsonObject rootObject)
        {
            throw new InvalidOperationException("Version json root is not an object.");
        }

        if (rootObject["arguments"] is JsonObject argumentsObject)
        {
            var gameArray = argumentsObject["game"] as JsonArray ?? [];
            argumentsObject["game"] = gameArray;
            gameArray.Add("--tweakClass");
            gameArray.Add("optifine.OptiFineTweaker");
            return;
        }

        var existingArguments = json.GetStringOrDefault("minecraftArguments", string.Empty) ?? string.Empty;
        if (!existingArguments.Contains("optifine.OptiFineTweaker", StringComparison.Ordinal))
        {
            json.Set("minecraftArguments", existingArguments + " --tweakClass optifine.OptiFineTweaker");
        }
    }

    private static void AppendLibraries(JsonUtils json, OptiFineInstallationRuntimeState state)
    {
        if (json.Node is not JsonObject rootObject)
        {
            throw new InvalidOperationException("Version json root is not an object.");
        }

        var libraries = rootObject["libraries"] as JsonArray ?? [];
        rootObject["libraries"] = libraries;

        libraries.Add(CreateLibraryNode(state.LibraryCoordinate));
        if (!string.IsNullOrWhiteSpace(state.LaunchWrapperCoordinate))
        {
            libraries.Add(CreateLibraryNode(state.LaunchWrapperCoordinate));
        }
    }

    private static JsonObject CreateLibraryNode(string coordinate)
    {
        return new JsonObject
        {
            ["name"] = coordinate
        };
    }
}
