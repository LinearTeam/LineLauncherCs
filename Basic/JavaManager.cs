#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Threading;
using LMC.Basic.Configs;
using LMC.Basic.Exceptions;
using LMC.Basic.Model;
using LMC.Utils;

namespace LMC.Basic
{
    public class JavaManager
    {
        private static Logger s_logger = new Logger("JM");
        
        private static Dictionary<string, Java> s_javaList = new Dictionary<string, Java>();
        private static Java? s_selectedJava;
        
        public static bool SearchingJava { get; set; } = false;

        public static List<Java> GetJavaList()
        {
            return s_javaList.Values.ToList();
        }
        
        public static Java? GetJavaWithMinVersion(Version minVersion)
        {
            if (s_selectedJava != null && VersionUtils.BiggerOrEqual(s_selectedJava.Version, minVersion))
            {
                return s_selectedJava;
            }
            Java? foundJava = null;
            s_javaList.ForEach(j =>
            {
                if(VersionUtils.BiggerOrEqual(j.Value.Version, minVersion)) foundJava = j.Value;
            });
            return foundJava;
        }

        public static Java? GetJavaWithMaxVersion(Version maxVersion)
        {
            if (s_selectedJava != null && VersionUtils.SmallerOrEqual(s_selectedJava.Version, maxVersion))
            {
                return s_selectedJava;
            }
            Java? foundJava = null;
            s_javaList.ForEach(j =>
            {
                if(VersionUtils.SmallerOrEqual(j.Value.Version, maxVersion)) foundJava = j.Value;
            });
            return foundJava;
        }

        public static Java? GetJavaBetween(Version minVersion, Version maxVersion)
        {
            if(s_selectedJava != null && VersionUtils.Between(s_selectedJava.Version, minVersion, maxVersion))
            {
                return s_selectedJava;
            }
            Java? foundJava = null;
            s_javaList.ForEach(j => foundJava = VersionUtils.Between(j.Value.Version, minVersion, maxVersion) ? j.Value : foundJava);
            return foundJava;
        }

        public static Java? GetSelectedJava()
        {
            return s_selectedJava;
        }
        public static void SetSelectedJava(string path)
        {
            Config.WriteGlobal("java", "selected", path);
        }
        
        public static void DeleteUserJava(string path)
        {
            s_logger.Info($"删除用户java: {path}");
            Config.DeleteGlobal("javas", path);
            if (s_selectedJava.Path == path)
            {
                s_selectedJava = null;
                Config.WriteGlobal("java", "selected", "auto");
            }
            RefreshUserJava();
        }

        public static void AddUserJava(string java)
        {
            s_logger.Info("添加用户java: " + java);
            java = Path.GetFullPath(java);
            EnsureJava(java);
            s_logger.Info("用户java: " + java);
            var javainfo = GetJavaInfo(java);
            s_logger.Info("Java " + java + $"有效\nVer: {javainfo.version}\nImpl: {javainfo.implementor}\nArch: {javainfo.arch}");
            Java j = new Java(Path.GetFullPath(java), File.Exists(Path.Combine(Path.GetDirectoryName(java), "javac.exe")), javainfo.version, javainfo.arch, javainfo.implementor, true);
            Config.WriteGlobal("javas", java, "true");
            Config.WriteGlobal(java, "version", j.Version.ToString());
            Config.WriteGlobal(java, "arch", j.Arch);
            Config.WriteGlobal(java, "implementor", j.Implementor);
            Config.WriteGlobal(java, "isJre", j.IsJre.ToString());
        }
        
