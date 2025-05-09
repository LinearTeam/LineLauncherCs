using System;
using System.Text.Json;
using LMC.Basic;
using LMC.Minecraft.Download.Model;

namespace LMC.Minecraft.Download.Exceptions
{
    public class InvalidLibraryJsonException : System.Exception, IBaseException
    {
        public VanillaLibraryJson? InvalidLibraryJson;
        
        public InvalidLibraryJsonException(VanillaLibraryJson? invalidLibraryJson, string message) : base(message)
        {
            this.InvalidLibraryJson = invalidLibraryJson;
        }

        public string GetLogString()
        {
            var guid = Guid.NewGuid();
            new Logger("ELJE").Error("[" + guid + "]" + JsonSerializer.Serialize(InvalidLibraryJson, new JsonSerializerOptions() { WriteIndented = true }));
            return Message + $"发生异常的VanilaLibraryJson请见日志。 LOG: {guid}";
        }
    }
}