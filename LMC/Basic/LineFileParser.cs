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

namespace LMC.Basic;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public class LineFileParser
{
    private readonly static Regex KeyValuePattern = new(@"\|(?<key>[^|]+)\|:\|(?<value>[^|]+)\|", RegexOptions.Compiled);

    public List<string> GetKeySet(string path, string section)
    {
        return ReadSectionEntries(path, section)
            .Select(entry => entry.Key)
            .Where(key => !string.IsNullOrEmpty(key))
            .ToList();
    }

    public List<string?> GetSections(string path)
    {
        var lines = ReadLines(path);
        var sections = new List<string?>();
        string? currentSection = null;

        foreach (var line in lines)
        {
            if (TryGetSectionMarker(line, "_start", out var startedSection))
            {
                currentSection = startedSection;
                continue;
            }

            if (TryGetSectionMarker(line, "_end", out var endedSection) &&
                string.Equals(currentSection, endedSection, StringComparison.Ordinal))
            {
                sections.Add(currentSection);
                currentSection = null;
            }
        }

        return sections;
    }

    public void DeleteSection(string path, string section)
    {
        var lines = ReadLines(path);
        var filteredLines = new List<string>(lines.Count);
        var startTag = GetSectionStartTag(section);
        var endTag = GetSectionEndTag(section);
        var inSection = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.Equals(trimmed, startTag, StringComparison.Ordinal))
            {
                inSection = true;
                continue;
            }

            if (string.Equals(trimmed, endTag, StringComparison.Ordinal))
            {
                inSection = false;
                continue;
            }

            if (!inSection)
            {
                filteredLines.Add(line);
            }
        }

        File.WriteAllLines(path, filteredLines.Where(line => !string.IsNullOrEmpty(line)));
    }

    public string? Read(string path, string key, string section)
    {
        foreach (var entry in ReadSectionEntries(path, section))
        {
            if (string.Equals(entry.Key, key, StringComparison.Ordinal))
            {
                return entry.Value;
            }
        }

        return null;
    }

    public void Write(string path, string key, string value, string section)
    {
        ValidateToken(nameof(key), key);
        ValidateToken(nameof(value), value);
        ValidateToken(nameof(section), section);

        EnsureFileExists(path);

        var lines = File.ReadAllLines(path).ToList();
        var startTag = GetSectionStartTag(section);
        var endTag = GetSectionEndTag(section);
        var inSection = false;
        var sectionFound = false;
        var keyFound = false;

        for (var index = 0; index < lines.Count; index++)
        {
            var trimmed = lines[index].Trim();
            if (string.Equals(trimmed, startTag, StringComparison.Ordinal))
            {
                inSection = true;
                sectionFound = true;
                continue;
            }

            if (string.Equals(trimmed, endTag, StringComparison.Ordinal))
            {
                inSection = false;
                if (!keyFound)
                {
                    lines.Insert(index, CreateEntryLine(key, value));
                    keyFound = true;
                }

                continue;
            }

            if (inSection && TryParseEntry(lines[index], out var existingKey, out _) &&
                string.Equals(existingKey, key, StringComparison.Ordinal))
            {
                lines[index] = CreateEntryLine(key, value);
                keyFound = true;
            }
        }

        if (!sectionFound)
        {
            lines.Add(startTag);
            lines.Add(CreateEntryLine(key, value));
            lines.Add(endTag);
        }

        File.WriteAllLines(path, lines);
    }

    public void Delete(string path, string key, string section)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var lines = File.ReadAllLines(path);
        var filteredLines = new List<string>(lines.Length);
        var startTag = GetSectionStartTag(section);
        var endTag = GetSectionEndTag(section);
        var inSection = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.Equals(trimmed, startTag, StringComparison.Ordinal))
            {
                inSection = true;
                filteredLines.Add(line);
                continue;
            }

            if (string.Equals(trimmed, endTag, StringComparison.Ordinal))
            {
                inSection = false;
                filteredLines.Add(line);
                continue;
            }

            if (inSection && TryParseEntry(line, out var existingKey, out _) &&
                string.Equals(existingKey, key, StringComparison.Ordinal))
            {
                continue;
            }

            filteredLines.Add(line);
        }

        File.WriteAllLines(path, filteredLines);
    }

    private static List<string> ReadLines(string path)
    {
        EnsureFileExists(path);
        return File.ReadAllLines(path).ToList();
    }

    private static void EnsureFileExists(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        var directory = Directory.GetParent(path)?.FullName
                        ?? throw new InvalidOperationException("Invalid path");
        Directory.CreateDirectory(directory);
        File.Create(path).Close();
    }

    private static IEnumerable<(string Key, string Value)> ReadSectionEntries(string path, string section)
    {
        var lines = ReadLines(path);
        var startTag = GetSectionStartTag(section);
        var endTag = GetSectionEndTag(section);
        var inSection = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.Equals(trimmed, startTag, StringComparison.Ordinal))
            {
                inSection = true;
                continue;
            }

            if (string.Equals(trimmed, endTag, StringComparison.Ordinal))
            {
                inSection = false;
                continue;
            }

            if (inSection && TryParseEntry(line, out var key, out var value))
            {
                yield return (key, value);
            }
        }
    }

    private static bool TryParseEntry(string line, out string key, out string value)
    {
        var match = KeyValuePattern.Match(line);
        if (match.Success)
        {
            key = match.Groups["key"].Value;
            value = match.Groups["value"].Value;
            return true;
        }

        key = string.Empty;
        value = string.Empty;
        return false;
    }

    private static bool TryGetSectionMarker(string line, string suffix, out string? section)
    {
        section = null;
        if (!line.StartsWith("|", StringComparison.Ordinal) || !line.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var markerBody = line[1..^suffix.Length];
        section = markerBody.EndsWith("|", StringComparison.Ordinal)
            ? markerBody[..^1]
            : markerBody;
        return true;
    }

    private static string GetSectionStartTag(string section) => $"|{section}|_start";

    private static string GetSectionEndTag(string section) => $"|{section}|_end";

    private static string CreateEntryLine(string key, string value) => $"|{key}|:|{value}|";

    private static void ValidateToken(string name, string value)
    {
        if (value.Contains('|'))
        {
            throw new ArgumentException($"{name} contain '|'", name);
        }
    }
}
