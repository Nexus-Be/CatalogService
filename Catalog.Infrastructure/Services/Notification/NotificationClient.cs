namespace Catalog.Infrastructure.Services.Notification;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Catalog.Application.Interfaces;

public sealed class NotificationClient : INotificationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationClient> _logger;

    public NotificationClient(HttpClient httpClient, ILogger<NotificationClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> SendInAppNotificationAsync(
        Guid userId,
        string title,
        string body,
        string? notificationType = null,
        string? payload = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        try
        {
            var requestPayload = new
            {
                userId = userId,
                title = title,
                body = body,
                notificationType = notificationType,
                payload = payload
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/v1/internal/notifications/in-app",
                requestPayload,
                JsonOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "[REST Internal Notification] Successfully dispatched in-app notification to user '{UserId}' (Type: '{Type}', Title: '{Title}')",
                    userId, notificationType ?? "Unspecified", title);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "[REST Internal Notification] Failed to create in-app notification for user '{UserId}'. Status: {StatusCode}, Body: {Body}",
                userId, response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[REST Internal Notification] Error connecting to NotificationService for user '{UserId}' (Type: '{Type}')",
                userId, notificationType ?? "Unspecified");
            return false;
        }
    }
}

