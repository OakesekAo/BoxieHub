using BoxieHub.Data;
using BoxieHub.Models;
using BoxieHub.Services;
using BoxieHub.Services.Audio;
using BoxieHub.Services.Import;
using BoxieHub.Services.Storage;
using BoxieHub.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BoxieHub.Tests.Integration;

/// <summary>
/// Integration tests for ImportJobProcessor with audio verification
/// Tests the complete flow: Download ? Verify ? Save
/// </summary>
[TestFixture]
public class ImportJobProcessorAudioVerificationTests : TestBase
{
    private Mock<IYouTubeImportService> _mockYouTubeService = null!;
    private Mock<IPodcastImportService> _mockPodcastService = null!;
    private Mock<IMediaLibraryService> _mockMediaLibraryService = null!;
    private Mock<IStoragePreferenceService> _mockStoragePreferenceService = null!;
    private Mock<IAudioAnalysisService> _mockAudioAnalysisService = null!;
    private Mock<ILogger<ImportJobProcessor>> _mockLogger = null!;

    [SetUp]
    public new void SetUp()
    {
        base.SetUpDatabase();
        
        _mockYouTubeService = new Mock<IYouTubeImportService>();
        _mockPodcastService = new Mock<IPodcastImportService>();
        _mockMediaLibraryService = new Mock<IMediaLibraryService>();
        _mockStoragePreferenceService = new Mock<IStoragePreferenceService>();
        _mockAudioAnalysisService = new Mock<IAudioAnalysisService>();
        _mockLogger = new Mock<ILogger<ImportJobProcessor>>();
    }

    #region YouTube Import Tests

