namespace Catalog.Infrastructure.Consumers;

using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Catalog.Application.Common.Contracts.Events;

/// <summary>
/// Consumer đón nhận và xử lý sự kiện CategoryCreatedEvent từ Message Broker (RabbitMQ / In-Memory).
/// </summary>
public sealed class CategoryCreatedConsumer : IConsumer<CategoryCreatedEvent>
{
    private readonly ILogger<CategoryCreatedConsumer> _logger;

    public CategoryCreatedConsumer(ILogger<CategoryCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<CategoryCreatedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "[RABBITMQ EVENT RECEIVED] CategoryCreatedEvent - EventId: {EventId}, CategoryId: {CategoryId}, Name: '{Name}', Level: {Level}, CreatedAt: {CreatedAt:O}",
            message.EventId,
            message.CategoryId,
            message.Name,
            message.Level,
            message.CreatedAt
        );

        return Task.CompletedTask;
    }
}

