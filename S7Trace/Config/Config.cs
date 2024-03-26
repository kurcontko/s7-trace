using System;
using System.Collections.Generic;
using System.Linq;
using S7Trace.Models;


namespace S7Trace.Config
{
    class Configuration
    {
        public string IPAddress { get; set; }
        public int Rack { get; set; }
        public int Slot { get; set; }
        public List<PLCVariable> Variables { get; set; }
    }
}
