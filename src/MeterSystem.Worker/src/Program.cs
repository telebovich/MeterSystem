using System.Text.Json;
using MeterSystem.Shared.Models;
using Npgsql;
using RabbitMQ.AMQP.Client;
using RabbitMQ.AMQP.Client.Impl;

var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();
const string brokerUri = "amqp://guest:guest@rabbitmq:5672/%2f";

ConnectionSettings settings = ConnectionSettingsBuilder.Create()
    .Uri(new Uri(brokerUri))
    .ContainerId("meter-readings-consumer")
    .Build();

IEnvironment environment = AmqpEnvironment.Create(settings);
IConnection connection = await environment.CreateConnectionAsync();

IConsumer consumer = await connection.ConsumerBuilder()
    .Queue("meter_readings")
    .InitialCredits(1)
    .MessageHandler(async (ctx, message) =>
    {
        MeterSystem.Shared.Models.MeterData reading = JsonSerializer.Deserialize<MeterSystem.Shared.Models.MeterData>(message.Body())!;

        Console.WriteLine($" [x] Received");
        try
        {
            await DoWork(reading);
        }
        finally
        {
            ctx.Accept();
        }
    })
    .BuildAndStartAsync();

IConsumer rawConsumer = await connection.ConsumerBuilder()
    .Queue("raw_meter_readings")
    .InitialCredits(1)
    .MessageHandler(async (ctx, message) =>
    {
        RawMeterData data = JsonSerializer.Deserialize<RawMeterData>(message.Body())!;

        var deserializedByteArray = Convert.FromBase64String(data.Data);
        var readings = MeterData.Parser.ParseFrom(deserializedByteArray);
        Console.WriteLine($" [x] Received Raw Reading");
        try
        {
           await DoRawWork(data.MeterNumber, readings);
        }
        finally
        {
            ctx.Accept();
        }
    })
    .BuildAndStartAsync();

host.Run();

async Task DoWork(MeterSystem.Shared.Models.MeterData body)
{
    var connString = "Host=postgres;Username=postgres;Password=postgres;Database=meters;GssEncMode=Disable";
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    int? meter_id = null;
    // Insert some data
    await using (var cmd = new NpgsqlCommand("INSERT INTO meters (meter_number) VALUES (@p) RETURNING meter_id", conn))
    {
        cmd.Parameters.AddWithValue("p", body.meter_number);
        var mtr_id = await cmd.ExecuteScalarAsync();

        if (mtr_id is not null)
        {
            meter_id = Convert.ToInt32(mtr_id);
        }
    }

    if (meter_id is null)
    {
        return;
    }

    foreach (var reading in body.Readings)
    {
        await using (var cmd = new NpgsqlCommand("INSERT INTO meter_readings (meter_id, value_at, value, received_at_utc) VALUES (@p1, @p2, @p3, @p4)", conn))
        {
            cmd.Parameters.AddWithValue("p1", meter_id);
            cmd.Parameters.AddWithValue("p2", reading.Key);
            cmd.Parameters.AddWithValue("p3", reading.Value);
            cmd.Parameters.AddWithValue("p4", DateTime.UtcNow);
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { }
        }
    }
}

async Task DoRawWork(long meter_number, MeterData data)
{
    var connString = "Host=postgres;Username=postgres;Password=postgres;Database=meters;GssEncMode=Disable";
    await using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    int? meter_id = null;
    // Insert some data
    await using (var cmd = new NpgsqlCommand("INSERT INTO meters (meter_number) VALUES (@p) RETURNING meter_id", conn))
    {
        cmd.Parameters.AddWithValue("p", meter_number);
        var mtr_id = await cmd.ExecuteScalarAsync();

        if (mtr_id is not null)
        {
            meter_id = Convert.ToInt32(mtr_id);
        }
    }

    if (meter_id is null)
    {
        return;
    }

    foreach (var reading in data.Readings)
    {
        await using (var cmd = new NpgsqlCommand("INSERT INTO meter_readings (meter_id, value_at, value, received_at_utc) VALUES (@p1, @p2, @p3, @p4)", conn))
        {
            cmd.Parameters.AddWithValue("p1", meter_id);
            cmd.Parameters.AddWithValue("p2", reading.Timestamp.ToDateTime());
            cmd.Parameters.AddWithValue("p3", reading.Value);
            cmd.Parameters.AddWithValue("p4", DateTime.UtcNow);
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
