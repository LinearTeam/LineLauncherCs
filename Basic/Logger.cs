using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LMC.Basic
{
    public class Logger
    {
        public static string LogNum = "1";
        private string _module;
        private string _logFile;
        private BlockingCollection<string> _logQueue;
        private Task _logTask;
        private CancellationTokenSource _cancellationTokenSource;

        public Logger(string module, string filePath = "not")
        {
            this._module = module;
            if (filePath.Equals("not"))
            {
                filePath = "./LMC/logs/log" + LogNum + ".log";
            }
            _logFile = filePath;
            _logQueue = new BlockingCollection<string>();
            _cancellationTokenSource = new CancellationTokenSource();

            _logTask = Task.Run(() => ProcessLogQueue(_cancellationTokenSource.Token));
        }

        private void ProcessLogQueue(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested || _logQueue.Count > 0)
                {
                    string logEntry;
                    if (_logQueue.TryTake(out logEntry, Timeout.Infinite, token))
                    {
                        using (StreamWriter logWriter = new StreamWriter(_logFile, true))
                        using (StreamWriter latestWriter = new StreamWriter("./LMC/logs/latest.log", true))
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
            catch(Exception ex)
            {
            }
        }

        private void FlushRemainingLogs()
        {
            using (StreamWriter logWriter = new StreamWriter(_logFile, true))
            using (StreamWriter latestWriter = new StreamWriter("./LMC/logs/latest.log", true))
            {
                while (_logQueue.TryTake(out string logEntry))
                {
                    logWriter.WriteLine(logEntry);
                    latestWriter.WriteLine(logEntry);
                }
            }
        }

        private void Log(string level, string msg)
        {
            string time = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss.fff");
            string logEntry = $"[{time}/{level}][{_module}]{msg}";
            _logQueue.Add(logEntry);
        }

        public void Info(string msg) => Log("INFO", msg);
        public void Error(string msg) => Log("ERROR", msg);
        public void Warn(string msg) => Log("WARN", msg);

        public void Close()
        {
            _cancellationTokenSource.Cancel();
            _logTask.Wait();
        }

        ~Logger()
        {
            Close();
        }
    }
}
