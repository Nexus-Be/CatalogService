namespace Catalog.Tests;

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Infrastructure.Services.Notification;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class NotificationClientTests
{
    [Fact]
    public async Task SendInAppNotification_DispatchesToInternalInAppRoute()
    {
        var handler = new RecordingHandler((HttpStatusCode.OK, """{"success":true,"message":"In-app notification created successfully."}"""));
        var client = new NotificationClient(new HttpClient(handler) { BaseAddress = new Uri("http://notification.test/") }, NullLogger<NotificationClient>.Instance);

        var result = await client.SendInAppNotificationAsync(
            Guid.NewGuid(),
            "Title test",
            "Body test",
            "TEST_TYPE",
            "{}");

        Assert.True(result);
        var (method, path) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("/api/v1/internal/notifications/in-app", path);
    }

    [Fact]
    public async Task SendEmailNotification_DispatchesToEmailSendRoute()
    {
        var handler = new RecordingHandler((HttpStatusCode.OK, """{"success":true,"message":"Email dispatched successfully."}"""));
        var client = new NotificationClient(new HttpClient(handler) { BaseAddress = new Uri("http://notification.test/") }, NullLogger<NotificationClient>.Instance);

        var result = await client.SendEmailNotificationAsync(
            "seller@nexus.test",
            "Product Violation Notice",
            "<h1>Your product has been banned</h1>");

        Assert.True(result);
        var (method, path) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("/api/v1/notifications/email/send", path);
    }

    [Fact]
    public async Task SendEmailNotification_WithEmptyEmailOrSubject_ReturnsFalseWithoutCallingNetwork()
    {
        var handler = new RecordingHandler((HttpStatusCode.OK, "{}"));
        var client = new NotificationClient(new HttpClient(handler) { BaseAddress = new Uri("http://notification.test/") }, NullLogger<NotificationClient>.Instance);

        var resultEmptyEmail = await client.SendEmailNotificationAsync("", "Subject", "Body");
        var resultEmptySubject = await client.SendEmailNotificationAsync("test@test.com", "", "Body");

        Assert.False(resultEmptyEmail);
        Assert.False(resultEmptySubject);
        Assert.Empty(handler.Requests);
    }

    private sealed class RecordingHandler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        private int _next;
        public System.Collections.Generic.List<(HttpMethod Method, string Path)> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            var (status, body) = responses[Math.Min(_next++, responses.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
