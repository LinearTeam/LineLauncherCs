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

using LMCCore.Utils;

namespace LineLauncherCs.Tests;

public class JsonUtilsTests
{
    [Fact]
    public void Set_CreatesMissingObjectPathAndPersistsTypedValue()
    {
        var json = JsonUtils.Parse("""{"launcher":{}}""");

        var written = json.Set("launcher.java.maxMemoryMb", 4096);

        Assert.True(written);
        Assert.Equal(4096, json.GetOrDefault("launcher.java.maxMemoryMb", 0));
    }

    [Fact]
    public void Set_CreatesMissingArrayPathAndPersistsNestedValue()
    {
        var json = JsonUtils.Parse("""{}""");

        var written = json.Set("downloads.files[1].url", "https://example.com/client.jar");

        Assert.True(written);
        Assert.Equal("https://example.com/client.jar", json.GetString("downloads.files[1].url"));
    }

    [Fact]
    public void Set_AllowsAssigningJsonUtilsAsNestedObject()
    {
        var json = JsonUtils.Parse("""{"launcher":{}}""");
        var nested = JsonUtils.Parse("""{"maxMemoryMb":4096,"showLog":true}""");

        var written = json.Set("launcher.profile", nested);

        Assert.True(written);
        Assert.Equal(4096, json.GetOrDefault("launcher.profile.maxMemoryMb", 0));
        Assert.True(json.GetOrDefault("launcher.profile.showLog", false));
    }

    [Fact]
    public void Delete_RemovesObjectPropertyButKeepsSiblingValues()
    {
        var json = JsonUtils.Parse("""{"launcher":{"java":"17","memory":"4G"}}""");

        var deleted = json.Delete("launcher.java");

        Assert.True(deleted);
        Assert.False(json.HasValue("launcher.java"));
        Assert.Equal("4G", json.GetString("launcher.memory"));
    }

    [Fact]
    public void Delete_RemovesArrayElementAndCompactsArray()
    {
        var json = JsonUtils.Parse("""{"files":[{"name":"a"},{"name":"b"},{"name":"c"}]}""");

        var deleted = json.Delete("files[1]");

        Assert.True(deleted);
        var files = json.GetArray<Dictionary<string, string>>("files");
        Assert.NotNull(files);
        Assert.Equal(2, files!.Count);
        Assert.Equal("a", files[0]["name"]);
        Assert.Equal("c", files[1]["name"]);
    }
}
