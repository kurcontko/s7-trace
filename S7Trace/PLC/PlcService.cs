
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sharp7;
using log4net;
using S7Trace.Models;

namespace S7Trace.PLC
{
   public class PlcService : IPlcService
   {
      private S7Client plcClient;
      private static readonly ILog log = LogManager.GetLogger(typeof(PlcService));

      public bool IsConnected => plcClient?.Connected ?? false;

      public int Connect(string ipAddress, int rack, int slot)
      {
         plcClient = new S7Client();
         int result = plcClient.ConnectTo(ipAddress, rack, slot);
         return result;
      }

      public void Disconnect()
      {                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   
         plcClient?.Disconnect();
      }

      public async Task ReadDataAsync(CancellationToken cancellationToken, ICollection<PLCVariable> variables,
         ConcurrentQueue<ChartData> chartDataQueue, ConcurrentQueue<LogData> logDataQueue)
      {
         while (!cancellationToken.IsCancellationRequested)
         {
            try {
               // Create a new instance of the S7MultiVar class
               S7MultiVar reader = new S7MultiVar(plcClient);

               // Add enabled variables to the reader
               List<PLCVariable> enabledVariables = new List<PLCVariable>();
               int enabledVariableCount = variables.Count(v => v.Enable);
               byte[][] buffers = new byte[enabledVariableCount][];
               int bufferIndex = 0;

               foreach (var variable in variables)
               {
                  if (variable.Enable)
                  {
                     int variableSize = GetBufferSizeForVariableType(variable.Type);
                     int bufferSize = variableSize * 2; // Word to Byte
                     buffers[bufferIndex] = new byte[bufferSize];
                     reader.Add((int)variable.AreaID, (int)variable.Type, variable.DBNumber, variable.Offset, variableSize, ref buffers[bufferIndex]);
                     enabledVariables.Add(variable);
                     bufferIndex++;
                  }
               }

               // Read all variables in the reader
               int result = reader.Read();

               // Check if the read operation was successful
               if (result == 0)
               {
                  var timestamp = DateTime.Now;
                  log.Info("Data read from the PLC successfully.");
                  // Extract the values from the buffers and enqueue them
                  for (int i = 0; i < enabledVariables.Count; i++)
                  {
                     // Extract the value from the buffer
                     dynamic value = ExtractValueFromBuffer(enabledVariables[i], buffers[i]);

                     // Enqueue the data to the chart queue
                     try {
                        double chartValue = value is double ? value : Convert.ToDouble(value);
                        chartDataQueue.Enqueue(new ChartData { Variable = enabledVariables[i], Value = chartValue });  
                     } catch (Exception ex) {
                        log.Warn($"Failed to convert value to double: {value}", ex);
                     }
                     
                     // Enqueue the data to the logger queue
                     logDataQueue.Enqueue(new LogData(timestamp, enabledVariables[i].Name, enabledVariables[i].Type.ToString(), value));
                  }
               }

               await Task.Delay(10, cancellationToken);
            }
            catch (Exception ex)
            {
               log.Warn("An error occurred while reading data from the PLC", ex);
               AttemptReconnect();
               await Task.Delay(100, cancellationToken);
            }
         }
      }

      private int GetBufferSizeForVariableType(S7WordLength variableType)
      {
         // These values are represented as Words
         switch (variableType)
         {
            case S7WordLength.Bit:
               return 1; // 1 byte for a bit
            case S7WordLength.Byte:
               return 1; // 1 byte
            case S7WordLength.Word:
               return 1; // 2 bytes
            case S7WordLength.DWord:
               return 2; // 4 bytes
            case S7WordLength.Int:
               return 1; // 2 bytes
            case S7WordLength.DInt:
               return 2; // 4 bytes
            case S7WordLength.Real:
               return 2; // 4 bytes
            default:
               throw new ArgumentOutOfRangeException(nameof(variableType), $"Unsupported variable type: {variableType}");
         }
      }

      private dynamic ExtractValueFromBuffer(PLCVariable variable, byte[] buffer)
      {
         switch (variable.Type)
         {
            case S7WordLength.Bit:
               return S7.GetBitAt(buffer, 0, 0) ? 1 : 0; // Assuming you want an integer value here

            case S7WordLength.Byte:
               return buffer[0]; // Byte value

            case S7WordLength.Word:
               return S7.GetWordAt(buffer, 0); // Word value, likely an integer

            case S7WordLength.DWord:
               return S7.GetDWordAt(buffer, 0); // DWord value, could be a larger integer

            case S7WordLength.Int:
               return S7.GetIntAt(buffer, 0); // Int value

            case S7WordLength.DInt:
               return S7.GetDIntAt(buffer, 0); // DInt value, likely a long or int depending on implementation

            case S7WordLength.Real:
               return S7.GetRealAt(buffer, 0); // Real value, likely a float or double

            default:
               throw new ArgumentOutOfRangeException(nameof(variable.Type), $"Unsupported variable type: {variable.Type}");
         }
      }

      private void AttemptReconnect()
      {
         log.Info("Attempting to reconnect to the PLC...");
         plcClient?.Disconnect();
         plcClient?.Connect();
      }
   }
}