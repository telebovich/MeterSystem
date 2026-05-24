using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;
using MeterSystem.Shared.Models;
using ProtoBuf;
using RabbitMQ.AMQP.Client;
using RabbitMQ.AMQP.Client.Impl;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

const string brokerUri = "amqp://guest:guest@rabbitmq:5672/%2f";

ConnectionSettings settings = ConnectionSettingsBuilder.Create()
    .Uri(new Uri(brokerUri))
    .ContainerId("meter-readings")
    .Build();

app.MapPost("/api/readings", async (MeterSystem.Shared.Models.MeterData meter) =>
{
    if (meter.meter_number <= 0)
    {
        return Results.BadRequest("Invalid meter number");
    }

    if (meter.Readings == null || meter.Readings.Count == 0)
    {
        return Results.BadRequest("Readings cannot be empty");
    }

    IEnvironment environment = AmqpEnvironment.Create(settings);
    IConnection connection = await environment.CreateConnectionAsync();

    try
    {
        IManagement management = connection.Management();
        IQueueSpecification queueSpec = management.Queue("meter_readings").Type(QueueType.CLASSIC); // TODO: Changed from quorum
        await queueSpec.DeclareAsync();

        IPublisher publisher = await connection.PublisherBuilder().Queue("meter_readings").BuildAsync();
        try
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(meter);

            var message = new AmqpMessage(data);
            PublishResult pr = await publisher.PublishAsync(message);
            if (pr.Outcome.State != OutcomeState.Accepted)
            {
                Console.Error.WriteLine($"Unexpected publish outcome: {pr.Outcome.State}");
                return Results.Problem("Failed to publish message");
            }

            Console.WriteLine($" [x] Sent");
        }
        finally
        {
            await publisher.CloseAsync();
        }
    }
    finally
    {
        await connection.CloseAsync();
        await environment.CloseAsync();
    }

    return Results.Accepted();
});

app.MapPost("/api/readings/raw", async (RawMeterData rawMeterData) =>
{
    if (rawMeterData.MeterNumber <= 0)
    {
        return Results.BadRequest("Invalid meter number");
    }

    if (string.IsNullOrEmpty(rawMeterData.Data))
    {
        return Results.BadRequest("Readings cannot be empty");
    }

    IEnvironment environment = AmqpEnvironment.Create(settings);
    IConnection connection = await environment.CreateConnectionAsync();

    try
    {
        IManagement management = connection.Management();
        IQueueSpecification queueSpec = management.Queue("raw_meter_readings").Type(QueueType.CLASSIC); // TODO: Changed from quorum
        await queueSpec.DeclareAsync();

        IPublisher publisher = await connection.PublisherBuilder().Queue("raw_meter_readings").BuildAsync();
        try
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(rawMeterData);
            var message = new AmqpMessage(data);
            PublishResult pr = await publisher.PublishAsync(message);
            if (pr.Outcome.State != OutcomeState.Accepted)
            {
                Console.Error.WriteLine($"Unexpected publish outcome: {pr.Outcome.State}");
                return Results.Problem("Failed to publish message");
            }

            Console.WriteLine($" [x] Sent");
        }
        finally
        {
            await publisher.CloseAsync();
        }
    }
    finally
    {
        await connection.CloseAsync();
        await environment.CloseAsync();
    }

    return Results.Accepted();
});

app.Run();

