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

    public async Task<bool> PenalizeSellerAsync(
        Guid sellerId,
        int penaltyPoints,
        string reason,
        Guid referenceId,
        CancellationToken cancellationToken = default)
    {
        if (sellerId == Guid.Empty)
        {
            return false;
        }

        try
        {
            var payload = new
            {
                userId = sellerId,
                targetRole = "Seller",
                penaltyPoints = penaltyPoints,
                reason = reason,
                referenceId = referenceId.ToString()
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/internal/users/penalties", payload, JsonOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "[REST Internal User] Successfully penalized seller '{SellerId}' with {Points} points (Reason: '{Reason}', ReferenceId: '{ReferenceId}')",
                    sellerId, penaltyPoints, reason, referenceId);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "[REST Internal User] Penalize seller failed for SellerId: {SellerId}. Status: {StatusCode}, Body: {Body}",
                sellerId, response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[REST Internal User] Error connecting to UserService during penalize for SellerId: {SellerId}",
                sellerId);
            return false;
        }
    }

    public async Task<bool> AdjustReputationAsync(
        Guid sellerId,
        short ratingStar,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (sellerId == Guid.Empty)
        {
            return false;
        }

        try
        {
            var payload = new
            {
                userId = sellerId,
                targetRole = "Seller",
                ratingStar = ratingStar,
                orderId = orderId,
                referenceId = orderId.ToString()
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/internal/users/reputation-adjust", payload, JsonOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "[REST Internal User] Successfully adjusted reputation for seller '{SellerId}' with {Rating} stars (OrderId: '{OrderId}')",
                    sellerId, ratingStar, orderId);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "[REST Internal User] Reputation adjust failed for SellerId: {SellerId}. Status: {StatusCode}, Body: {Body}",
                sellerId, response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[REST Internal User] Error connecting to UserService during reputation adjust for SellerId: {SellerId}",
                sellerId);
            return false;
        }
    }
}

