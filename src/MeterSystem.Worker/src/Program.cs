
using System.Runtime.CompilerServices;
using MeterSystem.Shared.Models;
using MeterSystem.Shared.src.Data;
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
    .MessageHandler((ctx, message) =>
    {
        Reading reading = Serializer.Deserialize<Reading>(message.Body());

        Console.WriteLine($" [x] Received");
        try
        {
            DoWork(reading);
        }
        finally
        {
            ctx.Accept();
        }

        return Task.CompletedTask;
    })
    .BuildAndStartAsync();

host.Run();

void DoWork(Reading body)
{
    return;
}