        //TODO: 灵活选择搜索方式
        public static async Task SearchJava(int depth, CancellationTokenSource ctx = null)
        {
            SearchingJava = true;
            await Task.Run(() => SearchJavaPrivate(depth, ctx));
            SearchingJava = false;
        }
        private static async Task SearchJavaPrivate(int depth, CancellationTokenSource ctx = null)
        {
            ctx ??= new CancellationTokenSource();
            s_logger.Info("正在搜索java :" + depth);
            string command =
                $"-command \"Get-ChildItem -Path (Get-PSDrive -PSProvider FileSystem | Select-Object -ExpandProperty Root) -Recurse -Depth {depth} -Filter java.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName\"";
            string fileName = "powershell";
            Process process = new Process();
            ProcessStartInfo psi = new ProcessStartInfo(fileName);
            psi.Arguments = command;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo = psi;
            process.Start();
            while (!process.HasExited && !ctx.IsCancellationRequested)
            {
                if (ctx.IsCancellationRequested && !process.HasExited)
                {
                    process.Kill();
                    return;
                }
                await Task.Delay(500);
            }
            string result = process.StandardOutput.ReadToEnd();
            var arr  = result.Trim().Split('\n');
            var finalJava = new List<Java>();
            foreach (var javaI in arr)
            {
                if (ctx.IsCancellationRequested) return;
                var java = javaI.Replace("\r", "").Replace("\n", "");
                s_logger.Info("找到Java " + java);
                try
                {
                    EnsureJava(java);
                    var javainfo = GetJavaInfo(java);
                    s_logger.Info("Java " + java + $"有效\nVer: {javainfo.version}\nImpl: {javainfo.implementor}\nArch: {javainfo.arch}");
                    Java j = new Java(Path.GetFullPath(java), File.Exists(Path.Combine(Path.GetDirectoryName(java), "javac.exe")), javainfo.version, javainfo.arch, javainfo.implementor, false);
                    finalJava.Add(j);
                }
                catch (InvalidJavaException e)
                {
                    s_logger.Error(e, "搜索Java");
                }
                catch (Exception e)
                {
                    s_logger.Error("搜索Java时发生未知异常：\n" + e.Message + "\n" + e.StackTrace);
                }
            }
            finalJava.ForEach(j => s_javaList[j.Path] = s_javaList.ContainsKey(j.Path) ? s_javaList[j.Path] : j);
            s_selectedJava = null;
            if (ctx.IsCancellationRequested) return;
            RefreshUserJava(ctx);
        }

        public static void RefreshUserJava(CancellationTokenSource ctx = null)
        {
            ctx ??= new CancellationTokenSource();
            var userJavas = Config.ReadKeySetGlobal("javas");
            foreach (var j in userJavas)
            {
                if (ctx.IsCancellationRequested) return;
                try
                {
                    var ver = new Version(Config.ReadGlobal(j, "version"));
                    var arch = Config.ReadGlobal(j, "arch");
                    var implementor = Config.ReadGlobal(j, "implementor");
                    var isJre = Boolean.Parse(Config.ReadGlobal(j, "isJre"));
                    Java java = new Java(j, isJre, ver, arch, implementor, true);
                    s_javaList[j] = java;
                }
                catch (Exception e)
                {
                    s_logger.Error(new InvalidJavaException(j, e.ToString()), "读取用户Java");
                }
            }
            var selected = Config.ReadGlobal("java","selected");
            if (string.IsNullOrEmpty(selected) || selected == "auto")
            {
                s_selectedJava = null;
            }
            else
            {
                s_selectedJava = s_javaList.TryGetValue(selected, out var value) ? value : null;
            }
        }
        
        public static (Version version, string arch, string implementor) GetJavaInfo(string javaExe)
        {

            #region ViaReleaseFile
            
            var lines = File.ReadAllLines(Path.Combine(Directory.GetParent(javaExe).Parent.FullName, "release"));
            string implementor = "";
            string arch = "";
            Version version = null;
            foreach (var line in lines)
            {
                var l = line.Replace("=", ":");
                if (l.StartsWith("IMPLEMENTOR:"))
                {
                    implementor = l.Replace("IMPLEMENTOR:", "").Replace("\"", "");
                    continue;
                }

                if (l.StartsWith("JAVA_VERSION:"))
                {
                    version = new Version(l.Replace("JAVA_VERSION:", "").Replace("\"", ""));
                    continue;
                }

                if (l.StartsWith("OS_ARCH:"))
                {
                    arch = l.Replace("OS_ARCH:", "").Replace("\"", "");
                }
            }
            
            if(version != null) return (version, arch, implementor);
            
            #endregion

            #region ViaFileInfo
            
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(javaExe);
            version = new Version(fvi.FileVersion);
            
            try
            {
                var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(javaExe));
                implementor = cert.Subject;
            }
            catch (CryptographicException)
            {
                throw new InvalidJavaException(javaExe, "文件签名无效。");
            }
            
            #endregion

            return (version, arch, implementor);
        }

        public static void EnsureJava(string java)
        {
            if (java.ToUpper().Contains("C:") && java.ToLower().Contains("oracle/java/"))
            {
                throw new InvalidJavaException(java, "疑似软链接");
            }

            var javaDir = Directory.GetParent(java).Parent.FullName;
            var checkPaths = new[]{
                javaDir,
                Path.Combine(javaDir, @"bin\java.exe"),
                Path.Combine(javaDir, @"bin\javaw.exe"),
                Path.Combine(javaDir, @"release")
            };
            checkPaths.ForEach(p =>
            {
                if (!(File.Exists(p) || Directory.Exists(p)))
                {
                    throw new InvalidJavaException(java, "目录/文件 " + p + " 不存在");
                }
            });
        }
    }
}