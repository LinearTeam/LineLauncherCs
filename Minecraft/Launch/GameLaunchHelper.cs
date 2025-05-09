using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using LMC.Account;
using LMC.Basic;
using LMC.Basic.Model;
using LMC.Minecraft.Profile;
using LMC.Minecraft.Profile.Model;
using LMC.Tasks;

namespace LMC.Minecraft.Launch
{
    public class GameLaunchHelper
    {
        private static Logger s_logger = new Logger("GLH");

        public async void LaunchGame(LocalProfile profile)
        {
            if (profile.Status != ProfileStatus.Normal)
            {
                return;
            }

            var task = TaskManager.Instance.CreateTask(3, "启动游戏 " + profile.Name + $"({profile.Version} {profile.ModLoader.ModLoaderType} {profile.ModLoader.LoaderVersion})");
            GameLauncher gl = new GameLauncher();
            gl.GamePath = profile.GamePath.Path;
            var root = gl.SubTaskForLaunchGame(profile.Name, task.Id, 0);
            TaskManager.Instance.AddSubTask(task.Id, ++root, async ctx =>
            {
                Process p = new Process();
                var major = TaskManager.Values[task.Id]["major"] as string;
                var java = JavaManager.GetJavaWithMinVersion(new Version(int.Parse(major), 0));
                if (java == null) throw new Exception("无可用的Java");
                var launchArgs = TaskManager.Values[task.Id]["launchArgs"] as string;
                s_logger.Info($"游戏使用的Java {java.Version} : " + java.Path);
                s_logger.Info($"启动参数:\n{launchArgs}");
                ProcessStartInfo psi = new ProcessStartInfo(java.Path);
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = Path.Combine(profile.GamePath.Path, "versions", profile.Name);
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                p.StartInfo = psi;
                s_logger.Info("正在启动游戏");
                p.Start();
                p.OutputDataReceived += (sender, args) =>
                {
                    s_logger.Info("[Minecraft]" + args.Data);
                };
            }, "启动游戏");
            task.Status = ExecutionStatus.Waiting;
            Task.Run(() => TaskManager.Instance.ExecuteTasksAsync()).ConfigureAwait(false);
        }
    }
}