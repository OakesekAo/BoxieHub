using System.Net;
using System.Text;
using System.Text.Json;
using BoxieHub.TonieCloud.Models;
using BoxieHub.TonieCloud.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace BoxieHub.TonieCloud.Tests.Services;

[TestFixture]
public class TonieCloudClientTests
{
    private Mock<HttpMessageHandler> _mockHttpHandler = null!;
    private HttpClient _httpClient = null!;
    private Mock<ITonieAuthService> _mockAuthService = null!;
    private Mock<ILogger<TonieCloudClient>> _mockLogger = null!;
    private TonieCloudClient _client = null!;
    
    private const string TestUsername = "test@example.com";
    private const string TestPassword = "password123";
    private const string TestToken = "test_bearer_token";
    
    [SetUp]
    public void SetUp()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object)
        {
            BaseAddress = new Uri("https://api.tonie.cloud/v2")
        };
        
        _mockAuthService = new Mock<ITonieAuthService>();
        _mockLogger = new Mock<ILogger<TonieCloudClient>>();
        
        // Setup auth service to return test token
        _mockAuthService
            .Setup(x => x.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestToken);
        
        _client = new TonieCloudClient(_httpClient, _mockAuthService.Object, _mockLogger.Object);
    }
    
    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }
    
    #region GetHouseholdsAsync Tests
    
    [Test]
    public async Task GetHouseholdsAsync_WithValidResponse_ReturnsHouseholds()
    {
        // Arrange
        var expectedHouseholds = new List<HouseholdDto>
        {
            new() { Id = "household-1", Name = "Test Family", Access = "owner", CanLeave = false },
            new() { Id = "household-2", Name = "Other Family", Access = "member", CanLeave = true }
        };
        
        SetupHttpResponse(HttpStatusCode.OK, expectedHouseholds);
        
        // Act
        var result = await _client.GetHouseholdsAsync(TestUsername, TestPassword);
        
        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Name, Is.EqualTo("Test Family"));
        Assert.That(result[1].Access, Is.EqualTo("member"));
    }
    
    [Test]
    public async Task GetHouseholdsAsync_WithEmptyResponse_ReturnsEmptyList()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, new List<HouseholdDto>());
        
        // Act
        var result = await _client.GetHouseholdsAsync(TestUsername, TestPassword);
        
        // Assert
        Assert.That(result, Is.Empty);
    }
    
    [Test]
    public void GetHouseholdsAsync_WithUnauthorized_ThrowsException()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.Unauthorized, new { error = "unauthorized" });
        
        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _client.GetHouseholdsAsync(TestUsername, TestPassword));
    }
    
    #endregion
    
    #region GetCreativeToniesByUserAsync Tests
    
    [Test]
    public async Task GetCreativeToniesByUserAsync_WithValidResponse_ReturnsTonies()
    {
        // Arrange
        var expectedTonies = new List<CreativeTonieDto>
        {
            new()
            {
                Id = "72EB4F1D500304E0",
                HouseholdId = "household-1",
                Name = "Creative-Tonie",
                ImageUrl = "https://cdn.tonies.de/test.png",
                SecondsPresent = 2694.0f,
                SecondsRemaining = 2705.0f,
                ChaptersPresent = 29,
                ChaptersRemaining = 70,
                Transcoding = false
            }
        };
        
        SetupHttpResponse(HttpStatusCode.OK, expectedTonies);
        
        // Act
        var result = await _client.GetCreativeToniesByUserAsync(TestUsername, TestPassword);
        
        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo("72EB4F1D500304E0"));
        Assert.That(result[0].ChaptersPresent, Is.EqualTo(29));
    }
    
    [Test]
    public async Task GetCreativeToniesByUserAsync_UsesAuthToken()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, new List<CreativeTonieDto>());
        
        // Act
        await _client.GetCreativeToniesByUserAsync(TestUsername, TestPassword);
        
        // Assert
        _mockAuthService.Verify(
            x => x.GetAccessTokenAsync(TestUsername, TestPassword, It.IsAny<CancellationToken>()), 
            Times.Once);
    }
    
    #endregion
    
    #region GetCreativeTonieDetailsAsync Tests
    
    [Test]
    public async Task GetCreativeTonieDetailsAsync_WithValidResponse_ReturnsTonieWithChapters()
    {
        // Arrange
        var tonieId = "72EB4F1D500304E0";
        var expectedTonie = new CreativeTonieDto
        {
            Id = tonieId,
            HouseholdId = "household-1",
            Name = "Creative-Tonie",
            ImageUrl = "https://cdn.tonies.de/test.png",
            SecondsPresent = 2694.0f,
            SecondsRemaining = 2705.0f,
            ChaptersPresent = 2,
            ChaptersRemaining = 97,
            Transcoding = false,
            Chapters = new List<ChapterDto>
            {
                new() { Id = "chapter-1", Title = "Song 1", Seconds = 180.5f },
                new() { Id = "chapter-2", Title = "Song 2", Seconds = 210.3f }
            }
        };
        
        SetupHttpResponse(HttpStatusCode.OK, expectedTonie);
        
        // Act
        var result = await _client.GetCreativeTonieDetailsAsync(TestUsername, TestPassword, tonieId);
        
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(tonieId));
        Assert.That(result.Chapters, Has.Count.EqualTo(2));
        Assert.That(result.Chapters![0].Title, Is.EqualTo("Song 1"));
    }
    
    [Test]
    public void GetCreativeTonieDetailsAsync_WithEmptyTonieId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _client.GetCreativeTonieDetailsAsync(TestUsername, TestPassword, ""));
    }
    
    [Test]
    public void GetCreativeTonieDetailsAsync_WithNotFound_ThrowsException()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.NotFound, new { error = "not_found" });
        
        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _client.GetCreativeTonieDetailsAsync(TestUsername, TestPassword, "invalid-id"));
    }
    
    #endregion
    
    #region SyncAudioAsync Tests
    
    [Test]
    public async Task SyncAudioAsync_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var tonieId = "72EB4F1D500304E0";
        var title = "Test Audio";
        using var audioStream = new MemoryStream(Encoding.UTF8.GetBytes("fake audio data"));
        
        SetupHttpResponse(HttpStatusCode.OK, new { success = true });
        
        // Act
        var result = await _client.SyncAudioAsync(TestUsername, TestPassword, tonieId, audioStream, title);
        
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.TracksProcessed, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("Successfully"));
    }
    
    [Test]
    public void SyncAudioAsync_WithEmptyTonieId_ThrowsArgumentException()
    {
        // Arrange
        using var audioStream = new MemoryStream();
        
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _client.SyncAudioAsync(TestUsername, TestPassword, "", audioStream, "title"));
    }
    
    [Test]
    public void SyncAudioAsync_WithNullStream_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () => 
            await _client.SyncAudioAsync(TestUsername, TestPassword, "tonie-id", null!, "title"));
    }
    
    [Test]
    public void SyncAudioAsync_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        using var audioStream = new MemoryStream();
        
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _client.SyncAudioAsync(TestUsername, TestPassword, "tonie-id", audioStream, ""));
    }
    
    [Test]
    public async Task SyncAudioAsync_WithServerError_ReturnsFailure()
    {
        // Arrange
        var tonieId = "72EB4F1D500304E0";
        using var audioStream = new MemoryStream(Encoding.UTF8.GetBytes("fake audio"));
        
        SetupHttpResponse(HttpStatusCode.InternalServerError, new { error = "server_error" });
        
        // Act
        var result = await _client.SyncAudioAsync(TestUsername, TestPassword, tonieId, audioStream, "Test");
        
        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorDetails, Is.Not.Null);
    }
    
    #endregion
    
    #region Helper Methods
    
    private void SetupHttpResponse<T>(HttpStatusCode statusCode, T content)
    {
        var jsonContent = JsonSerializer.Serialize(content);
        
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            });
    }
    
    #endregion
}
