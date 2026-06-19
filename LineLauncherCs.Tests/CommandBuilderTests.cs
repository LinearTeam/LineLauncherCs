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

using LMCCore.Game.Launching.Steps.Arguments;

namespace LineLauncherCs.Tests;

public class CommandBuilderTests
{
    [Fact]
    public void AddJvmArgument_ReplacesDuplicateSystemProperty()
    {
        var builder = new CommandBuilder();

        builder.AddJvmArgument("-Dfoo=a");
        builder.AddJvmArgument("-Dfoo=b");

        Assert.Equal(["\"-Dfoo=b\""], builder.GetJvmArguments());
    }

    [Fact]
    public void GetJvmArguments_QuotesSystemPropertyArgumentsOnly()
    {
        var builder = new CommandBuilder();

        builder.AddJvmArgument("-Dfoo=bar");
        builder.AddJvmArgument("-Xmx2G");

        Assert.Equal(["\"-Dfoo=bar\"", "-Xmx2G"], builder.GetJvmArguments());
    }

    [Fact]
    public void AddJvmArgument_ReplacesDuplicateAdvancedJvmToggle()
    {
        var builder = new CommandBuilder();

        builder.AddJvmArgument("-XX:+UseG1GC");
        builder.AddJvmArgument("-XX:-UseG1GC");

        Assert.Equal(["-XX:-UseG1GC"], builder.GetJvmArguments());
    }

    [Fact]
    public void AddJvmArgument_ReplacesDuplicateAdvancedJvmOptionValue()
    {
        var builder = new CommandBuilder();

        builder.AddJvmArgument("-XX:MaxGCPauseMillis=100");
        builder.AddJvmArgument("-XX:MaxGCPauseMillis=200");

        Assert.Equal(["-XX:MaxGCPauseMillis=200"], builder.GetJvmArguments());
    }

    [Fact]
    public void AddJvmArgument_ReplacesDuplicateMemoryArgument()
    {
        var builder = new CommandBuilder();

        builder.AddJvmArgument("-Xmx1G");
        builder.AddJvmArgument("-Xmx2G");

        Assert.Equal(["-Xmx2G"], builder.GetJvmArguments());
    }

    [Fact]
    public void AddGameArguments_ReplacesExistingOptionValue()
    {
        var builder = new CommandBuilder();

        builder.AddGameArguments(["--username", "Steve"]);
        builder.AddGameArguments(["--username", "Alex"]);

        Assert.Equal(["--username", "Alex"], builder.GetGameArguments());
    }

    [Fact]
    public void AddGameArguments_PreservesFlagWithoutValue()
    {
        var builder = new CommandBuilder();

        builder.AddGameArguments(["--demo"]);

        Assert.Equal(["--demo"], builder.GetGameArguments());
    }

    [Fact]
    public void AddGameArguments_AllowsReplacingFlagWithValue()
    {
        var builder = new CommandBuilder();

        builder.AddGameArguments(["--demo"]);
        builder.AddGameArguments(["--demo", "true"]);

        Assert.Equal(["--demo", "true"], builder.GetGameArguments());
    }

    [Fact]
    public void AddGameArguments_AllowsReplacingValueWithFlagOnly()
    {
        var builder = new CommandBuilder();

        builder.AddGameArguments(["--foo", "bar"]);
        builder.AddGameArguments(["--foo"]);

        Assert.Equal(["--foo"], builder.GetGameArguments());
    }

    [Fact]
    public void AddGameArguments_AppendsPositionalArguments()
    {
        var builder = new CommandBuilder();

        builder.AddGameArguments(["--username", "Steve", "demoWorld"]);

        Assert.Equal(["--username", "Steve", "demoWorld"], builder.GetGameArguments());
    }

    [Fact]
    public void AddGameArguments_TreatsNextOptionAsMissingValue()
    {
        var builder = new CommandBuilder();

        builder.AddGameArguments(["--demo", "--username", "Steve"]);

        Assert.Equal(["--demo", "--username", "Steve"], builder.GetGameArguments());
    }
}
