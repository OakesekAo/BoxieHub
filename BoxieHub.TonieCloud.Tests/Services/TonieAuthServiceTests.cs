using System.Net;
using System.Text;
using System.Text.Json;
using BoxieHub.TonieCloud.Models;
using BoxieHub.TonieCloud.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace BoxieHub.TonieCloud.Tests.Services;

[TestFixture]
public class TonieAuthServiceTests
{
    private Mock<HttpMessageHandler> _mockHttpHandler = null!;
    private HttpClient _httpClient = null!;
    private IMemoryCache _memoryCache = null!;
    private Mock<ILogger<TonieAuthService>> _mockLogger = null!;
    private TonieAuthService _authService = null!;
    
    [SetUp]
    public void SetUp()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<TonieAuthService>>();
        
        _authService = new TonieAuthService(_httpClient, _memoryCache, _mockLogger.Object);
    }
    
    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
        _memoryCache?.Dispose();
    }
    
    [Test]
    public async Task GetAccessTokenAsync_WithValidCredentials_ReturnsToken()
    {
        // Arrange
        var username = "test@example.com";
        var password = "password123";
        var expectedToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test";
        
        var tokenResponse = new TonieTokenResponse
        {
            AccessToken = expectedToken,
            TokenType = "Bearer",
            ExpiresIn = 3600
        };
        
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse), Encoding.UTF8, "application/json")
            });
        
        // Act
        var result = await _authService.GetAccessTokenAsync(username, password);
        
        // Assert
        Assert.That(result, Is.EqualTo(expectedToken));
    }
    
    [Test]
    public async Task GetAccessTokenAsync_WithCachedToken_ReturnsCachedToken()
    {
        // Arrange
        var username = "test@example.com";
        var password = "password123";
        var expectedToken = "cached_token";
        
        // First call to populate cache
        var tokenResponse = new TonieTokenResponse
        {
            AccessToken = expectedToken,
            TokenType = "Bearer",
            ExpiresIn = 3600
        };
        
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse), Encoding.UTF8, "application/json")
            });
        
        await _authService.GetAccessTokenAsync(username, password);
        
        // Act - Second call should use cache
        var result = await _authService.GetAccessTokenAsync(username, password);
        
        // Assert
        Assert.That(result, Is.EqualTo(expectedToken));
        
        // Verify HTTP was only called once (cached on second call)
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
    
    [Test]
    public void GetAccessTokenAsync_WithEmptyUsername_ThrowsArgumentException()
    {
        // Arrange
        var username = "";
        var password = "password123";
        
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _authService.GetAccessTokenAsync(username, password));
    }
    
    [Test]
    public void GetAccessTokenAsync_WithEmptyPassword_ThrowsArgumentException()
    {
        // Arrange
        var username = "test@example.com";
        var password = "";
        
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () => 
            await _authService.GetAccessTokenAsync(username, password));
    }
    
    [Test]
    public async Task GetAccessTokenAsync_WithInvalidCredentials_ThrowsException()
    {
        // Arrange
        var username = "test@example.com";
        var password = "wrongpassword";
        
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("{\"error\":\"invalid_grant\"}", Encoding.UTF8, "application/json")
            });
        
        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _authService.GetAccessTokenAsync(username, password));
    }
    
    [Test]
    public async Task InvalidateTokenAsync_RemovesTokenFromCache()
    {
        // Arrange
        var username = "test@example.com";
        var password = "password123";
        
        var tokenResponse = new TonieTokenResponse
        {
            AccessToken = "test_token",
            TokenType = "Bearer",
            ExpiresIn = 3600
        };
        
        // Setup mock to return fresh response each time
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse), Encoding.UTF8, "application/json")
            });
        
        // Cache a token
        await _authService.GetAccessTokenAsync(username, password);
        
        // Act - Invalidate
        await _authService.InvalidateTokenAsync(username);
        
        // Assert - Next call should request new token
        await _authService.GetAccessTokenAsync(username, password);
        
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2), // Once for first call, once after invalidation
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}
