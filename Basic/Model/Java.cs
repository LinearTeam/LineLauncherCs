using System;

namespace LMC.Basic.Model
{
    public class Java
    {
        public string Path { get; set; }
        public bool IsJre { get; set; }
        public Version Version { get; set; }
        public string Arch { get; set; }
        public string Implementor { get; set; }
        public bool IsUserJava  { get; set; }
        
        public Java(string path, bool isJre, Version version, string arch, string implementor, bool isUserJava)
        {
            Path = path;
            IsJre = isJre;
            Version = version;
            Arch = arch;
            Implementor = implementor;
            IsUserJava = isUserJava;
        }
    }
}