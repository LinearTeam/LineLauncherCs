using LMC.Minecraft.Download.Model;

namespace LMC.Minecraft.Download.Exceptions
{
    public class InvalidLibraryJsonException : System.Exception
    {
        public VanillaLibraryJson? InvalidLibraryJson;
        
        public InvalidLibraryJsonException(VanillaLibraryJson? invalidLibraryJson, string message) : base(message)
        {
            this.InvalidLibraryJson = invalidLibraryJson;
        }
    }
}