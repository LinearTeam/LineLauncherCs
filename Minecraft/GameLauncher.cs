﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using LMC.Basic;
using LMC.Utils;

namespace LMC.Minecraft
{
    public class JvmCommand
    {
        private string _literalCommand;
        public void AppendExpressionPair(string key, string value)
        {
            _literalCommand += $"-{key}=\"{value}\" ";
        }

        public string GetLiteralCommand()
        {
            if (_literalCommand != null)
            {
                return _literalCommand;
            }
            else
            {
                return string.Empty;
            }
        }

        public void GenerateMemoryArgs(string maxMemorySize)
        {
            _literalCommand += $"-Xmx{maxMemorySize}M ";
        }

        public void MakeUpGeneralJvmArg(string targetJava)
        {
            _literalCommand += $"\"{targetJava}\" -XX:+UseG1GC -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow ";
        }
    }
    public class MinecraftCommand
    {
        private string _literalCommand;
        public void Append(string key, string value)
        {
            _literalCommand += $"--{key} \"{value}\" ";
        }

        public string GetLiteralCommand()
        {
            if (_literalCommand != null)
            {
                return _literalCommand;
            }
            else
            {
                return string.Empty;
            }
        }
    }
    public class GameLauncher {

        private readonly string _mcDir;
        private readonly string _mcVer;
        private readonly string _mcJava;
        private readonly string _maxMem;
        private readonly Dictionary<string, dynamic> _userInfo;
        private readonly Dictionary<string, dynamic> _windowArgs;
        private readonly Dictionary<string, dynamic> _extendArgs;
        private readonly MinecraftCommand _minecraftCommandLine = new MinecraftCommand();
        private readonly JvmCommand _jvmCommandLine = new JvmCommand();
        private static Logger s_logger = new Logger("GL");

        public GameLauncher(
        string minecraftDirectory, string minecraftVersion,
        string targetJava, Dictionary<string, dynamic> userInformation,
        string maxMemory, Dictionary<string, dynamic> windowArguments,
        Dictionary<string, dynamic> extendArguments)
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
            string jsonstring = File.ReadAllText(filePath);
            string classPathArgs = "-cp ";

            _minecraftCommandLine.Append("version", _mcVer);
            _minecraftCommandLine.Append("assetIndex", JsonUtils.GetValueFromJson(jsonstring, "assets"));
            _minecraftCommandLine.Append("assetsIndex", JsonUtils.GetValueFromJson(jsonstring, "assets"));
            _minecraftCommandLine.Append("assetsDir", $"{_mcDir}/assets");
            _minecraftCommandLine.Append("gameDir", $"{_mcDir}/versions/{_mcVer}");

            string librariesStr = JsonUtils.GetValueFromJson(jsonstring, "libraries");
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
            classPathArgs += $"\"{_mcDir}/versions/{_mcVer}/{_mcVer}.jar\" {JsonUtils.GetValueFromJson(jsonstring, "mainClass")} ";
            return classPathArgs;
        }

        public void OrganizeArgs()
        {
            GenerateUserInfo();
            GenerateWindowArgs();
            GenerateJvmArgs();
            string totalCommand = _jvmCommandLine.GetLiteralCommand() + GenerateClassPaths() + _minecraftCommandLine.GetLiteralCommand();
            File.WriteAllText("./LMC/LatestLaunch.bat", totalCommand);
            string fullPath = Directory.GetParent("./LMC/LatestLaunch.bat").FullName + "\\LatestLaunch.bat";
            Process.Start(fullPath);
        }
    }

    public class Test
    {
        public static void STest()
        {
            Dictionary<string, dynamic> a = new Dictionary<string, dynamic>();
            Dictionary<string, dynamic> b = new Dictionary<string, dynamic>();
            Dictionary<string, dynamic> c = null;
            a["userType"] = "Legacy";
            a["username"] = "Dongxuelian";
            a["accessToken"] = "3153145";
            b["width"] = "873";
            b["height"] = "508";
            GameLauncher example = new GameLauncher("D:/LMC/.minecraft", "1.12.2", "E:\\Java22\\bin\\java.exe", a, "4096", b, c);
            example.OrganizeArgs();
        }
    }
}