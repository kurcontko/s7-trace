using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using S7Trace.Models;


namespace S7Trace.PLC
{
   public interface IPlcService
   {
      bool IsConnected { get; }
      int Connect(string ipAddress, int rack, int slot);
      void Disconnect();
      Task ReadDataAsync(CancellationToken cancellationToken, ICollection<PLCVariable> variables,
         ConcurrentQueue<ChartData> chartDataQueue, ConcurrentQueue<LogData> logDataQueue);
   }
}
