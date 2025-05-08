using System;
using System.Collections.Generic;

namespace LMC.Basic.Config
{
    public class Config
    {
        public static readonly string GlobalPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/.linelauncher/global.line";
        public static readonly string LocalPath = "./LMC/config.line";
        private static LineFileParser s_lineFileParser = new LineFileParser();
        private static Logger s_logger = new Logger("ST");
        public static void WriteGlobal(string section, string key, string value)
        {
            s_lineFileParser.Write(GlobalPath, key, value, section);
        }
        public static string ReadGlobal(string section, string key)
        {
            return s_lineFileParser.Read(GlobalPath, key, section);
        }

        public static void DeleteGlobal(string section, string key)
        {
            s_lineFileParser.Delete(GlobalPath, key, section);
        }
        
        public static void Write(string section, string key, string value)
        {
            s_lineFileParser.Write(LocalPath, key, value, section);
        }
        public static string Read(string section, string key)
        {
            return s_lineFileParser.Read(LocalPath, key, section);
        }
        
        public static void Delete(string section, string key)
        {
            s_lineFileParser.Delete(LocalPath, key, section);
        }

        public static List<string> ReadKeySet(string section)
        {
            return s_lineFileParser.GetKeySet(LocalPath, section);
        }
        public static List<string> ReadKeySetGlobal(string section)
        {
            return s_lineFileParser.GetKeySet(GlobalPath, section);
        }

    }
}
