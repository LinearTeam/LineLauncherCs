using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Shapes;
using LMC.Basic;
using LMC.Utils;

namespace LMC.Minecraft
{
    public class JvmCommand
    {
        private String? literalCommand;
        public void AppendExpressionPair(String key, String value)
        {
            literalCommand += $"-{key}=\"{value}\" ";
        }

        public String GetLiteralCommand()
        {
            if (literalCommand != null)
            {
                return literalCommand;
            }
            else
            {
                return string.Empty;
            }
        }

        public void GenerateMemoryArgs(String maxMemorySize)
        {
            literalCommand += $"-Xmn645m -Xmx{maxMemorySize}m ";
        }

        public void MakeUpGeneralJvmArg(string targetJava)
        {
            literalCommand += $"\"{targetJava}\" -XX:+UseG1GC -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow ";
        }
    }
    public class MinecraftCommand
    {
        private String? literalCommand;
        public void Append(String key, String value)
        {
            literalCommand += $"--{key} \"{value}\" ";
        }

        public String GetLiteralCommand()
        {
            if (literalCommand != null)
            {
                return literalCommand;
            }
            else
            {
                return string.Empty;
            }
        }
    }
    public class GameInstanceLauncher {

        private readonly string? _mcDir;
        private readonly string? _mcVer;
        private readonly string? _mcJava;
        private readonly string? _maxMem;
        private readonly Dictionary<String, dynamic>? _userInfo;
        private readonly Dictionary<String, dynamic>? _windowArgs;
        private readonly Dictionary<String, dynamic>? _extendArgs;
        private readonly MinecraftCommand _minecraftCommandLine = new MinecraftCommand();
        private readonly JvmCommand _jvmCommandLine = new JvmCommand();
        private static Logger s_logger = new Logger("GIL");

        public GameInstanceLauncher(
        String? minecraftDirectory, String? minecraftVersion,
        String? targetJava, Dictionary<String, dynamic>? userInformation,
        String? maxMemory, Dictionary<String, dynamic>? windowArguments,
        Dictionary<String, dynamic>? extendArguments)
        {
            _mcDir = minecraftDirectory;
            _userInfo = userInformation;
            _mcVer = minecraftVersion;
            _mcJava = targetJava;
            _userInfo = userInformation;
            _maxMem = maxMemory;
            _extendArgs = extendArguments;
            _windowArgs = windowArguments;
        }


        public void GenerateUserInfo()
        {
            if (this._userInfo != null)
            {
                foreach (dynamic i in this._userInfo)
                {
                    _minecraftCommandLine.Append(i.Key, i.Value);

                }
            }
        }

        public void GenerateWindowArgs()
        {
            if (this._windowArgs != null)
            {
                foreach (dynamic i in this._windowArgs)
                {
                    _minecraftCommandLine.Append(i.Key, i.Value);
                }
            }
        }

        public void GenerateJvmArgs()
        {
            _jvmCommandLine.MakeUpGeneralJvmArg(_mcJava);
            _jvmCommandLine.GenerateMemoryArgs(_maxMem);
            _jvmCommandLine.AppendExpressionPair("Dos.name", "Windows 10");
            _jvmCommandLine.AppendExpressionPair("Dos.version", "10.0");
            _jvmCommandLine.AppendExpressionPair("Dorg.lwjgl.util.DebugLoader", "true");
            _jvmCommandLine.AppendExpressionPair("Dorg.lwjgl.util.Debug", "true");
            _jvmCommandLine.AppendExpressionPair("Dos.version", "10.0");
            _jvmCommandLine.AppendExpressionPair("Dminecraft.launcher.brand", "LMC");
            _jvmCommandLine.AppendExpressionPair("Dminecraft.launcher.version", "");
            _jvmCommandLine.AppendExpressionPair("Djava.library.path", $"{_mcDir}/versions/{_mcVer}/{_mcVer}-natives");
        }

