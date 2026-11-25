namespace Catalog.Application.Interfaces;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed record UserPermissionCheckResponse(
    Guid UserId,
    string Action,
    bool IsAllowed,
    string? Reason = null,
    DateTimeOffset? ExpiresAt = null,
    List<string>? ActiveRestrictions = null);

public interface IUserClient
{
    Task<UserPermissionCheckResponse?> CheckPermissionAsync(
        Guid userId,
        string action = "sell",
        CancellationToken cancellationToken = default);

    Task<bool> PenalizeSellerAsync(
        Guid sellerId,
        int penaltyPoints,
        string reason,
        Guid referenceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the penalty and returns the points that User actually deducted (it can differ from the points asked for),
    /// or null when the call failed. A duplicate request returns 0.
    /// </summary>
    Task<int?> PenalizeSellerForReportAsync(
        Guid sellerId,
        int penaltyPoints,
        string reason,
        Guid referenceId,
        CancellationToken cancellationToken = default);

    Task<bool> AdjustReputationAsync(
        Guid sellerId,
        short ratingStar,
        Guid orderId,
        CancellationToken cancellationToken = default);
}

