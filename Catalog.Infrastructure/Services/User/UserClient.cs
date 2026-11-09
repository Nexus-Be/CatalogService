namespace Catalog.Infrastructure.Services.User;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Catalog.Application.Interfaces;

public sealed class UserClient : IUserClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<UserClient> _logger;

    public UserClient(HttpClient httpClient, ILogger<UserClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UserPermissionCheckResponse?> CheckPermissionAsync(
        Guid userId,
        string action = "sell",
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        try
        {
            var url = $"api/v1/internal/users/{userId}/permission-check?action={Uri.EscapeDataString(action)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserPermissionCheckResponse>(JsonOptions, cancellationToken);
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "[REST Internal User] Permission check failed for UserId: {UserId}, Action: {Action}. Status: {StatusCode}, Body: {Body}",
                userId, action, response.StatusCode, errorBody);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[REST Internal User] Error connecting to UserService during permission check for UserId: {UserId}, Action: {Action}",
                userId, action);

            return null;
        }
    }
}