        public string GenerateClassPaths()
        {
            string filePath = $"{_mcDir}/versions/{_mcVer}/{_mcVer}.json";
            string jsonString = File.ReadAllText(filePath);
            string classPathArgs = "-cp ";

            _minecraftCommandLine.Append("version", _mcVer);
            _minecraftCommandLine.Append("assetIndex", JsonUtils.GetValueFromJson(jsonString, "assets"));
            _minecraftCommandLine.Append("assetsIndex", JsonUtils.GetValueFromJson(jsonString, "assets"));
            _minecraftCommandLine.Append("assetsDir", $"{_mcDir}/assets/objects");
            _minecraftCommandLine.Append("gameDir", $"{_mcDir}/versions/{_mcVer}");

            string librariesStr = JsonUtils.GetValueFromJson(jsonString, "libraries");
            JsonArray larr = JsonArray.Parse(librariesStr).AsArray();
            foreach (var lib in larr)
            {
                string url;
                string path;
                string libStr = lib.ToJsonString();
                bool windows = false;
                if (JsonUtils.GetValueFromJson(libStr, "rules") != null)
                {
                    string rules = JsonUtils.GetValueFromJson(libStr, "rules");
                    JsonArray rarr = JsonArray.Parse(rules).AsArray();
                    foreach (var rule in rarr)
                    {
                        string ruleStr = rule.ToJsonString();
                        if (JsonUtils.GetValueFromJson(ruleStr, "os.name") == "windows" && JsonUtils.GetValueFromJson(ruleStr, "action") == "allow")
                        {
                            windows = true;
                            break;
                        }
                        if (JsonUtils.GetValueFromJson(ruleStr, "os.name") == "windows" && JsonUtils.GetValueFromJson(ruleStr, "action") == "disallow")
                        {
                            windows = false;
                            break;
                        }
                        if (JsonUtils.GetValueFromJson(ruleStr, "os.name") != "windows" && JsonUtils.GetValueFromJson(ruleStr, "action") == "disallow")
                        {
                            windows = true;
                            continue;
                        }
                    }

                }
                else { windows = true; }

                if (windows)
                {
                    path = $"{_mcDir}/libraries/{JsonUtils.GetValueFromJson(libStr, "downloads.artifact.path")}";
                    if (path.Contains("arm64")) continue;
                    classPathArgs += $"\"{path}\";";
                }
                if (JsonUtils.GetValueFromJson(libStr, "downloads.natives.windows") != null)
                {
                    libStr = JsonUtils.GetValueFromJson(libStr, $"downloads.classifiers.{JsonUtils.GetValueFromJson(libStr, "downloads.natives.windows")}");
                    path = $"{_mcDir}/libraries/{JsonUtils.GetValueFromJson(libStr, "path")}";
                    if (path.Contains("arm64")) continue;
                    classPathArgs += $"\"{path}\";";
                }

            }
            classPathArgs += $"\"{_mcDir}/versions/{_mcVer}/{_mcVer}.jar\" {JsonUtils.GetValueFromJson(jsonString, "mainClass")} ";
            return classPathArgs;
        }

        public void organizeArgs()
        {
            GenerateUserInfo();
            GenerateWindowArgs();
            GenerateJvmArgs();
            string totalCommand = _jvmCommandLine.GetLiteralCommand() + GenerateClassPaths() + _minecraftCommandLine.GetLiteralCommand();
        }
        public void p()
        {
            s_logger.Info(_jvmCommandLine.GetLiteralCommand() + _minecraftCommandLine.GetLiteralCommand());
        }
    }

    public class Test
    {
        public static void STest()
        {
            Dictionary<string, dynamic>? a = new Dictionary<string, dynamic>();
            Dictionary<string, dynamic>? b = new Dictionary<string, dynamic>();
            Dictionary<string, dynamic>? c = null;
            a["userType"] = "Legacy";
            a["username"] = "Dongxuelian";
            a["accessToken"] = "3153145";
            b["width"] = "873";
            b["height"] = "508";
            GameInstanceLauncher example = new GameInstanceLauncher("D:/HMCL/.minecraft", "1.21.1", "E:\\Java22\\bin\\java.exe", a, "4096", b, c);
            example.organizeArgs();
        }
    }
}