using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace MeterSystem.Shared.src.Data
{
    [ProtoContract]
    public class Reading
    {
        [ProtoMember(1)]
        public long meter_number { get; set; }
        [ProtoMember(2)]
        public Dictionary<DateTime, double> Readings { get; set; } = new Dictionary<DateTime, double>();
    }
}
