using LMC.Basic;
using LMC.Minecraft.Profile;
using LMC.Minecraft.Profile.Model;
using LMC.Tasks;

namespace LMC.Minecraft.Launch
{
    public class GameLaunchHelper
    {
        private static Logger s_logger = new Logger("GL");
        public void LaunchGame(LocalProfile profile)
        {
            if (profile.Status != ProfileStatus.Normal)
            {
                return;
            }
            if (profile.ModLoader.ModLoaderType == ModLoaderType.Vanilla)
            {
                var task = TaskManager.Instance.CreateTask(3, "启动原版游戏 " + profile.Name + $"({profile.Version})");
                TaskManager.Instance.AddSubTask(task.Id, 0, async token =>
                {

                }, "");
            }
        }
    }
}