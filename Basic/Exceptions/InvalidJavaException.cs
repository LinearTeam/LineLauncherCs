using System;

namespace LMC.Basic.Exceptions
{
    public class InvalidJavaException : Exception, IBaseException
    {
        public string JavaPath { get; set; }

        public string GetLogString()
        {
            return Message + "，Java路径：" + JavaPath;
        }

        public InvalidJavaException(string javaPath, string message) : base(message)
        {
            JavaPath = javaPath;
        }
    }
}