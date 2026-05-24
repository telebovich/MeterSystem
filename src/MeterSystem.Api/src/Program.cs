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
    // TODO: Validation logic

    const string brokerUri = "amqp://guest:guest@localhost:5672/%2f";

    ConnectionSettings settings = ConnectionSettingsBuilder.Create()
        .Uri(new Uri(brokerUri))
        .ContainerId("tutorial-send")
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
                MeterNumber = meter.MeterNumber,
                Readings = meter.Readings
            };

            var stream = new MemoryStream();
            Serializer.Serialize(stream, reading); // Validate that the object can be serialized
            var data = stream.ToArray();

            var message = new AmqpMessage(new Amqp.Message(data));
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

