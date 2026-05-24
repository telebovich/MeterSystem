using System.Text;
using RabbitMQ.AMQP.Client;
using RabbitMQ.AMQP.Client.Impl;
using MeterSystem.Shared.Models;
using ProtoBuf;
using MeterSystem.Shared.src.Data;

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

app.MapPost("/api/readings", async (MeterData meter) =>
{
    if (meter.meter_number <= 0)
    {
        return Results.BadRequest("Invalid meter number");
    }

    if (meter.Readings == null || meter.Readings.Count == 0)
    {
        return Results.BadRequest("Readings cannot be empty");
    }

    const string brokerUri = "amqp://guest:guest@rabbitmq:5672/%2f";

    ConnectionSettings settings = ConnectionSettingsBuilder.Create()
        .Uri(new Uri(brokerUri))
        .ContainerId("meter-readings")
        .Build();

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
            // TODO: Add automatic mapping or change the initial data model to match the message format
            var reading = new Reading
            {
                meter_number = meter.meter_number,
                Readings = meter.Readings
            };

            var stream = new MemoryStream();
            Serializer.Serialize(stream, reading); // Validate that the object can be serialized
            var data = stream.ToArray();

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

