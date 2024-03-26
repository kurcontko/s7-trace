using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace S7Trace.Logger
{
    public class CsvLogger
    {
        private readonly string filePath;
        private readonly string fallbackFilePath;
        private readonly ConcurrentQueue<object> logQueue = new ConcurrentQueue<object>();
        private readonly object fileLock = new object();
        private Task loggingTask;
        private bool isStopping = false;
        private const int BatchSize = 1000;

        public CsvLogger(string filePath, string fallbackFilePath)
        {
            this.filePath = filePath;
            this.fallbackFilePath = fallbackFilePath;
            StartLoggingTask();
        }

        public void EnqueueLogs(object[] logObjects)
        {
            foreach (var logObject in logObjects)
            {
            logQueue.Enqueue(logObject);
            }
        }

        private void StartLoggingTask()
        {
            loggingTask = Task.Run(() =>
            {
            List<string> batch = new List<string>(BatchSize);

            while (!isStopping || !logQueue.IsEmpty)
            {
                if (logQueue.TryDequeue(out object logObject))
                {
                    batch.Add(logObject.ToString());
                    if (batch.Count >= BatchSize)
                    {
                        WriteBatchToFile(batch);
                        batch.Clear();
                    }
                }
                else
                {
                // Small delay to prevent a tight loop when the queue is momentarily empty
                Task.Delay(100).Wait();
                }
            }

            // Ensure any remaining logs are flushed at the end
            if (batch.Count > 0)
            {
                WriteBatchToFile(batch);
            }
            });
        }

        private void MergeFallbackLogIfExists()
        {
            if (File.Exists(fallbackFilePath) && new FileInfo(fallbackFilePath).Length > 0)
            {
            try
            {
                var lines = File.ReadAllLines(fallbackFilePath);
                lock (fileLock)
                {
                    File.AppendAllLines(filePath, lines);
                    File.WriteAllText(fallbackFilePath, string.Empty); // Clear fallback log
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions during merging
                Console.WriteLine($"Error merging fallback log: {ex.Message}");
            }
            }
        }

        private void WriteBatchToFile(List<string> batch)
        {
            string combinedLog = string.Join(Environment.NewLine, batch) + Environment.NewLine;
            bool writeSuccessful = WriteToFileSafe(combinedLog, filePath);
            if (writeSuccessful)
            {
            MergeFallbackLogIfExists();
            }
        }

        private bool WriteToFileSafe(string line, string filePath)
        {
            try
            {
            lock (fileLock)
            {
                File.AppendAllText(filePath, line + Environment.NewLine);
            }
            return true;
            }
            catch (IOException)
            {
            // Primary file is locked or unavailable, write to fallback file
            WriteToFileSafe(line, fallbackFilePath);
            return false;
            }
            catch (Exception ex)
            {
            // Handle other exceptions
            Console.WriteLine($"Error writing to file: {ex.Message}");
            return false;
            }
        }

        public async Task StopLogging()
        {
            isStopping = true;
            await loggingTask; // Ensure all logs are processed before stopping
            MergeFallbackLogIfExists(); // Merge any remaining fallback logs
        }
    }
}