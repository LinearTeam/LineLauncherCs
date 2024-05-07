using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LMC.Basic
{
    
    public class LineFileParser
    {
        //|Key|:|Value|
        private const string Pattern = @"\|(?<key>[^|]+)\|:\|(?<value>[^|]+)\|";

        // ReadFile
        public string ReadFile(string path, string key, string category)
        {
            if (!File.Exists(path))
            {
                File.Create(path).Close();
                return null;
            }
            string startTag = $"|{category}|_start";
            string endTag = $"|{category}|_end";
            bool inCategory = false;
            string keyValue = null;

            var lines = File.ReadAllLines(path);

            foreach (var line in lines)
            {
                if (line.Trim() == startTag)
                {
                    inCategory = true;
                    continue;
                }
                if (line.Trim() == endTag)
                {
                    inCategory = false;
                    continue;
                }

                if (inCategory)
                {
                    var match = Regex.Match(line, Pattern);
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

        public void WriteFile(string path, string key, string value, string category)
        {
            if (value.Contains("|") || key.Contains("|") || category.Contains("|")){
                throw new Exception("key/value/category contain '|'");
            }
            string startTag = $"|{category}|_start";
            string endTag = $"|{category}|_end";
            bool inCategory = false;
            bool categoryFound = false;
            bool keyFound = false;
            var lines = File.ReadAllLines(path).ToList();

            if (!File.Exists(path))
            {
                File.Create(path).Close();
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim() == startTag)
                {
                    inCategory = true;
                    categoryFound = true;
                    continue;
                }
                if (lines[i].Trim() == endTag)
                {
                    inCategory = false;
                    if (!keyFound) //If didn't find key, then create.
                    {
                        lines.Insert(i, $"|{key}|:|{value}|");
                        keyFound = true;
                    }
                    continue;
                }

                if (inCategory)
                {
                    var match = Regex.Match(lines[i], Pattern);
                    if (match.Success && match.Groups["key"].Value == key)
                    {
                        //If found key, then update.
                        lines[i] = $"|{key}|:|{value}|";
                        keyFound = true;
                    }
                }
            }

            if (!categoryFound) //If didn't find category, then create new
            {
                lines.Add(startTag);
                lines.Add($"|{key}|:|{value}|");
                lines.Add(endTag);
            }

            File.WriteAllLines(path, lines); 
        }
    }
}