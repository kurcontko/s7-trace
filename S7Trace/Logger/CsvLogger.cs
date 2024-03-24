using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace S7Trace.Logger
{
   public class CsvLogger
   {
      private readonly string _filePath;
      private readonly string _fallbackFilePath;
      private readonly ConcurrentQueue<object> _logQueue = new ConcurrentQueue<object>();
      private readonly object _fileLock = new object();
      private Task _loggingTask;
      private bool _isStopping = false;
      private const int BatchSize = 1000;

      public CsvLogger(string filePath, string fallbackFilePath)
      {
         _filePath = filePath;
         _fallbackFilePath = fallbackFilePath;
         StartLoggingTask();
      }

      public void EnqueueLogs(object[] logObjects)
      {
         foreach (var logObject in logObjects)
         {
            _logQueue.Enqueue(logObject);
         }
      }

      private void StartLoggingTask()
      {
         _loggingTask = Task.Run(() =>
         {
            List<string> batch = new List<string>(BatchSize);

            while (!_isStopping || !_logQueue.IsEmpty)
            {
               if (_logQueue.TryDequeue(out object logObject))
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
         if (File.Exists(_fallbackFilePath) && new FileInfo(_fallbackFilePath).Length > 0)
         {
            try
            {
               var lines = File.ReadAllLines(_fallbackFilePath);
               lock (_fileLock)
               {
                  File.AppendAllLines(_filePath, lines);
                  File.WriteAllText(_fallbackFilePath, string.Empty); // Clear fallback log
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
         bool writeSuccessful = WriteToFileSafe(combinedLog, _filePath);
         if (writeSuccessful)
         {
            MergeFallbackLogIfExists();
         }
      }

      private bool WriteToFileSafe(string line, string filePath)
      {
         try
         {
            lock (_fileLock)
            {
               File.AppendAllText(filePath, line + Environment.NewLine);
            }
            return true;
         }
         catch (IOException)
         {
            // Primary file is locked or unavailable, write to fallback file
            WriteToFileSafe(line, _fallbackFilePath);
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
         _isStopping = true;
         await _loggingTask; // Ensure all logs are processed before stopping
         MergeFallbackLogIfExists(); // Merge any remaining fallback logs
      }
   }
}