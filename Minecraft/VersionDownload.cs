using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LMC.Minecraft
{
    public class VersionDownload
    {
        public String id;
        public int type; //0 - normal 1 - snapshot 2 - old-beta 3 - old-alpha
        public String url;
        public String updateTime;
        public String releaseTime;
        bool latest;


        public VersionDownload(String id, int type, String url, String updateTime, String releaseTime, bool latest) { 
            this.id = id;
            this.type = type;
            this.url = url;
            this.updateTime = updateTime;
            this.releaseTime = releaseTime;
            this.latest = latest;
        }

    }
}
