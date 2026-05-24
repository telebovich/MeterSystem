namespace MeterSystem.Shared.Models;

public record MeterData(long meter_number, Dictionary<DateTime, double> Readings);
