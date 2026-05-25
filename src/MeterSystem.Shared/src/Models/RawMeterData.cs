using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MeterSystem.Shared.Models
{
    public class RawMeterData
    {
        [JsonPropertyName("meter_number")]
        public long MeterNumber { get; set; }
        public string Data { get; set; }
    }
}
