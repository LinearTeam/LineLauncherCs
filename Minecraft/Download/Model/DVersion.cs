namespace LMC.Minecraft.Download.Model
{
    public class DVersion
    {
        public string Id { get; set; }
        public int Type { get; set; } // 0 - release, 1 - snapshot, 2 - old-beta, 3 - old-alpha
        public string Url { get; set; }
        public string UpdateTime { get; set; }
        public string ReleaseTime { get; set; }

        public DVersion(string id, int type, string url, string updateTime, string releaseTime)
        {
            Id = id;
            Type = type;
            Url = url;
            UpdateTime = updateTime;
            ReleaseTime = releaseTime;
        }
    }
}
