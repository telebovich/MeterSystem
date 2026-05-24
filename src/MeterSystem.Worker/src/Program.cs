using MeterSystem.Shared.src.Data;
using Npgsql;
using ProtoBuf;
using RabbitMQ.AMQP.Client;
using RabbitMQ.AMQP.Client.Impl;

var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();
const string brokerUri = "amqp://guest:guest@localhost:5672/%2f";

ConnectionSettings settings = ConnectionSettingsBuilder.Create()
    .Uri(new Uri(brokerUri))
    .ContainerId("meter-readings")
    .Build();

IEnvironment environment = AmqpEnvironment.Create(settings);
IConnection connection = await environment.CreateConnectionAsync();

IConsumer consumer = await connection.ConsumerBuilder()
    .Queue("meter_readings")
    .InitialCredits(1)
    .MessageHandler(async (ctx, message) =>
    {
        Reading reading = Serializer.Deserialize<Reading>(message.Body());

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

host.Run();

async Task DoWork(Reading body)
{
    var connString = "Host=localhost;Username=postgres;Password=passw0rd;Database=rimonim-tech";
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

    if (meter_id != null)
    {
        foreach (var reading in body.Readings)
        {
            await using (var cmd = new NpgsqlCommand("INSERT INTO meter_readings (meter_id, value_at, value, received_at_utc) VALUES (@p1, @p2, @p3, @p4)", conn))
            {
                cmd.Parameters.AddWithValue("p1", meter_id);
                cmd.Parameters.AddWithValue("p2", reading.Key);
                cmd.Parameters.AddWithValue("p3", reading.Value);
                cmd.Parameters.AddWithValue("p4", DateTime.UtcNow);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
