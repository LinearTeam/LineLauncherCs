using System;

namespace LMC.Minecraft
{
    public class UnknownVersionException : Exception
    {
        public string VersionFolder;
        public UnknownVersionException(string message, string versionFolderPath, Exception innerException) : base(message, innerException)
        {
            VersionFolder = versionFolderPath;
        }
        
        public UnknownVersionException(string message, string versionFolderPath) : base(message)
        {
            VersionFolder = versionFolderPath;            
        }
    }
}