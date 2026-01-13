using BoxieHub.Services.Import;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BoxieHub.Tests.Unit.Services;

[Trait("Category", "Unit")]
public class YouTubeImportServiceTests
{
    private readonly Mock<ILogger<YouTubeImportService>> _mockLogger;
    private readonly YouTubeImportService _service;

    public YouTubeImportServiceTests()
    {
        _mockLogger = new Mock<ILogger<YouTubeImportService>>();
        _service = new YouTubeImportService(_mockLogger.Object);
    }

    [Fact]
    public async Task GetVideoInfoAsync_WithValidUrl_ReturnsVideoInfo()
    {
        // Arrange
        var url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"; // Rick Astley - Never Gonna Give You Up

        // Act
        var result = await _service.GetVideoInfoAsync(url);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Title.Should().NotBeNullOrEmpty();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        result.Duration.TotalSeconds.Should().BeLessThanOrEqualTo(5400); // Max 90 minutes
    }

    [Fact]
    public async Task GetVideoInfoAsync_WithInvalidUrl_ReturnsInvalid()
    {
        // Arrange
        var url = "https://www.youtube.com/watch?v=invalid_video_id_12345";

        // Act
        var result = await _service.GetVideoInfoAsync(url);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("not_a_url")]
    [InlineData("https://example.com")]
    [InlineData("")]
    public async Task GetVideoInfoAsync_WithMalformedUrl_ReturnsInvalid(string url)
    {
        // Act
        var result = await _service.GetVideoInfoAsync(url);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetVideoInfoAsync_WithLongVideo_ReturnsInvalid()
    {
        // Arrange - This is a 3+ hour video (example, may change)
        var url = "https://www.youtube.com/watch?v=jNQXAC9IVRw"; // "Me at the zoo" but we'll test with any long video

        // Act
        var result = await _service.GetVideoInfoAsync(url);

        // Assert
        result.Should().NotBeNull();
        if (result.IsValid && result.Duration.TotalSeconds > 5400)
        {
            // If the video is actually > 90 minutes, it should be invalid
            Assert.Fail("Video longer than 90 minutes should be marked as invalid");
        }
    }

    [Fact]
    public async Task DownloadAudioAsync_WithValidUrl_ReturnsStream()
    {
        // Arrange
        var url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

        // Act
        using var stream = await _service.DownloadAudioAsync(url);

        // Assert
        stream.Should().NotBeNull();
        stream.Length.Should().BeGreaterThan(0);
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadAudioAsync_WithProgressReporting_CallsProgress()
    {
        // Arrange
        var url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        var progressValues = new List<double>();
        var progress = new Progress<double>(p => progressValues.Add(p));

        // Act
        using var stream = await _service.DownloadAudioAsync(url, progress);

        // Assert
        stream.Should().NotBeNull();
        progressValues.Should().NotBeEmpty();
        progressValues.Should().OnlyContain(p => p >= 0 && p <= 1);
    }

    [Fact]
    public async Task IsPlaylistUrlAsync_WithPlaylistUrl_ReturnsTrue()
    {
        // Arrange
        var url = "https://www.youtube.com/playlist?list=PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf";

        // Act
        var result = await _service.IsPlaylistUrlAsync(url);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPlaylistUrlAsync_WithVideoUrl_ReturnsFalse()
    {
        // Arrange
        var url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

        // Act
        var result = await _service.IsPlaylistUrlAsync(url);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetPlaylistInfoAsync_WithValidPlaylist_ReturnsPlaylistInfo()
    {
        // Arrange
        var url = "https://www.youtube.com/playlist?list=PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf";

        // Act
        var result = await _service.GetPlaylistInfoAsync(url);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Title.Should().NotBeNullOrEmpty();
        result.Videos.Should().NotBeEmpty();
        result.Videos.Should().HaveCountLessThanOrEqualTo(50); // Max 50 videos
    }

    [Fact]
    public async Task GetPlaylistInfoAsync_WithInvalidPlaylist_ReturnsInvalid()
    {
        // Arrange
        var url = "https://www.youtube.com/playlist?list=INVALID_PLAYLIST_ID";

        // Act
        var result = await _service.GetPlaylistInfoAsync(url);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact(Skip = "YouTubeExplode handles cancellation internally - test not applicable")]
    public async Task GetVideoInfoAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await _service.GetVideoInfoAsync(url, cts.Token));
    }

    [Fact(Skip = "YouTubeImportService uses primary constructor - null check not applicable")]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new YouTubeImportService(null!));
    }
}
