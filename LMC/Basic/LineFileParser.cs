namespace LMC.Config;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public class LineFileParser
{
    //|Key|:|Value|
    private const string _pattern = @"\|(?<key>[^|]+)\|:\|(?<value>[^|]+)\|";

    public List<string> GetKeySet(string path, string section)
    {
        var res = new List<string>();
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Directory.GetParent(path).FullName);
            File.Create(path).Close();
            return res;
        }

        string startTag = $"|{section}|_start";
        string endTag = $"|{section}|_end";
        bool inSection = false;

        var lines = File.ReadAllLines(path);

        foreach (var line in lines)
        {
            if (line.Trim() == startTag)
            {
                inSection = true;
                continue;
            }

            if (line.Trim() == endTag)
            {
                inSection = false;
                continue;
            }

            if (inSection)
            {
                var match = Regex.Match(line, _pattern);
                if (match.Success && !string.IsNullOrEmpty(match.Groups["key"].Value))
                {
                    res.Add(match.Groups["key"].Value);
                }
            }
        }

        return res;
    }

    public List<string> GetSections(string path)
    {
        var lines = File.ReadAllLines(path);
        List<string> res = new List<string>();
        string section = null;
        foreach (string line in lines)
        {
            if (line.StartsWith("|") && line.EndsWith("|_start"))
            {
                section = line.Substring(1).Replace("|_start", "");
            }

            if (line.StartsWith("|") && line.EndsWith("|_end"))
            {
                if (line.Substring(1).Replace("|_end", "").Equals(section))
                {
                    res.Add(section);
                    section = null;
                }
            }
        }

        return res;
    }

    public void DeleteSection(string path, string section)
    {
        var lines = File.ReadAllLines(path);
        string[] totalLines = new string[lines.Length];
        bool inSection = false;
        int i = 0;
        foreach (string line in lines)
        {
            if (line.StartsWith("|") && line.EndsWith("|_start"))
            {
                if (line.Substring(1).Replace("|_start", "") == section)
                {
                    inSection = true;
                    continue;
                }
            }

            if (line.StartsWith("|") && line.EndsWith("|_end"))
            {
                if (line.Substring(1).Replace("|_end", "").Equals(section))
                {
                    inSection = false;
                    continue;
                }
            }

            if (inSection)
            {
                continue;
            }

            totalLines[i] = line;
            i++;
        }

        int length = 1;
        foreach (var line in totalLines)
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            length++;
        }

        string[] reallyTotalLines = new string[length];
        i = 0;
        foreach (var line in totalLines)
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            reallyTotalLines[i] = line;
            i++;
        }

        File.WriteAllLines(path, reallyTotalLines);

    }

    // ReadFile
    public string Read(string path, string key, string section)
    {
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Directory.GetParent(path).FullName);
            File.Create(path).Close();
            return null;
        }

        string startTag = $"|{section}|_start";
        string endTag = $"|{section}|_end";
        bool insection = false;
        string keyValue = null;

        var lines = File.ReadAllLines(path);

        foreach (var line in lines)
        {
            if (line.Trim() == startTag)
            {
                insection = true;
                continue;
            }

            if (line.Trim() == endTag)
            {
                insection = false;
                continue;
            }

            if (insection)
            {
                var match = Regex.Match(line, _pattern);
                if (match.Success && match.Groups["key"].Value == key)
                {
                    keyValue = match.Groups["value"].Value;
                    break;
                }
            }
        }

        return keyValue;
    }

    // WriteFile

    public void Write(string path, string key, string value, string section)
    {
        if (value.Contains("|") || key.Contains("|") || section.Contains("|"))
        {
            throw new Exception("key/value/section contain '|'");
        }

        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Directory.GetParent(path).FullName);
            File.Create(path).Close();
        }

        string startTag = $"|{section}|_start";
        string endTag = $"|{section}|_end";
        bool insection = false;
        bool sectionFound = false;
        bool keyFound = false;
        var lines = File.ReadAllLines(path).ToList();

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Trim() == startTag)
            {
                insection = true;
                sectionFound = true;
                continue;
            }

            if (lines[i].Trim() == endTag)
            {
                insection = false;
                if (!keyFound) //If didn't find key, then create.
                {
                    lines.Insert(i, $"|{key}|:|{value}|");
                    keyFound = true;
                }

                continue;
            }

            if (insection)
            {
                var match = Regex.Match(lines[i], _pattern);
                if (lines[i].StartsWith($"|{key}|:|") && lines[i].EndsWith($"|"))
                {
                    //If found key, then update.    
                    lines[i] = $"|{key}|:|{value}|";
                    keyFound = true;
                }
            }
        }

        if (!sectionFound) //If didn't find section, then create new
        {
            lines.Add(startTag);
            lines.Add($"|{key}|:|{value}|");
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

        string startTag = $"|{section}|_start";
        string endTag = $"|{section}|_end";
        bool insection = false;
        var lines = File.ReadAllLines(path).ToList();

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Trim() == startTag)
            {
                insection = true;
                continue;
            }

            if (lines[i].Trim() == endTag)
            {
                insection = false;
                continue;
            }

            if (insection)
            {
                var match = Regex.Match(lines[i], _pattern);
                if (match.Success && match.Groups["key"].Value == key)
                {
                    lines.RemoveAt(i);
                }
            }
        }

        File.WriteAllLines(path, lines);
    }

}