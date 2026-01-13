using BoxieHub.Data;
using BoxieHub.Models;
using BoxieHub.Services.Import;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BoxieHub.Tests.Unit.Services;

[Trait("Category", "Unit")]
public class ImportJobServiceTests : IDisposable
{
    private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;
    private readonly Mock<IDbContextFactory<ApplicationDbContext>> _mockDbContextFactory;
    private readonly Mock<IYouTubeImportService> _mockYouTubeService;
    private readonly Mock<IPodcastImportService> _mockPodcastService;
    private readonly Mock<ILogger<ImportJobService>> _mockLogger;
    private readonly ImportJobService _service;
    private readonly string _testUserId = "test-user-123";

    public ImportJobServiceTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ImportJobServiceTests_{Guid.NewGuid()}")
            .Options;

        _mockDbContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        _mockDbContextFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ApplicationDbContext(_dbContextOptions));

        _mockYouTubeService = new Mock<IYouTubeImportService>();
        _mockPodcastService = new Mock<IPodcastImportService>();
        _mockLogger = new Mock<ILogger<ImportJobService>>();

        _service = new ImportJobService(
            _mockDbContextFactory.Object,
            _mockYouTubeService.Object,
            _mockPodcastService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CreateYouTubeImportJobAsync_WithValidUrl_CreatesJob()
    {
        // Arrange
        var url = "https://www.youtube.com/watch?v=test123";
        var videoInfo = new YouTubeVideoInfo
        {
            Id = "test123",
            Title = "Test Video",
            Description = "Test Description",
            Author = "Test Author",
            Duration = TimeSpan.FromMinutes(10),
            ThumbnailUrl = "https://example.com/thumb.jpg",
            UploadDate = DateTimeOffset.UtcNow,
            IsValid = true
        };

        _mockYouTubeService
            .Setup(s => s.GetVideoInfoAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(videoInfo);

        // Act
        var job = await _service.CreateYouTubeImportJobAsync(_testUserId, url);

        // Assert
        job.Should().NotBeNull();
        job.Id.Should().BeGreaterThan(0);
        job.UserId.Should().Be(_testUserId);
        job.Source.Should().Be(ImportSource.YouTube);
        job.SourceUrl.Should().Be(url);
        job.SourceTitle.Should().Be(videoInfo.Title);
        job.StatusEnum.Should().Be(ImportJobStatus.Pending);
        job.SourceDurationSeconds.Should().Be((float)videoInfo.Duration.TotalSeconds);
    }

    [Fact]
    public async Task CreateYouTubeImportJobAsync_WithInvalidVideo_ThrowsException()
    {
        // Arrange
        var url = "https://www.youtube.com/watch?v=invalid";
        var videoInfo = new YouTubeVideoInfo
        {
            IsValid = false,
            ErrorMessage = "Video not found"
        };

        _mockYouTubeService
            .Setup(s => s.GetVideoInfoAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(videoInfo);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.CreateYouTubeImportJobAsync(_testUserId, url));
    }

    [Fact]
    public async Task CreateYouTubeImportJobAsync_WithCustomTitle_UsesCustomTitle()
    {
        // Arrange
        var url = "https://www.youtube.com/watch?v=test123";
        var customTitle = "My Custom Title";
        var videoInfo = new YouTubeVideoInfo
        {
            Id = "test123",
            Title = "Original Title",
            Duration = TimeSpan.FromMinutes(5),
            IsValid = true
        };

        _mockYouTubeService
            .Setup(s => s.GetVideoInfoAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(videoInfo);

        // Act
        var job = await _service.CreateYouTubeImportJobAsync(_testUserId, url, customTitle);

        // Assert
        job.SourceTitle.Should().Be(customTitle);
    }

    [Fact]
    public async Task CreateYouTubeImportJobAsync_WithLongTitle_TruncatesToMaxLength()
    {
        // Arrange
        var url = "https://www.youtube.com/watch?v=test123";
        var longTitle = new string('A', 600); // Exceeds 500 char limit
        var videoInfo = new YouTubeVideoInfo
        {
            Id = "test123",
            Title = longTitle,
            Duration = TimeSpan.FromMinutes(5),
            IsValid = true
        };

        _mockYouTubeService
            .Setup(s => s.GetVideoInfoAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(videoInfo);

        // Act
        var job = await _service.CreateYouTubeImportJobAsync(_testUserId, url);

        // Assert
        job.SourceTitle.Should().HaveLength(500);
        job.SourceTitle.Should().EndWith("...");
    }

    [Fact]
    public async Task CreateYouTubeBatchImportAsync_WithMultipleUrls_CreatesMultipleJobs()
    {
        // Arrange
        var urls = new List<string>
        {
            "https://www.youtube.com/watch?v=test1",
            "https://www.youtube.com/watch?v=test2",
            "https://www.youtube.com/watch?v=test3"
        };

        _mockYouTubeService
            .Setup(s => s.GetVideoInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubeVideoInfo
            {
                Id = "test",
                Title = "Test",
                Duration = TimeSpan.FromMinutes(5),
                IsValid = true
            });

        // Act
        var jobs = await _service.CreateYouTubeBatchImportAsync(_testUserId, urls);

        // Assert
        jobs.Should().HaveCount(3);
        jobs.All(j => j.UserId == _testUserId).Should().BeTrue();
        jobs.All(j => j.Source == ImportSource.YouTube).Should().BeTrue();
    }

    [Fact]
    public async Task CreateYouTubeBatchImportAsync_WithSomeInvalidUrls_CreatesOnlyValidJobs()
    {
        // Arrange
        var urls = new List<string>
        {
            "https://www.youtube.com/watch?v=valid",
            "https://www.youtube.com/watch?v=invalid",
            "https://www.youtube.com/watch?v=valid2"
        };

        _mockYouTubeService
            .Setup(s => s.GetVideoInfoAsync("https://www.youtube.com/watch?v=valid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubeVideoInfo { Id = "valid", Title = "Valid", Duration = TimeSpan.FromMinutes(5), IsValid = true });

        _mockYouTubeService
            .Setup(s => s.GetVideoInfoAsync("https://www.youtube.com/watch?v=invalid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubeVideoInfo { IsValid = false, ErrorMessage = "Invalid" });

        _mockYouTubeService
            .Setup(s => s.GetVideoInfoAsync("https://www.youtube.com/watch?v=valid2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YouTubeVideoInfo { Id = "valid2", Title = "Valid2", Duration = TimeSpan.FromMinutes(5), IsValid = true });

        // Act
        var jobs = await _service.CreateYouTubeBatchImportAsync(_testUserId, urls);

        // Assert
        jobs.Should().HaveCount(2); // Only valid ones
    }

    [Fact]
    public async Task CreatePodcastImportJobAsync_WithValidEpisode_CreatesJob()
    {
        // Arrange
        var feedUrl = "https://example.com/feed.xml";
        var episode = new PodcastEpisode
        {
            Id = "ep-123",
            Title = "Episode 1",
            Description = "Episode description",
            AudioUrl = "https://example.com/ep1.mp3",
            Duration = TimeSpan.FromMinutes(30),
            FileSizeBytes = 50000000,
            PublishDate = DateTimeOffset.UtcNow,
            ImageUrl = "https://example.com/ep.jpg"
        };

        // Act
        var job = await _service.CreatePodcastImportJobAsync(_testUserId, feedUrl, episode);

        // Assert
        job.Should().NotBeNull();
        job.Id.Should().BeGreaterThan(0);
        job.UserId.Should().Be(_testUserId);
        job.Source.Should().Be(ImportSource.Podcast);
        job.SourceUrl.Should().Be(episode.AudioUrl);
        job.SourceTitle.Should().Be(episode.Title);
        job.StatusEnum.Should().Be(ImportJobStatus.Pending);
        job.SourceDurationSeconds.Should().Be((float)episode.Duration.TotalSeconds);
    }

    [Fact]
    public async Task GetJobAsync_WithExistingJob_ReturnsJob()
    {
        // Arrange
        await using var dbContext = new ApplicationDbContext(_dbContextOptions);
        var job = new ImportJob
        {
            UserId = _testUserId,
            Source = ImportSource.YouTube,
            SourceUrl = "https://example.com",
            SourceTitle = "Test",
            StatusEnum = ImportJobStatus.Pending,
            Created = DateTimeOffset.UtcNow
        };
        dbContext.ImportJobs.Add(job);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetJobAsync(job.Id, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(job.Id);
    }

    [Fact]
    public async Task GetJobAsync_WithNonExistentJob_ReturnsNull()
    {
        // Act
        var result = await _service.GetJobAsync(99999, _testUserId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetJobAsync_WithDifferentUser_ReturnsNull()
    {
        // Arrange
        await using var dbContext = new ApplicationDbContext(_dbContextOptions);
        var job = new ImportJob
        {
            UserId = "other-user",
            Source = ImportSource.YouTube,
            SourceUrl = "https://example.com",
            SourceTitle = "Test",
            StatusEnum = ImportJobStatus.Pending,
            Created = DateTimeOffset.UtcNow
        };
        dbContext.ImportJobs.Add(job);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetJobAsync(job.Id, _testUserId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserJobsAsync_ReturnsUserJobsOnly()
    {
        // Arrange
        await using var dbContext = new ApplicationDbContext(_dbContextOptions);
        var userJob1 = new ImportJob { UserId = _testUserId, Source = ImportSource.YouTube, SourceUrl = "url1", SourceTitle = "Title1", StatusEnum = ImportJobStatus.Pending, Created = DateTimeOffset.UtcNow.AddDays(-2) };
        var userJob2 = new ImportJob { UserId = _testUserId, Source = ImportSource.Podcast, SourceUrl = "url2", SourceTitle = "Title2", StatusEnum = ImportJobStatus.Completed, Created = DateTimeOffset.UtcNow.AddDays(-1) };
        var otherUserJob = new ImportJob { UserId = "other-user", Source = ImportSource.YouTube, SourceUrl = "url3", SourceTitle = "Title3", StatusEnum = ImportJobStatus.Pending, Created = DateTimeOffset.UtcNow };

        dbContext.ImportJobs.AddRange(userJob1, userJob2, otherUserJob);
        await dbContext.SaveChangesAsync();

        // Act
        var jobs = await _service.GetUserJobsAsync(_testUserId);

        // Assert
        jobs.Should().HaveCount(2);
        jobs.Should().OnlyContain(j => j.UserId == _testUserId);
    }

    [Fact]
    public async Task GetUserJobsAsync_ReturnsOrderedByCreatedDesc()
    {
        // Arrange
        await using var dbContext = new ApplicationDbContext(_dbContextOptions);
        var job1 = new ImportJob { UserId = _testUserId, Source = ImportSource.YouTube, SourceUrl = "url1", SourceTitle = "Title1", StatusEnum = ImportJobStatus.Pending, Created = DateTimeOffset.UtcNow.AddDays(-2) };
        var job2 = new ImportJob { UserId = _testUserId, Source = ImportSource.YouTube, SourceUrl = "url2", SourceTitle = "Title2", StatusEnum = ImportJobStatus.Pending, Created = DateTimeOffset.UtcNow.AddDays(-1) };
        var job3 = new ImportJob { UserId = _testUserId, Source = ImportSource.YouTube, SourceUrl = "url3", SourceTitle = "Title3", StatusEnum = ImportJobStatus.Pending, Created = DateTimeOffset.UtcNow };

        dbContext.ImportJobs.AddRange(job1, job2, job3);
        await dbContext.SaveChangesAsync();

        // Act
        var jobs = await _service.GetUserJobsAsync(_testUserId);

        // Assert
        jobs.Should().HaveCount(3);
        jobs[0].Created.Should().BeAfter(jobs[1].Created);
        jobs[1].Created.Should().BeAfter(jobs[2].Created);
    }

    [Fact]
    public async Task CancelJobAsync_WithPendingJob_CancelsJob()
    {
        // Arrange
        await using var dbContext = new ApplicationDbContext(_dbContextOptions);
        var job = new ImportJob
        {
            UserId = _testUserId,
            Source = ImportSource.YouTube,
            SourceUrl = "https://example.com",
            SourceTitle = "Test",
            StatusEnum = ImportJobStatus.Pending,
            Created = DateTimeOffset.UtcNow
        };
        dbContext.ImportJobs.Add(job);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await _service.CancelJobAsync(job.Id, _testUserId);

        // Assert
        result.Should().BeTrue();
        
        // Create new context to verify changes
        await using var verifyContext = new ApplicationDbContext(_dbContextOptions);
        var cancelledJob = await verifyContext.ImportJobs.FindAsync(job.Id);
        cancelledJob!.StatusEnum.Should().Be(ImportJobStatus.Cancelled);
        cancelledJob.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelJobAsync_WithCompletedJob_ReturnsFalse()
    {
        // Arrange
        await using var dbContext = new ApplicationDbContext(_dbContextOptions);
        var job = new ImportJob
        {
            UserId = _testUserId,
            Source = ImportSource.YouTube,
            SourceUrl = "https://example.com",
            SourceTitle = "Test",
            StatusEnum = ImportJobStatus.Completed,
            Created = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };
        dbContext.ImportJobs.Add(job);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await _service.CancelJobAsync(job.Id, _testUserId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelJobAsync_WithNonExistentJob_ReturnsFalse()
    {
        // Act
        var result = await _service.CancelJobAsync(99999, _testUserId);

        // Assert
        result.Should().BeFalse();
    }

    public void Dispose()
    {
        // Cleanup happens automatically with InMemory database
    }
}
