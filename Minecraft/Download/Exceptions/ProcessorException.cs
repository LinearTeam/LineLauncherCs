using System;
using LMC.Minecraft.Download.Model;

namespace LMC.Minecraft.Download.Exceptions
{
    public class ProcessorException : Exception
    {
        public int ExitCode { get; }
        public Processor Process { get; }

        public ProcessorException(int exitCode, Processor process, string message) : base(message)
        {
            ExitCode = exitCode;
            Process = process;
        }
    }
}