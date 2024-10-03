using System;
using System.Collections.Generic;

namespace LMC.Basic
{
    public class Config
    {
        private static string s_globalPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/.linelauncher/global.line";
        private static string s_localPath = "./LMC/config.line";
        private static LineFileParser s_lineFileParser = new LineFileParser();
        private static Logger s_logger = new Logger("ST");
        public static void WriteGlobal(string section, string key, string value)
        {
            s_lineFileParser.Write(s_globalPath, key, value, section);
        }
        public static string ReadGlobal(string section, string key)
        {
            return s_lineFileParser.Read(s_globalPath, key, section);
        }
        public static void Write(string section, string key, string value)
        {
            s_lineFileParser.Write(s_localPath, key, value, section);
        }
        public static string Read(string section, string key)
        {
            return s_lineFileParser.Read(s_localPath, key, section);
        }
        public static List<string> ReadKeySet(string section)
        {
            return s_lineFileParser.GetKeySet(s_localPath, section);
        }
        public static List<string> ReadKeySetGlobal(string section)
        {
            return s_lineFileParser.GetKeySet(s_globalPath, section);
        }

    }
}
