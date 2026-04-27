using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Nextech.Api.HackerNews;
using Nextech.Api.Models;

namespace Nextech.Api.UnitTests;

public class HackerNewsClientTests
{
    private static HackerNewsClient CreateSut(HttpMessageHandler handler, ILogger<HackerNewsClient>? logger = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/v0/") };
        var opts = Options.Create(new HackerNewsOptions { BaseUrl = "https://example.com/v0/" });
        return new HackerNewsClient(http, opts, logger ?? NullLogger<HackerNewsClient>.Instance);
    }

    private static Mock<HttpMessageHandler> HandlerReturning(string url, HttpResponseMessage response)
    {
        var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString() == url),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
        return mock;
    }

    [Fact]
    public async Task GetNewStoryIdsAsync_hits_newstories_endpoint_and_returns_ids()
    {
        var handler = HandlerReturning(
            "https://example.com/v0/newstories.json",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[] { 1, 2, 3 })
            });
        var sut = CreateSut(handler.Object);

        var ids = await sut.GetNewStoryIdsAsync(CancellationToken.None);

        ids.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        handler.VerifyAll();
    }

    [Fact]
    public async Task GetItemAsync_hits_item_endpoint_and_deserializes_with_null_url()
    {
        var json = """{"id":42,"type":"story","title":"Ask HN: foo","url":null,"by":"u","time":123,"score":7}""";
        var handler = HandlerReturning(
            "https://example.com/v0/item/42.json",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler.Object);

        var item = await sut.GetItemAsync(42, CancellationToken.None);

        item.Should().NotBeNull();
        item!.Id.Should().Be(42);
        item.Title.Should().Be("Ask HN: foo");
        item.Url.Should().BeNull();
        item.By.Should().Be("u");
        handler.VerifyAll();
    }

    [Fact]
    public async Task GetItemAsync_returns_null_when_response_body_is_null()
    {
        var handler = HandlerReturning(
            "https://example.com/v0/item/9.json",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler.Object);

        var item = await sut.GetItemAsync(9, CancellationToken.None);

        item.Should().BeNull();
        handler.VerifyAll();
    }

    [Fact]
    public async Task GetNewStoryIdsAsync_logs_error_and_rethrows_on_http_failure()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("network error"));
        var mockLogger = new Mock<ILogger<HackerNewsClient>>();
        var sut = CreateSut(handler.Object, mockLogger.Object);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.GetNewStoryIdsAsync(CancellationToken.None));

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<HttpRequestException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetItemAsync_logs_error_and_rethrows_on_http_failure()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("network error"));
        var mockLogger = new Mock<ILogger<HackerNewsClient>>();
        var sut = CreateSut(handler.Object, mockLogger.Object);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.GetItemAsync(42, CancellationToken.None));

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<HttpRequestException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
