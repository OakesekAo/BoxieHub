using BoxieHub.Data;
using BoxieHub.Models;
using BoxieHub.Services;
using BoxieHub.Services.Storage;
using BoxieHub.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BoxieHub.Tests.Unit.Services;

/// <summary>
/// Unit tests for MediaLibraryService
/// Tests Bug Fix #2: FileUpload navigation property loading
/// Tests Bug Fix #4: Duration detection storage
/// Tests Feature #5: Add to Library
/// </summary>
[TestFixture]
public class MediaLibraryServiceTests : TestBase
{
    private Mock<IFileStorageService> _mockFileStorageService = null!;
    private Mock<IStoragePreferenceService> _mockStoragePreferenceService = null!;
    private Mock<ILogger<MediaLibraryService>> _mockLogger = null!;
    private IMediaLibraryService _service = null!;

    [SetUp]
    public new void SetUp()
    {
        base.SetUpDatabase();
        _mockFileStorageService = new Mock<IFileStorageService>();
        _mockStoragePreferenceService = new Mock<IStoragePreferenceService>();
        _mockLogger = new Mock<ILogger<MediaLibraryService>>();
        
        var dbContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        dbContextFactory
            .Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DbContext);
        
        _service = new MediaLibraryService(
            dbContextFactory.Object,
            _mockFileStorageService.Object,
            _mockStoragePreferenceService.Object,
            _mockLogger.Object);
    }

    #region Bug Fix #3: GetUserLibraryAsync includes FileUpload

    [Test]
    public async Task GetUserLibraryAsync_IncludesFileUploadNavigationProperty()
    {
        // Arrange - Create library item with file upload
        var userId = "test-user";
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            Data = new byte[] { 1, 2, 3, 4 },
            ContentType = "audio/mpeg",
            FileName = "test.mp3",
            Provider = StorageProvider.Database
        };
        DbContext.FileUploads.Add(fileUpload);
        
        var libraryItem = new MediaLibraryItem
        {
            UserId = userId,
            Title = "Test Audio",
            FileUploadId = fileUpload.Id,
            DurationSeconds = 120.5f,
            FileSizeBytes = 4,
            ContentType = "audio/mpeg"
        };
        DbContext.MediaLibraryItems.Add(libraryItem);
        DbContext.SaveChanges();

        // Act
        var results = await _service.GetUserLibraryAsync(userId);

        // Assert
        Assert.That(results, Has.Count.EqualTo(1));
        var item = results[0];
        
        // ? This should NOT be null (Bug Fix #3)
        Assert.That(item.FileUpload, Is.Not.Null, 
            "FileUpload navigation property should be loaded");
        Assert.That(item.FileUpload.Id, Is.EqualTo(fileUpload.Id));
        Assert.That(item.FileUpload.FileName, Is.EqualTo("test.mp3"));
    }

    [Test]
    public async Task GetLibraryItemAsync_IncludesFileUploadNavigationProperty()
    {
        // Arrange
        var userId = "test-user";
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            Data = new byte[] { 1, 2, 3, 4 },
            ContentType = "audio/mpeg",
            FileName = "test.mp3",
            Provider = StorageProvider.S3Railway,
            StoragePath = "users/test-user/abc/test.mp3"
        };
        DbContext.FileUploads.Add(fileUpload);
        
        var libraryItem = new MediaLibraryItem
        {
            UserId = userId,
            Title = "Test Audio",
            FileUploadId = fileUpload.Id,
            DurationSeconds = 120.5f,
            FileSizeBytes = 4,
            ContentType = "audio/mpeg"
        };
        DbContext.MediaLibraryItems.Add(libraryItem);
        DbContext.SaveChanges();

        // Act
        var result = await _service.GetLibraryItemAsync(libraryItem.Id, userId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.FileUpload, Is.Not.Null);
        Assert.That(result.FileUpload.StoragePath, Is.EqualTo("users/test-user/abc/test.mp3"));
    }

    #endregion

    #region Bug Fix #4: Duration Detection

    [Test]
    public async Task AddToLibraryAsync_SavesActualDuration()
    {
        // Arrange
        var userId = "test-user";
        var audioData = new byte[] { 0xFF, 0xFB, 0x90, 0x44 }; // MP3 header
        var audioStream = new MemoryStream(audioData);
        
        var dto = new MediaLibraryItemDto
        {
            Title = "Test Song",
            Description = "A test song",
            Category = "Music",
            Tags = new List<string> { "test" },
            ContentType = "audio/mpeg",
            OriginalFileName = "test.mp3",
            FileSizeBytes = audioData.Length,
            DurationSeconds = 185.7f // ? Actual duration (not 0)
        };

        _mockStoragePreferenceService
            .Setup(x => x.GetDefaultProviderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StorageProvider.Database);

        // Act
        var result = await _service.AddToLibraryAsync(userId, audioStream, dto);

        // Assert
        Assert.That(result.DurationSeconds, Is.EqualTo(185.7f), 
            "Duration should be saved correctly");
        Assert.That(result.FormattedDuration, Is.EqualTo("3m 5s"), 
            "Formatted duration should be correct");
    }

    [Test]
    public async Task AddToLibraryAsync_WithZeroDuration_StillSaves()
    {
        // Arrange - Old behavior (before fix) - duration = 0
        var userId = "test-user";
        var audioData = new byte[] { 0xFF, 0xFB, 0x90, 0x44 };
        var audioStream = new MemoryStream(audioData);
        
        var dto = new MediaLibraryItemDto
        {
            Title = "Test Song",
            ContentType = "audio/mpeg",
            OriginalFileName = "test.mp3",
            FileSizeBytes = audioData.Length,
            DurationSeconds = 0f // Old uploads before fix
        };

        _mockStoragePreferenceService
            .Setup(x => x.GetDefaultProviderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StorageProvider.Database);

        // Act
        var result = await _service.AddToLibraryAsync(userId, audioStream, dto);

        // Assert
        Assert.That(result.DurationSeconds, Is.EqualTo(0f));
        Assert.That(result.FormattedDuration, Is.EqualTo("0.0s"));
    }

    #endregion

    #region Feature #5: Add to Library from Tonie Upload

    [Test]
    public async Task AddToLibraryAsync_WithS3Provider_SavesCorrectly()
    {
        // Arrange - Simulates "Add to Library" feature
        var userId = "test-user";
        var audioData = new byte[] { 0xFF, 0xFB, 0x90, 0x44 };
        var audioStream = new MemoryStream(audioData);
        
        var dto = new MediaLibraryItemDto
        {
            Title = "Uploaded to Pirate Tonie",
            Description = "Uploaded to Pirate Tonie on Jan 09, 2026",
            Category = "Audio",
            Tags = new List<string> { "tonie-upload" }, // Auto-tagged
            ContentType = "audio/mpeg",
            OriginalFileName = "boom_x.wav",
            FileSizeBytes = audioData.Length,
            DurationSeconds = 45.2f
        };

        var storagePath = "users/test-user/guid/boom_x.wav";
        
        _mockStoragePreferenceService
            .Setup(x => x.GetDefaultProviderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StorageProvider.S3Railway);
        
        _mockFileStorageService
            .Setup(x => x.UploadFileAsync(
                It.IsAny<Stream>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                userId, 
                null, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(storagePath);

        // Act
        var result = await _service.AddToLibraryAsync(userId, audioStream, dto, StorageProvider.S3Railway);

        // Assert
        Assert.That(result.Title, Is.EqualTo("Uploaded to Pirate Tonie"));
        Assert.That(result.Tags, Contains.Item("tonie-upload"));
        Assert.That(result.DurationSeconds, Is.EqualTo(45.2f));
        Assert.That(result.FileUpload, Is.Not.Null);
        Assert.That(result.FileUpload.Provider, Is.EqualTo(StorageProvider.S3Railway));
        Assert.That(result.FileUpload.StoragePath, Is.EqualTo(storagePath));
        Assert.That(result.FileUpload.Data, Is.Null, "S3 files should not have database data");
    }

    [Test]
    public async Task AddToLibraryAsync_WithDatabaseProvider_SavesCorrectly()
    {
        // Arrange - User prefers database storage
        var userId = "test-user";
        var audioData = new byte[] { 0xFF, 0xFB, 0x90, 0x44 };
        var audioStream = new MemoryStream(audioData);
        
        var dto = new MediaLibraryItemDto
        {
            Title = "Test Audio",
            ContentType = "audio/mpeg",
            OriginalFileName = "test.mp3",
            FileSizeBytes = audioData.Length,
            DurationSeconds = 60.0f
        };

        _mockStoragePreferenceService
            .Setup(x => x.GetDefaultProviderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StorageProvider.Database);

        // Act
        var result = await _service.AddToLibraryAsync(userId, audioStream, dto);

        // Assert
        Assert.That(result.FileUpload, Is.Not.Null);
        Assert.That(result.FileUpload.Provider, Is.EqualTo(StorageProvider.Database));
        Assert.That(result.FileUpload.Data, Is.Not.Null, "Database files should have data");
        Assert.That(result.FileUpload.Data, Is.EqualTo(audioData));
        Assert.That(result.FileUpload.StoragePath, Is.Null, "Database files should not have storage path");
    }

    #endregion

    #region Delete Tests

    [Test]
    public async Task DeleteLibraryItemAsync_WithS3File_DeletesFromS3()
    {
        // Arrange
        var userId = "test-user";
        var storagePath = "users/test-user/abc/test.mp3";
        
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            Provider = StorageProvider.S3Railway,
            StoragePath = storagePath,
            ContentType = "audio/mpeg",
            FileName = "test.mp3"
        };
        DbContext.FileUploads.Add(fileUpload);
        
        var libraryItem = new MediaLibraryItem
        {
            UserId = userId,
            Title = "Test Audio",
            FileUploadId = fileUpload.Id,
            DurationSeconds = 120.0f,
            FileSizeBytes = 1000,
            ContentType = "audio/mpeg"
        };
        DbContext.MediaLibraryItems.Add(libraryItem);
        DbContext.SaveChanges();
        
        var libraryItemId = libraryItem.Id;

        // Act
        var result = await _service.DeleteLibraryItemAsync(libraryItemId, userId);

        // Assert
        Assert.That(result, Is.True);
        
        // Verify S3 delete was called
        _mockFileStorageService.Verify(
            x => x.DeleteFileAsync(storagePath, null, It.IsAny<CancellationToken>()),
            Times.Once);
        
        // Note: We can't verify database deletion here because the service
        // creates its own DbContext via factory and disposes it.
        // The deletion is verified implicitly by the service returning true.
    }

    #endregion

    #region Search and Filter Tests

    [Test]
    public async Task SearchLibraryAsync_WithTagFilter_ReturnsOnlyMatchingItems()
    {
        // Arrange
        var userId = "test-user";
        
        // Item 1: Has "tonie-upload" tag
        var item1 = CreateLibraryItem(userId, "Audio 1", new List<string> { "tonie-upload" });
        
        // Item 2: Has different tag
        var item2 = CreateLibraryItem(userId, "Audio 2", new List<string> { "manual-upload" });
        
        DbContext.MediaLibraryItems.AddRange(item1, item2);
        DbContext.SaveChanges();

        var search = new LibrarySearchDto
        {
            Tag = "tonie-upload",
            Skip = 0,
            Take = 10
        };

        // Act
        var results = await _service.SearchLibraryAsync(userId, search);

        // Assert
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Title, Is.EqualTo("Audio 1"));
    }

    private MediaLibraryItem CreateLibraryItem(string userId, string title, List<string> tags)
    {
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            Data = new byte[] { 1, 2, 3 },
            ContentType = "audio/mpeg",
            Provider = StorageProvider.Database
        };
        DbContext.FileUploads.Add(fileUpload);
        
        return new MediaLibraryItem
        {
            UserId = userId,
            Title = title,
            FileUploadId = fileUpload.Id,
            Tags = tags,
            DurationSeconds = 60.0f,
            FileSizeBytes = 3,
            ContentType = "audio/mpeg"
        };
    }

    #endregion
}
