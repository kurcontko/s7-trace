using System;

namespace S7Trace.Models
{
   public class LogData
   {
      public DateTime Timestamp { get; set; }
      public string VariableName { get; set; }
      public string VariableType { get; set; }
      public object Value { get; set; }

      public LogData(DateTime timestamp, string variableName, string variableType, object value)
      {
         Timestamp = timestamp;
         VariableName = variableName;
         VariableType = variableType;
         Value = value;
      }

      public override string ToString()
      {
         return $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff},{VariableName},{VariableType},{Value}";
      }

   }
}