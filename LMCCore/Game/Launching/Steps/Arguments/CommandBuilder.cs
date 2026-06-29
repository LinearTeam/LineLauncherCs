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

namespace LMCCore.Game.Launching.Steps.Arguments;

public class CommandBuilder
{
    private readonly List<string> _jvmArguments = [];
    private readonly List<string> _gameArguments = [];

    public IReadOnlyList<string> GetJvmArguments()
    {
        return _jvmArguments
            .Select(QuoteSystemPropertyArgumentIfNeeded)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<string> GetGameArguments()
    {
        return _gameArguments
            .Select(QuoteGameArgumentIfNeeded)
            .ToList()
            .AsReadOnly();
    }
    public void AddJvmArgument(string arg)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arg);

        if (arg.StartsWith("-D"))
        {
            var key = GetSystemPropertyKey(arg);
            _jvmArguments.RemoveAll(existing => IsSameSystemProperty(existing, key));
            _jvmArguments.Add(arg);
            return;
        }

        if (arg.StartsWith("-XX:"))
        {
            var key = GetAdvancedJvmOptionKey(arg);
            _jvmArguments.RemoveAll(existing => IsSameAdvancedJvmOption(existing, key));
            _jvmArguments.Add(arg);
            return;
        }

        if (arg.StartsWith("-X"))
        {
            string[] memoryArgs = ["-Xmx", "-Xms", "-Xmn", "-Xss"];
            var key = memoryArgs.FirstOrDefault(arg.StartsWith!, null);
            if (key != null)
            {
                _jvmArguments.RemoveAll(s => s.StartsWith(key));
            }
        }
        _jvmArguments.RemoveAll(s => s.Equals(arg));
        _jvmArguments.Add(arg);
    }

    public void AddJvmArguments(ICollection<string> args)
    {
        foreach (var arg in args)
        {
            AddJvmArgument(arg);
        }
    }

    public void AddGameArguments(IList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        for (var i = 0; i < args.Count; i++)
        {
            var current = args[i];
            if (string.IsNullOrWhiteSpace(current))
            {
                continue;
            }

            if (!current.StartsWith("--"))
            {
                _gameArguments.Add(current);
                continue;
            }

            var hasValue = HasGameArgumentValue(args, i);
            RemoveGameArgument(current);
            _gameArguments.Add(current);

            if (!hasValue)
            {
                continue;
            }

            _gameArguments.Add(args[i + 1]);
            i++;
        }
    }

    private static bool HasGameArgumentValue(IList<string> args, int currentIndex)
    {
        if (currentIndex + 1 >= args.Count)
        {
            return false;
        }

        var next = args[currentIndex + 1];
        return !string.IsNullOrWhiteSpace(next) &&
               !next.StartsWith("--", StringComparison.Ordinal);
    }

    private static string GetSystemPropertyKey(string arg)
    {
        var index = arg.IndexOf('=');
        return index >= 0 ? arg[2..index] : arg[2..];
    }

    private static string QuoteSystemPropertyArgumentIfNeeded(string arg)
    {
        if (!arg.StartsWith("-D", StringComparison.Ordinal) && !arg.Contains(' '))
        {
            return arg;
        }

        return arg.Length >= 2 && arg[0] == '"' && arg[^1] == '"'
            ? arg
            : $"\"{arg}\"";
    }

    private static string QuoteGameArgumentIfNeeded(string arg)
    {
        if (!arg.Contains(' '))
        {
            return arg;
        }

        return arg.Length >= 2 && arg[0] == '"' && arg[^1] == '"'
            ? arg
            : $"\"{arg}\"";
    }

    private static bool IsSameSystemProperty(string existing, string key)
    {
        if (!existing.StartsWith("-D", StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(GetSystemPropertyKey(existing), key, StringComparison.Ordinal);
    }

    private static string GetAdvancedJvmOptionKey(string arg)
    {
        if (arg.StartsWith("-XX:+", StringComparison.Ordinal) ||
            arg.StartsWith("-XX:-", StringComparison.Ordinal))
        {
            return arg[5..];
        }

        var index = arg.IndexOf('=');
        return index >= 0 ? arg[4..index] : arg[4..];
    }

    private static bool IsSameAdvancedJvmOption(string existing, string key)
    {
        if (!existing.StartsWith("-XX:", StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(GetAdvancedJvmOptionKey(existing), key, StringComparison.Ordinal);
    }

    private void RemoveGameArgument(string key)
    {
        for (var index = 0; index < _gameArguments.Count; index++)
        {
            if (!string.Equals(_gameArguments[index], key, StringComparison.Ordinal))
            {
                continue;
            }

            _gameArguments.RemoveAt(index);
            if (index < _gameArguments.Count && !_gameArguments[index].StartsWith("--", StringComparison.Ordinal))
            {
                _gameArguments.RemoveAt(index);
            }

            index--;
        }
    }
}
