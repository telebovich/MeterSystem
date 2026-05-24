namespace MeterSystem.Shared.Models;

public record MeterData(long MeterNumber, Dictionary<DateTime, double> Readings);
