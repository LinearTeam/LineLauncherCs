using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LMC.Basic
{
    public class Logger
    {
        public static string logNum = "1";
        private string module;
        private string logFile;
        private BlockingCollection<string> logQueue;
        private Task logTask;
        private CancellationTokenSource cancellationTokenSource;
        private const int flushCount = 20;

        public Logger(string module, string filePath = "not")
        {
            this.module = module;
            if (filePath.Equals("not"))
            {
                filePath = "./lmc/logs/log" + logNum + ".log";
            }
            logFile = filePath;
            logQueue = new BlockingCollection<string>();
            cancellationTokenSource = new CancellationTokenSource();

            logTask = Task.Run(() => ProcessLogQueue(cancellationTokenSource.Token));
        }

        private void ProcessLogQueue(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested || logQueue.Count > 0)
                {
                    string logEntry;
                    if (logQueue.TryTake(out logEntry, Timeout.Infinite, token))
                    {
                        using (StreamWriter logWriter = new StreamWriter(logFile, true))
                        using (StreamWriter latestWriter = new StreamWriter("./lmc/logs/latest.log", true))
                        {
                            logWriter.WriteLine(logEntry);
                            latestWriter.WriteLine(logEntry);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                FlushRemainingLogs();
            }
            catch (IOException e)
            {
                Environment.Exit(1);
            }
        }

        private void FlushRemainingLogs()
        {
            using (StreamWriter logWriter = new StreamWriter(logFile, true))
            using (StreamWriter latestWriter = new StreamWriter("./lmc/logs/latest.log", true))
            {
                while (logQueue.TryTake(out string logEntry))
                {
                    logWriter.WriteLine(logEntry);
                    latestWriter.WriteLine(logEntry);
                }
            }
        }

        private void Log(string level, string msg)
        {
            string time = DateTime.Now.ToString("G");
            string logEntry = $"[{time}/{level}][{module}]{msg}";
            logQueue.Add(logEntry);
        }

        public void info(string msg) => Log("INFO", msg);
        public void error(string msg) => Log("ERROR", msg);
        public void warn(string msg) => Log("WARN", msg);

        public void Close()
        {
            cancellationTokenSource.Cancel();
            logTask.Wait();
        }

        ~Logger()
        {
            Close();
        }
    }
}
