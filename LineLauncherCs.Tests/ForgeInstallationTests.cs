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

using LMCCore.Game.Download.Installation.Forge;

namespace LineLauncherCs.Tests;

public class ForgeInstallationTests
{
    [Fact]
    public void ForgeLibraryPathHelper_GetLibPath_ResolvesJarAndClassifierExtension()
    {
        var jarPath = ForgeLibraryPathHelper.GetLibPath("net.minecraftforge:forge:1.20.1-47.2.0");
        var lzmaPath = ForgeLibraryPathHelper.GetLibPath("net.minecraftforge:forge:47.2.0:clientdata@lzma");
        var mojmapsPath = ForgeLibraryPathHelper.GetLibPath("net.minecraft:client:1.20.6:mappings@tsrg");

        Assert.Equal(
            Path.Combine(".minecraft", "libraries", "net", "minecraftforge", "forge", "1.20.1-47.2.0", "forge-1.20.1-47.2.0.jar"),
            jarPath);
        Assert.Equal(
            Path.Combine(".minecraft", "libraries", "net", "minecraftforge", "forge", "47.2.0", "forge-47.2.0-clientdata.lzma"),
            lzmaPath);
        Assert.Equal(
            Path.Combine(".minecraft", "libraries", "net", "minecraft", "client", "1.20.6", "client-1.20.6-mappings.tsrg"),
            mojmapsPath);
    }
}
