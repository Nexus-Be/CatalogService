namespace Catalog.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface INotificationClient
{
    Task<bool> SendInAppNotificationAsync(
        Guid userId,
        string title,
        string body,
        string? notificationType = null,
        string? payload = null,
        CancellationToken cancellationToken = default);

    Task<bool> SendEmailNotificationAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}

