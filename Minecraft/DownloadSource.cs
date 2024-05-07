using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LMC.Minecraft
{
    /*
     * 
     * DownloadSource Class
     * SourceType: 0 = Official(Optifine is BMCLAPI) 1 = BMCLAPI 2 = LineMirror 3 = Custom
     * 
     */
    public class DownloadSource
    {
        private string _versionManifest = "https://piston-meta.mojang.com/mc/game/version_manifest.json";
        private string _versionManifestV2 = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
        private string _launcherMeta = "https://launchermeta.mojang.com/";
        private string _resourcesDownload = "http://resources.download.minecraft.net";
        private string _libraries = "https://libraries.minecraft.net/";
        private string _mojangJava = "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";
        private string _forge = "https://files.minecraftforge.net/maven";
        private string _optifineAPI = "https://bmclapi2.bangbang93.com/optifine/{mcversion}/{type}/{patch}";
        private string _authlibInjector = "https://authlib-injector.yushi.moe";
        private string _fabricMeta = "https://meta.fabricmc.net";
        private string _fabricMaven = "https://maven.fabricmc.net";
        private string _name = "Official";
        private int _sourceType = 0;
        

        public string VersionManifest { get => _versionManifest; set => _versionManifest = value; }
        public string VersionManifestV2 { get => _versionManifestV2; set => _versionManifestV2 = value; }
        public string LauncherMeta { get => _launcherMeta; set => _launcherMeta = value; }
        public string ResourcesDownload { get => _resourcesDownload; set => _resourcesDownload = value; }
        public string Libraries { get => _libraries; set => _libraries = value; }
        public string MojangJava { get => _mojangJava; set => _mojangJava = value; }
        public string Forge { get => _forge; set => _forge = value; }
        public string OptifineAPI { get => _optifineAPI; set => _optifineAPI = value; }
        public string AuthlibInjector { get => _authlibInjector; set => _authlibInjector = value; }
        public string FabricMeta { get => _fabricMeta; set => _fabricMeta = value; }
        public string FabricMaven { get => _fabricMaven; set => _fabricMaven = value; }
        public string Name { get => _name; set => _name = value; }
        public int SourceType { get => _sourceType; set => _sourceType = value; }

        public DownloadSource(
            string versionManifest = "https://piston-meta.mojang.com/mc/game/version_manifest.json",
            string versionManifestV2 = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json",
            string launcherMeta = "https://launchermeta.mojang.com/",
            string resourcesDownload = "http://resources.download.minecraft.net",
            string libraries = "https://libraries.minecraft.net/",
            string mojangJava = "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json",
            string forge = "https://files.minecraftforge.net/maven",
            string optifineAPI = "https://bmclapi2.bangbang93.com/optifine/{mcversion}/{type}/{patch}",
            string authlibInjector = "https://authlib-injector.yushi.moe",
            string fabricMeta = "https://meta.fabricmc.net",
            string fabricMaven = "https://maven.fabricmc.net",
            int sourceType = 0
        ){
            VersionManifest = versionManifest;
            VersionManifestV2 = versionManifestV2;
            LauncherMeta = launcherMeta;
            ResourcesDownload = resourcesDownload;
            Libraries = libraries;
            MojangJava = mojangJava;
            Forge = forge;
            OptifineAPI = optifineAPI;
            AuthlibInjector = authlibInjector;
            FabricMeta = fabricMeta;
            FabricMaven = fabricMaven;
            SourceType = sourceType;
        }
    }

}
