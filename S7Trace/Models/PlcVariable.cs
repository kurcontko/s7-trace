using Sharp7;

namespace S7Trace.Models
{
    public class PLCVariable
    {
        public bool Enable { get; set; }
        public string Name { get; set; }
        public S7Area AreaID { get; set; }
        public int DBNumber { get; set; }
        public S7WordLength Type { get; set; }
        public int Offset { get; set; }
    }
}
