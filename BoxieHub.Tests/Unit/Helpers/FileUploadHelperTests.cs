using BoxieHub.Data;
using BoxieHub.Helpers;
using BoxieHub.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BoxieHub.Tests.Unit.Helpers;

/// <summary>
/// Unit tests for file upload helper functionality
/// Ensures file uploads are properly saved to database without hitting external APIs
/// </summary>
public class FileUploadHelperTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;

    public FileUploadHelperTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetFileUploadAsync_WithAudioFile_CreatesFileUploadWithCorrectMetadata()
    {
        // Arrange
        var audioContent = new byte[] { 0x49, 0x44, 0x33, 0x04 }; // MP3 header
        var stream = new MemoryStream(audioContent);
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("test-audio.mp3");
        mockFile.Setup(f => f.ContentType).Returns("audio/mpeg");
        mockFile.Setup(f => f.Size).Returns(audioContent.Length);
        mockFile.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Returns(stream);

        // Act
        var fileUpload = await ImageHelper.GetFileUploadAsync(mockFile.Object, "Audio", maxSizeBytes: 1024 * 1024);

        // Assert
        fileUpload.Should().NotBeNull();
        fileUpload.Id.Should().NotBe(Guid.Empty);
        fileUpload.FileName.Should().Be("test-audio.mp3");
        fileUpload.ContentType.Should().Be("audio/mpeg");
        fileUpload.FileCategory.Should().Be("Audio");
        fileUpload.FileSizeBytes.Should().Be(audioContent.Length);
        fileUpload.Data.Should().Equal(audioContent);
        fileUpload.Created.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetFileUploadAsync_WithLargeFile_ThrowsException()
    {
        // Arrange
        var largeContent = new byte[300 * 1024 * 1024]; // 300MB
        var stream = new MemoryStream(largeContent);
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("large-file.mp3");
        mockFile.Setup(f => f.ContentType).Returns("audio/mpeg");
        mockFile.Setup(f => f.Size).Returns(largeContent.Length);
        mockFile.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Returns(stream);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await ImageHelper.GetFileUploadAsync(mockFile.Object, "Audio", maxSizeBytes: 200 * 1024 * 1024));
    }

    [Fact]
    public async Task FileUpload_SavedToDatabase_CanBeRetrieved()
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            Data = new byte[] { 0x01, 0x02, 0x03 },
            ContentType = "audio/mpeg",
            FileName = "test.mp3",
            FileCategory = "Audio",
            FileSizeBytes = 3,
            Created = DateTimeOffset.UtcNow
        };

        // Act
        _dbContext.FileUploads.Add(fileUpload);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.FileUploads.FindAsync(fileUpload.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.FileName.Should().Be("test.mp3");
        retrieved.ContentType.Should().Be("audio/mpeg");
        retrieved.FileCategory.Should().Be("Audio");
        retrieved.Data.Should().Equal(new byte[] { 0x01, 0x02, 0x03 });
    }

    [Fact]
    public void ImageUpload_IsBackwardCompatibleWithFileUpload()
    {
        // Arrange & Act
        var imageUpload = new ImageUpload
        {
            Id = Guid.NewGuid(),
            Data = new byte[] { 0xFF, 0xD8, 0xFF }, // JPEG header
            ContentType = "image/jpeg",
            FileName = "test.jpg",
            FileSizeBytes = 3
        };

        // Assert
        imageUpload.FileCategory.Should().Be("Image");
        imageUpload.Should().BeAssignableTo<FileUpload>();
    }

    [Fact]
    public async Task AudioUploadHistory_CanBeCreatedAndLinkedToFileUpload()
    {
        // Arrange
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            Data = new byte[] { 0x01, 0x02, 0x03 },
            ContentType = "audio/mpeg",
            FileName = "test.mp3",
            FileCategory = "Audio",
            FileSizeBytes = 3
        };

        _dbContext.FileUploads.Add(fileUpload);
        await _dbContext.SaveChangesAsync();

        var uploadHistory = new AudioUploadHistory
        {
            UserId = "test-user",
            FileUploadId = fileUpload.Id,
            HouseholdId = "household-123",
            TonieId = "tonie-456",
            ChapterTitle = "Test Chapter",
            Status = UploadStatus.Pending,
            Created = DateTimeOffset.UtcNow
        };

        // Act
        _dbContext.AudioUploadHistories.Add(uploadHistory);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.AudioUploadHistories
            .Include(h => h.FileUpload)
            .FirstOrDefaultAsync(h => h.FileUploadId == fileUpload.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.ChapterTitle.Should().Be("Test Chapter");
        retrieved.Status.Should().Be(UploadStatus.Pending);
        retrieved.FileUpload.Should().NotBeNull();
        retrieved.FileUpload!.FileName.Should().Be("test.mp3");
    }

    [Fact]
    public void AudioUploadHistory_CanRetry_WhenFailedAndUnderLimit()
    {
        // Arrange
        var uploadHistory = new AudioUploadHistory
        {
            Status = UploadStatus.Failed,
            RetryCount = 2
        };

        // Assert
        uploadHistory.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void AudioUploadHistory_CannotRetry_WhenRetryLimitReached()
    {
        // Arrange
        var uploadHistory = new AudioUploadHistory
        {
            Status = UploadStatus.Failed,
            RetryCount = 3
        };

        // Assert
        uploadHistory.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void AudioUploadHistory_CannotRetry_WhenSuccessful()
    {
        // Arrange
        var uploadHistory = new AudioUploadHistory
        {
            Status = UploadStatus.Success,
            RetryCount = 0
        };

        // Assert
        uploadHistory.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void AudioUploadHistory_IsInProgress_WhenPending()
    {
        // Arrange
        var uploadHistory = new AudioUploadHistory
        {
            Status = UploadStatus.Pending
        };

        // Assert
        uploadHistory.IsInProgress.Should().BeTrue();
    }

    [Fact]
    public void AudioUploadHistory_IsInProgress_WhenInProgress()
    {
        // Arrange
        var uploadHistory = new AudioUploadHistory
        {
            Status = UploadStatus.InProgress
        };

        // Assert
        uploadHistory.IsInProgress.Should().BeTrue();
    }

    [Fact]
    public void AudioUploadHistory_IsNotInProgress_WhenCompleted()
    {
        // Arrange
        var uploadHistory = new AudioUploadHistory
        {
            Status = UploadStatus.Success
        };

        // Assert
        uploadHistory.IsInProgress.Should().BeFalse();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