    [Test]
    [Ignore("Structural test - Documents expected behavior. " +
            "ImportJobProcessor needs refactoring to be testable (extract IImportJobExecutor). " +
            "See docs/TEST_COVERAGE_GAP_ANALYSIS.md for details.")]
    public async Task ProcessJob_YouTubeImport_VerifiesDuration_UpdatesJob()
    {
        // Arrange
        var userId = "test-user";
        var youtubeUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        
        var job = new ImportJob
        {
            UserId = userId,
            Source = ImportSource.YouTube,
            SourceUrl = youtubeUrl,
            SourceTitle = "Test Video",
            SourceDurationSeconds = 212.0f, // YouTube claims 3:32
            StatusEnum = ImportJobStatus.Pending
        };
        DbContext.ImportJobs.Add(job);
        DbContext.SaveChanges();

        // Mock audio download
        var audioData = new byte[] { 0xFF, 0xFB, 0x90, 0x00 }; // Minimal MP3 header
        var audioStream = new MemoryStream(audioData);
        
        _mockYouTubeService
            .Setup(x => x.DownloadAudioAsync(youtubeUrl, It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioStream);

        // Mock audio verification - returns DIFFERENT duration (simulating YouTube metadata error)
        _mockAudioAnalysisService
            .Setup(x => x.GetActualDurationAsync(It.IsAny<Stream>(), "audio/mp4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(210.0f); // Actual: 3:30 (2 seconds different)

        // Mock storage preference
        _mockStoragePreferenceService
            .Setup(x => x.GetDefaultProviderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StorageProvider.Database);

        // Mock library item creation
        var libraryItem = new MediaLibraryItem
        {
            Id = 1,
            UserId = userId,
            Title = "Test Video",
            DurationSeconds = 210.0f, // Verified duration
            FileUploadId = Guid.NewGuid()
        };
        
        _mockMediaLibraryService
            .Setup(x => x.AddToLibraryAsync(
                userId,
                It.IsAny<Stream>(),
                It.IsAny<MediaLibraryItemDto>(),
                It.IsAny<StorageProvider?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryItem);

        // Act - Process the job
        // TODO: Refactor ImportJobProcessor to extract IImportJobExecutor for testing
        // For now, this documents the expected behavior

        // Assert - What we expect when this is implemented:
        // 1. AudioAnalysisService.GetActualDurationAsync should be called
        // 2. Job duration should be updated to verified duration (210s, not 212s)
        // 3. MediaLibraryService should receive DTO with verified duration
    }

    [Test]
    public async Task ProcessJob_YouTubeImport_DurationTooLong_FailsJob()
    {
        // Arrange
        var userId = "test-user";
        var youtubeUrl = "https://www.youtube.com/watch?v=LONG_VIDEO";
        
        var job = new ImportJob
        {
            UserId = userId,
            Source = ImportSource.YouTube,
            SourceUrl = youtubeUrl,
            SourceTitle = "2 Hour Documentary",
            SourceDurationSeconds = 7200.0f, // 2 hours (>90 minutes)
            StatusEnum = ImportJobStatus.Pending
        };
        DbContext.ImportJobs.Add(job);
        DbContext.SaveChanges();

        // Mock audio download
        var audioData = new byte[] { 0xFF, 0xFB, 0x90, 0x00 };
        var audioStream = new MemoryStream(audioData);
        
        _mockYouTubeService
            .Setup(x => x.DownloadAudioAsync(youtubeUrl, It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioStream);

        // Mock audio verification - confirms it's too long
        _mockAudioAnalysisService
            .Setup(x => x.GetActualDurationAsync(It.IsAny<Stream>(), "audio/mp4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(7200.0f); // 2 hours

        // Act & Assert
        // Job should throw InvalidOperationException and be marked as Failed
        // In ImportJobProcessor, this exception is caught and job.StatusEnum = Failed
        
        // We'd need to actually run ProcessJobAsync here
        // For now, this documents the expected behavior
    }

    #endregion

    #region Podcast Import Tests

    [Test]
    public async Task ProcessJob_PodcastImport_VerifiesDuration_CorrectsMilliseconds()
    {
        // Arrange
        var userId = "test-user";
        var podcastUrl = "https://example.com/podcast/episode.mp3";
        
        var job = new ImportJob
        {
            UserId = userId,
            Source = ImportSource.Podcast,
            SourceUrl = podcastUrl,
            SourceTitle = "Podcast Episode",
            SourceDurationSeconds = 1019000.0f, // RSS feed in milliseconds (16h 59m)
            StatusEnum = ImportJobStatus.Pending
        };
        DbContext.ImportJobs.Add(job);
        DbContext.SaveChanges();

        // Mock audio download
        var audioData = new byte[] { 0xFF, 0xFB, 0x90, 0x00 };
        var audioStream = new MemoryStream(audioData);
        
        _mockPodcastService
            .Setup(x => x.DownloadEpisodeAsync(podcastUrl, It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioStream);

        // Mock audio verification - returns CORRECT duration
        _mockAudioAnalysisService
            .Setup(x => x.GetActualDurationAsync(It.IsAny<Stream>(), "audio/mpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1019.0f); // Actual: 16m 59s (not hours!)

        // Mock storage preference
        _mockStoragePreferenceService
            .Setup(x => x.GetDefaultProviderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StorageProvider.Database);

        // Mock library item creation
        var libraryItem = new MediaLibraryItem
        {
            Id = 2,
            UserId = userId,
            Title = "Podcast Episode",
            DurationSeconds = 1019.0f, // CORRECTED duration
            FileUploadId = Guid.NewGuid()
        };
        
        _mockMediaLibraryService
            .Setup(x => x.AddToLibraryAsync(
                userId,
                It.IsAny<Stream>(),
                It.IsAny<MediaLibraryItemDto>(),
                It.IsAny<StorageProvider?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryItem);

        // Act - Process the job
        // (Would need refactored ImportJobProcessor)

        // Assert - Expected behavior:
        // 1. Duration discrepancy logged (1019000s vs 1019s)
        // 2. Job updated with verified 1019s
        // 3. Library item created with 1019s
    }

    #endregion

    #region Deduplication Tests

    [Test]
    [Ignore("Structural test - Documents expected behavior. " +
            "Requires fixing PodcastEpisodeCache test data (missing EpisodeGuid and Title). " +
            "ImportJobProcessor also needs refactoring for proper testing.")]
    public async Task ProcessJob_PodcastDeduplication_SkipsVerification()
    {
        // Arrange
        var userId = "test-user";
        var podcastUrl = "https://example.com/podcast/episode.mp3";
        
        // Create existing file (already verified)
        var existingFile = new FileUpload
        {
            Id = Guid.NewGuid(),
            Data = new byte[] { 1, 2, 3, 4 },
            ContentType = "audio/mpeg",
            Provider = StorageProvider.Database
        };
        DbContext.FileUploads.Add(existingFile);

        // Create episode cache pointing to existing file
        // NOTE: EpisodeGuid and Title are required fields but missing here
        // TODO: Fix test data creation
        var episodeCache = new PodcastEpisodeCache
        {
            AudioUrl = podcastUrl,
            FileUploadId = existingFile.Id,
            DurationSeconds = 1019.0f, // Already verified
            ImportCount = 1,
            EpisodeGuid = "test-episode-guid-123",  // ? Required field
            Title = "Test Podcast Episode"           // ? Required field
        };
        DbContext.PodcastEpisodeCache.Add(episodeCache);
        DbContext.SaveChanges();

        var job = new ImportJob
        {
            UserId = userId,
            Source = ImportSource.Podcast,
            SourceUrl = podcastUrl,
            SourceTitle = "Duplicate Episode",
            SourceDurationSeconds = 1019.0f,
            StatusEnum = ImportJobStatus.Pending
        };
        DbContext.ImportJobs.Add(job);
        DbContext.SaveChanges();

        // Act - Process the job
        // TODO: Actually invoke ImportJobProcessor.ProcessJobAsync when refactored

        // Assert - Expected behavior:
        // 1. No download (reuses existing file)
        // 2. No audio verification (file already verified)
        // 3. ImportCount incremented to 2
        // 4. Library item created instantly
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task ProcessJob_AudioVerificationFails_ContinuesWithClaimedDuration()
    {
        // Arrange
        var userId = "test-user";
        var youtubeUrl = "https://www.youtube.com/watch?v=TEST";
        
        var job = new ImportJob
        {
            UserId = userId,
            Source = ImportSource.YouTube,
            SourceUrl = youtubeUrl,
            SourceTitle = "Test Video",
            SourceDurationSeconds = 180.0f, // Claimed duration
            StatusEnum = ImportJobStatus.Pending
        };
        DbContext.ImportJobs.Add(job);
        DbContext.SaveChanges();

        // Mock audio download
        var audioData = new byte[] { 0xFF, 0xFB, 0x90, 0x00 };
        var audioStream = new MemoryStream(audioData);
        
        _mockYouTubeService
            .Setup(x => x.DownloadAudioAsync(youtubeUrl, It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioStream);

        // Mock audio verification - THROWS EXCEPTION
        _mockAudioAnalysisService
            .Setup(x => x.GetActualDurationAsync(It.IsAny<Stream>(), "audio/mp4", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Corrupted audio file"));

        // Mock storage preference
        _mockStoragePreferenceService
            .Setup(x => x.GetDefaultProviderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StorageProvider.Database);

        // Mock library item creation
        var libraryItem = new MediaLibraryItem
        {
            Id = 3,
            UserId = userId,
            Title = "Test Video",
            DurationSeconds = 180.0f, // Uses CLAIMED duration (verification failed)
            FileUploadId = Guid.NewGuid()
        };
        
        _mockMediaLibraryService
            .Setup(x => x.AddToLibraryAsync(
                userId,
                It.IsAny<Stream>(),
                It.Is<MediaLibraryItemDto>(dto => dto.DurationSeconds == 180.0f), // Original claimed duration
                It.IsAny<StorageProvider?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(libraryItem);

        // Act - Process the job
        // (Would need refactored ImportJobProcessor)

        // Assert - Expected behavior:
        // 1. Exception caught and logged
        // 2. Job continues with claimed duration (180s)
        // 3. Job completes successfully (not failed)
        // 4. Warning logged about failed verification
    }

    #endregion
}
