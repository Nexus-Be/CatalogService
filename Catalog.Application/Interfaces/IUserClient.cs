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
}

