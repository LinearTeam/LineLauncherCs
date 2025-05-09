using System;
using LMC.Basic;
using LMC.Minecraft.Download.Model;

namespace LMC.Minecraft.Download.Exceptions
{
    public class ProcessorException : Exception, IBaseException
    {
        public int ExitCode { get; }
        public Processor Process { get; }

        public ProcessorException(int exitCode, Processor process, string message) : base(message)
        {
            ExitCode = exitCode;
            Process = process;
        }

        public string GetLogString()
        {
            return Message + $"，Process: {Process.Jar}，退出码: {ExitCode}";
        }
    }
}