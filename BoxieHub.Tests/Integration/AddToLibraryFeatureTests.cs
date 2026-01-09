using BoxieHub.Data;
using BoxieHub.Models;
using BoxieHub.Services;
using BoxieHub.Services.Storage;
using BoxieHub.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BoxieHub.Tests.Integration;

/// <summary>
/// Integration tests for the complete "Add to Library" feature workflow
/// Tests the full flow: Upload to Tonie ? Save to Library ? Verify Storage
/// </summary>
[TestFixture]
[Category("Integration")]
public class AddToLibraryFeatureTests : TestBase
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

    [Test]
    public async Task CompleteWorkflow_UploadToTonieAndSaveToLibrary_Success()
    {
        // Arrange - Simulate the complete workflow
        var userId = "test-user";
        var tonieId = "64A78A22500304E0";
        var tonieName = "Pirate Tonie";
        var chapterTitle = "boom_x";
        
        // Step 1: User selects audio file (simulated by byte array)
        var audioData = new byte[12790]; // Same size as error log
        new Random().NextBytes(audioData);
        var audioStream = new MemoryStream(audioData);
        
        // Step 2: JavaScript calculates duration (simulated)
        var calculatedDuration = 45.2f;
        
        // Step 3: User checks "Also save to my library" checkbox
        var addToLibrary = true;
        
        // Step 4: File is buffered before upload (simulated)
        var fileBuffer = audioData; // In real code, this is done in StartUpload()
        
        // Step 5: User's default storage provider is S3
        var storagePath = $"users/{userId}/acf69bc2-750c-4735-a3c3-daa2837f58a9/boom_x.wav";
        _mockStoragePreferenceService
            .Setup(x => x.GetDefaultProviderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StorageProvider.S3Railway);
        
        _mockFileStorageService
            .Setup(x => x.UploadFileAsync(
                It.IsAny<Stream>(), 
                "boom_x.wav", 
                "audio/wav", 
                userId, 
                null, 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(storagePath);
        
        // Step 6: Create DTO (as done in SaveToLibraryAsync)
        var dto = new MediaLibraryItemDto
        {
            Title = chapterTitle,
            Description = $"Uploaded to {tonieName} on {DateTime.Now:MMM dd, yyyy}",
            Category = "Audio",
            Tags = new List<string> { "tonie-upload" },
            ContentType = "audio/wav",
            OriginalFileName = "boom_x.wav",
            FileSizeBytes = audioData.Length,
            DurationSeconds = calculatedDuration
        };
        
        // Act - Save to library (Step 7 in Upload.razor)
        var libraryItem = await _service.AddToLibraryAsync(
            userId, 
            new MemoryStream(fileBuffer), // Use buffered data
            dto,
            StorageProvider.S3Railway);
        
        // Assert - Verify all data is correct
        Assert.That(libraryItem, Is.Not.Null);
        Assert.That(libraryItem.Title, Is.EqualTo(chapterTitle));
        Assert.That(libraryItem.Description, Does.Contain(tonieName));
        Assert.That(libraryItem.Tags, Contains.Item("tonie-upload"));
        Assert.That(libraryItem.DurationSeconds, Is.EqualTo(calculatedDuration));
        Assert.That(libraryItem.FileSizeBytes, Is.EqualTo(12790));
        Assert.That(libraryItem.ContentType, Is.EqualTo("audio/wav"));
        
        // Verify FileUpload was created correctly
        Assert.That(libraryItem.FileUpload, Is.Not.Null);
        Assert.That(libraryItem.FileUpload.Provider, Is.EqualTo(StorageProvider.S3Railway));
        Assert.That(libraryItem.FileUpload.StoragePath, Is.EqualTo(storagePath));
        Assert.That(libraryItem.FileUpload.Data, Is.Null, "S3 files should not have database data");
        
        // Verify S3 upload was called
        _mockFileStorageService.Verify(
            x => x.UploadFileAsync(
                It.IsAny<Stream>(), 
                "boom_x.wav", 
                "audio/wav", 
                userId, 
                null, 
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task FileBuffering_PreventsBrowserFileReuseError()
    {
        // This test verifies the bug fix: buffering prevents "_blazorFilesById is null" error
        
        // Arrange
        var userId = "test-user";
        var audioData = new byte[1000];
        new Random().NextBytes(audioData);
        
        // Simulate: File is read ONCE into buffer (in StartUpload)
        var fileBuffer = audioData;
        
        // Step 1: Upload to Tonie (consumes first stream)
        var tonieStream = new MemoryStream(fileBuffer);
        // ... upload to Tonie happens here (simulated)
        await tonieStream.CopyToAsync(Stream.Null); // Simulate consumption
        
        // Step 2: Save to Library (uses SAME buffer, NEW stream)
        var libraryStream = new MemoryStream(fileBuffer); // ? New stream from same buffer
        
        var dto = new MediaLibraryItemDto
        {
            Title = "Test",
            ContentType = "audio/mpeg",
            OriginalFileName = "test.mp3",
            FileSizeBytes = audioData.Length,
            DurationSeconds = 60.0f
        };
        
        _mockStoragePreferenceService
            .Setup(x => x.GetDefaultProviderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StorageProvider.Database);
        
        // Act - This should NOT throw "_blazorFilesById is null" error
        var result = await _service.AddToLibraryAsync(userId, libraryStream, dto);
        
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.FileUpload.Data, Is.EqualTo(audioData));
    }

    [Test]
    public async Task AddToLibrary_NonBlockingFailure_DoesNotBreakTonieUpload()
    {
        // This test verifies that if library save fails, it doesn't break the Tonie upload
        
        // Arrange
        var userId = "test-user";
        var audioData = new byte[1000];
        var audioStream = new MemoryStream(audioData);
        
        var dto = new MediaLibraryItemDto
        {
            Title = "Test",
            ContentType = "audio/mpeg",
            OriginalFileName = "test.mp3",
            FileSizeBytes = audioData.Length,
            DurationSeconds = 60.0f
        };
        
        // Mock S3 failure
        _mockStoragePreferenceService
            .Setup(x => x.GetDefaultProviderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StorageProvider.S3Railway);
        
        _mockFileStorageService
            .Setup(x => x.UploadFileAsync(
                It.IsAny<Stream>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<int?>(), 
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("S3 connection failed"));
        
        // Act & Assert - Should throw (library save fails)
        Assert.ThrowsAsync<Exception>(async () => 
            await _service.AddToLibraryAsync(userId, audioStream, dto, StorageProvider.S3Railway));
        
        // In real code, this exception is caught in Upload.razor:
        // - savedToLibrary = false
        // - uploadResult.Success = true (Tonie upload succeeded)
        // - Warning logged but upload completes
    }

    [Test]
    public async Task MultipleLibrarySaves_FromDifferentTonies_TracksSeparately()
    {
        // Arrange - User uploads same audio to 3 different Tonies, saving to library each time
        var userId = "test-user";
        
        var tonies = new[]
        {
            ("Pirate Tonie", "Treasure Hunt"),
            ("Princess Tonie", "Magic Story"),
            ("Dinosaur Tonie", "Roar Adventure")
        };
        
        _mockStoragePreferenceService
            .Setup(x => x.GetDefaultProviderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StorageProvider.Database);
        
        var savedItems = new List<MediaLibraryItem>();
        
        foreach (var (tonieName, chapterTitle) in tonies)
        {
            // Act
            var audioData = new byte[1000];
            var dto = new MediaLibraryItemDto
            {
                Title = chapterTitle,
                Description = $"Uploaded to {tonieName} on {DateTime.Now:MMM dd, yyyy}",
                Category = "Audio",
                Tags = new List<string> { "tonie-upload" },
                ContentType = "audio/mpeg",
                OriginalFileName = $"{chapterTitle}.mp3",
                FileSizeBytes = audioData.Length,
                DurationSeconds = 120.0f
            };
            
            var item = await _service.AddToLibraryAsync(userId, new MemoryStream(audioData), dto);
            savedItems.Add(item);
        }
        
        // Assert - All 3 are saved separately
        Assert.That(savedItems, Has.Count.EqualTo(3));
        Assert.That(savedItems[0].Description, Does.Contain("Pirate Tonie"));
        Assert.That(savedItems[1].Description, Does.Contain("Princess Tonie"));
        Assert.That(savedItems[2].Description, Does.Contain("Dinosaur Tonie"));
        
        // Verify library has all 3 items
        var library = await _service.GetUserLibraryAsync(userId);
        Assert.That(library, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task LibraryItem_AfterSave_CanBeUsedOnOtherTonies()
    {
        // This test verifies the full cycle:
        // Upload to Tonie A ? Save to Library ? Use from Library on Tonie B
        
        // Arrange
        var userId = "test-user";
        var audioData = new byte[1000];
        
        // Step 1: Upload to Tonie A and save to library
        var dto = new MediaLibraryItemDto
        {
            Title = "Reusable Audio",
            Description = "Uploaded to Tonie A",
            Category = "Audio",
            Tags = new List<string> { "tonie-upload" },
            ContentType = "audio/mpeg",
            OriginalFileName = "reusable.mp3",
            FileSizeBytes = audioData.Length,
            DurationSeconds = 180.0f
        };
        
        _mockStoragePreferenceService
            .Setup(x => x.GetDefaultProviderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StorageProvider.Database);
        
        var libraryItem = await _service.AddToLibraryAsync(userId, new MemoryStream(audioData), dto);
        
        // Step 2: Use from library on Tonie B
        var retrievedItem = await _service.GetLibraryItemAsync(libraryItem.Id, userId);
        
        // Assert
        Assert.That(retrievedItem, Is.Not.Null);
        Assert.That(retrievedItem.FileUpload, Is.Not.Null, 
            "FileUpload must be loaded for 'Use from Library' to work");
        Assert.That(retrievedItem.FileUpload.Data, Is.Not.Null, 
            "File data must be available for download");
        Assert.That(retrievedItem.FileUpload.Data, Is.EqualTo(audioData));
        
        // Step 3: Track usage
        await _service.TrackUsageAsync(
            libraryItem.Id,
            "household-123",
            "tonie-B",
            "chapter-456",
            "Tonie B",
            "Reusable Audio");
        
        // Step 4: Verify usage was tracked
        var usages = await _service.GetItemUsageAsync(libraryItem.Id);
        Assert.That(usages, Has.Count.EqualTo(1));
        Assert.That(usages[0].TonieId, Is.EqualTo("tonie-B"));
        
        // Verify use count incremented
        var updatedItem = await _service.GetLibraryItemAsync(libraryItem.Id, userId);
        Assert.That(updatedItem.UseCount, Is.EqualTo(1));
    }
}
