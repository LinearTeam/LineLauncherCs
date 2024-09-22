using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace LMC.Minecraft
{
    /*
     * 
     * SourceType: 0 = Official(Optifine is BMCLAPI) 1 = BMCLAPI 2 = LineMirror 3 = Custom
     * 
     */
    public class DownloadSource
    {
        private string _versionManifest = "https://piston-meta.mojang.com/mc/game/version_manifest.json";
        private string _versionManifestV2 = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
        private string _launcherMeta = "https://launchermeta.mojang.com/";
        private string _launcher = "https://launcher.mojang.com/";
        private string _resourcesDownload = "https://resources.download.minecraft.net";
        private string _libraries = "https://libraries.minecraft.net/";
        private string _mojangJava = "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";
        private string _forge = "https://files.minecraftforge.net/maven";
        private string _optifineAPI = "https://bmclapi2.bangbang93.com/optifine/{mcversion}/{type}/{patch}";
        private string _authlibInjector = "https://authlib-injector.yushi.moe";
        private string _fabricMeta = "https://meta.fabricmc.net";
        private string _fabricMaven = "https://maven.fabricmc.net";
        private string _name = "Official";
        private string _fabricManifest = "https://meta.fabricmc.net/v2/versions";
        private string _forgeListViaMcVer = "https://bmclapi2.bangbang93.com/forge/minecraft/{mcversion}";
        private string _forgeSupportedMc = "https://bmclapi2.bangbang93.com/forge/minecraft/{mcversion}";
        private string _optifineListViaMcVer = "https://bmclapi2.bangbang93.com/optifine/{mcversion}";
        private int _sourceType = 0;
        

        public string VersionManifest { get => _versionManifest; set => _versionManifest = value; }
        public string VersionManifestV2 { get => _versionManifestV2; set => _versionManifestV2 = value; }
        public string LauncherMeta { get => _launcherMeta; set => _launcherMeta = value; }
        public string Launcher { get => _launcher; set => _launcher = value; }
        public string ResourcesDownload { get => _resourcesDownload; set => _resourcesDownload = value; }
        public string Libraries { get => _libraries; set => _libraries = value; }
        public string MojangJava { get => _mojangJava; set => _mojangJava = value; }
        public string Forge { get => _forge; set => _forge = value; }
        public string OptifineAPI { get => _optifineAPI; set => _optifineAPI = value; }
        public string AuthlibInjector { get => _authlibInjector; set => _authlibInjector = value; }
        public string FabricMeta { get => _fabricMeta; set => _fabricMeta = value; }
        public string FabricMaven { get => _fabricMaven; set => _fabricMaven = value; }
        public string Name { get => _name; set => _name = value; }
        public string FabricManifest { get => _fabricManifest; set => _fabricManifest = value; }
        public string ForgeListViaMcVer { get => _forgeListViaMcVer; set => _forgeListViaMcVer = value; }
        public string ForgeSupportedMc { get => _forgeSupportedMc; set => _forgeSupportedMc = value; }
        public string OptifineListViaMcVer { get => _optifineListViaMcVer; set => _optifineListViaMcVer = value; }
        public int SourceType { get => _sourceType; set => _sourceType = value; }

        public DownloadSource(
            string versionManifest = "https://piston-meta.mojang.com/mc/game/version_manifest.json",
            string versionManifestV2 = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json",
            string launcherMeta = "https://launchermeta.mojang.com/",
            string launcher = "https://launcher.mojang.com/",
            string resourcesDownload = "https://resources.download.minecraft.net",
            string libraries = "https://libraries.minecraft.net/",
            string mojangJava = "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json",
            string forge = "https://files.minecraftforge.net/maven",
            string optifineAPI = "https://bmclapi2.bangbang93.com/optifine/{mcversion}/{type}/{patch}",
            string authlibInjector = "https://authlib-injector.yushi.moe",
            string fabricMeta = "https://meta.fabricmc.net",
            string fabricMaven = "https://maven.fabricmc.net",
            string forgeListViaMcVer = "https://bmclapi2.bangbang93.com/forge/minecraft/{mcversion}",
            string forgeSupportedMc = "https://bmclapi2.bangbang93.com/forge/minecraft",
            string optifineListViaMcVer = "https://bmclapi2.bangbang93.com/optifine/{mcversion}",
            int sourceType = 0,
            string name = "Official",
            string fabricManifest = "https://meta.fabricmc.net/v2/versions")
        {
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
            Launcher = launcher;
            ForgeListViaMcVer = forgeListViaMcVer;
            ForgeSupportedMc = forgeSupportedMc;
            OptifineListViaMcVer = optifineListViaMcVer;
            FabricManifest = fabricManifest;
            Name = name;
        }

        public void Bmclapi()
        {
            VersionManifest = "http://bmclapi2.bangbang93.com/mc/game/version_manifest.json";
            VersionManifestV2 = "http://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json";
            LauncherMeta = "https://bmclapi2.bangbang93.com";
            Launcher = "https://bmclapi2.bangbang93.com";
            ResourcesDownload = "https://bmclapi2.bangbang93.com/assets";
            Libraries = "https://bmclapi2.bangbang93.com/maven";
            MojangJava = "https://bmclapi2.bangbang93.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";
            Forge = "https://bmclapi2.bangbang93.com/maven";
            OptifineAPI = "https://bmclapi2.bangbang93.com/optifine/{mcversion}/{type}/{patch}";
            AuthlibInjector = "https://bmclapi2.bangbang93.com/mirrors/authlib-injector";
            FabricMeta = "https://bmclapi2.bangbang93.com/fabric-meta";
            FabricMaven = "https://bmclapi2.bangbang93.com/maven";
            SourceType = 1;
            FabricManifest = FabricMeta + "/v2/versions";
        }
        public void LineMirror()
        {
            VersionManifest = "https://lm.icecreamteam.win:440/mc/game/version_manifest.json";
            VersionManifestV2 = "http://launchermeta.mojang.com/mc/game/version_manifest_v2.json";
            LauncherMeta = "https://lm.icecreamteam.win:440/launchermeta/";
            Launcher = "https://lm.icecreamteam.win:440/launcher/";
            ResourcesDownload = "https://lm.icecreamteam.win:440/assets";
            Libraries = "https://lm.icecreamteam.win:440/libraries";
            MojangJava = "https://bmclapi2.bangbang93.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";
            Forge = "https://bmclapi2.bangbang93.com/maven";
            OptifineAPI = "https://bmclapi2.bangbang93.com/optifine/{mcversion}/{type}/{patch}";
            AuthlibInjector = "https://bmclapi2.bangbang93.com/mirrors/authlib-injector";
            FabricMeta = "https://bmclapi2.bangbang93.com/fabric-meta";
            FabricMaven = "https://bmclapi2.bangbang93.com/maven";
            SourceType = 2;
            FabricManifest = FabricMeta + "/v2/versions";
        }

    }

}
