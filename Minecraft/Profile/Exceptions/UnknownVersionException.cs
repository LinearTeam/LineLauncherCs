namespace LMC.Minecraft.Profile.Exceptions
{
    public class UnknownVersionException : System.Exception
    {
        public string VersionFolder;
        public UnknownVersionException(string message, string versionFolderPath, System.Exception innerException) : base(message, innerException)
        {
            VersionFolder = versionFolderPath;
        }
        
        public UnknownVersionException(string message, string versionFolderPath) : base(message)
        {
            VersionFolder = versionFolderPath;            
        }
    }
}