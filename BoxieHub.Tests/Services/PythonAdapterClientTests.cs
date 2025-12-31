using BoxieHub.Services.PythonAdapter;
using BoxieHub.Services.PythonAdapter.Dtos;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using System.Net;

namespace BoxieHub.Tests.Services.PythonAdapter;

/// <summary>
/// Unit tests for PythonAdapterClient.
/// Tests HTTP communication with the Python adapter service.
/// </summary>
[TestFixture]
public class PythonAdapterClientTests
{
    private PythonAdapterClient _client;
    private Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private ILogger<PythonAdapterClient> _logger;

    [SetUp]
    public void SetUp()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };

        _logger = new Mock<ILogger<PythonAdapterClient>>().Object;
        _client = new PythonAdapterClient(httpClient, _logger);
    }

    [Test]
    public async Task GetHealthAsync_WhenServiceIsHealthy_ReturnsHealthResponse()
    {
        // Arrange
        var healthResponse = new HealthResponseDto
        {
            Status = "healthy",
            Version = "0.1.0",
            CheckedAt = DateTime.UtcNow
        };

        var json = System.Text.Json.JsonSerializer.Serialize(healthResponse);
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri.PathAndQuery.Contains("/health")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponseMessage);

        // Act
        var result = await _client.GetHealthAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Status, Is.EqualTo("healthy"));
        Assert.That(result.Version, Is.EqualTo("0.1.0"));
    }

    [Test]
    public async Task GetHealthAsync_WhenServiceIsDown_ThrowsHttpRequestException()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        // Act & Assert
        Assert.ThrowsAsync<HttpRequestException>(() => _client.GetHealthAsync());
    }

    [Test]
    public async Task SyncAsync_WithValidRequest_ReturnsSyncResponse()
    {
        // Arrange
        var syncRequest = new SyncRequestDto
        {
            CreativeTonieExternalId = "tonie-001",
            Tracks = new List<SyncTrackDto>
            {
                new SyncTrackDto { Title = "Story 1", SourceUrl = "http://localhost/audio/1" }
            }
        };

        var syncResponse = new SyncResponseDto
        {
            Success = true,
            Message = "Successfully synced 1 track",
            TracksProcessed = 1
        };

        var json = System.Text.Json.JsonSerializer.Serialize(syncResponse);
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post && req.RequestUri.PathAndQuery.Contains("/sync")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponseMessage);

        // Act
        var result = await _client.SyncAsync(syncRequest);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Message, Is.EqualTo("Successfully synced 1 track"));
        Assert.That(result.TracksProcessed, Is.EqualTo(1));
    }

    [Test]
    public async Task SyncAsync_WithFailedSync_ReturnsSyncResponseWithSuccess_False()
    {
        // Arrange
        var syncRequest = new SyncRequestDto
        {
            CreativeTonieExternalId = "tonie-invalid",
            Tracks = new List<SyncTrackDto>
            {
                new SyncTrackDto { Title = "Story 1", SourceUrl = "http://localhost/audio/1" }
            }
        };

        var syncResponse = new SyncResponseDto
        {
            Success = false,
            Message = "Sync failed",
            ErrorDetails = "Device not found",
            TracksProcessed = 0
        };

        var json = System.Text.Json.JsonSerializer.Serialize(syncResponse);
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponseMessage);

        // Act
        var result = await _client.SyncAsync(syncRequest);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorDetails, Is.EqualTo("Device not found"));
    }

    [Test]
    public void SyncAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => _client.SyncAsync(null));
    }

    [Test]
    public async Task SyncAsync_WithMultipleTracks_SyncsAllTracks()
    {
        // Arrange
        var syncRequest = new SyncRequestDto
        {
            CreativeTonieExternalId = "tonie-001",
            Tracks = new List<SyncTrackDto>
            {
                new SyncTrackDto { Title = "Story 1", SourceUrl = "http://localhost/audio/1" },
                new SyncTrackDto { Title = "Story 2", SourceUrl = "http://localhost/audio/2" },
                new SyncTrackDto { Title = "Story 3", SourceUrl = "http://localhost/audio/3" }
            }
        };

        var syncResponse = new SyncResponseDto
        {
            Success = true,
            Message = "Successfully synced 3 tracks",
            TracksProcessed = 3
        };

        var json = System.Text.Json.JsonSerializer.Serialize(syncResponse);
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponseMessage);

        // Act
        var result = await _client.SyncAsync(syncRequest);

        // Assert
        Assert.That(result.TracksProcessed, Is.EqualTo(3));
    }
}
