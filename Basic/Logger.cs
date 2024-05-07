using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LMC.Basic
{
    //Simple Logger
    public class Logger
    {
        public static string logNum = "1";
        public String module;
        public String logFile;
        public Logger(String module, String filePath = "not")
        {
            this.module = module;
            if(filePath.Equals("not"))
            {
                filePath = "./lmc/logs/log" + logNum + ".log";
            }
            logFile = filePath;
        }

        public void info(String msg)
        {
            msg = "[" + module + "]" + msg;
            msg = msg + "\n";
            try
            {
                String time = System.DateTime.Now.ToString("G");
                File.AppendAllText(logFile, "[" + time + "/INFO]" + msg);
                File.AppendAllText("./lmc/logs/latest.log", "[" + time + "/INFO]" + msg);
            }
            catch (IOException e)
            {
                Environment.Exit(1);
            }
        }
        public void error(String msg)
        {
            msg = "[" + module + "]" + msg;
            msg = msg + "\n";
            try
            {
                String time = System.DateTime.Now.ToString("G");
                File.AppendAllText(logFile, "[" + time + "/ERROR]" + msg);
                File.AppendAllText("./lmc/logs/latest.log", "[" + time + "/ERROR]" + msg);
            }
            catch (IOException e)
            {
                Environment.Exit(1);
            }
        }
        public void warn(String msg)
        {
            msg = "[" + module + "]" + msg;
            msg = msg + "\n";
            try
            {
                String time = System.DateTime.Now.ToString("G");
                File.AppendAllText(logFile, "[" + time + "/WARN]" + msg);
                File.AppendAllText("./lmc/logs/latest.log", "[" + time + "/WARN]" + msg);
            }
            catch (IOException e)
            {
                Environment.Exit(1);
            }
        }

    }
}
