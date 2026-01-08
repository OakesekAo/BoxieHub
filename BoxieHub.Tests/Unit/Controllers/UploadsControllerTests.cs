using BoxieHub.Controllers;
using BoxieHub.Models;
using BoxieHub.Services.Storage;
using BoxieHub.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BoxieHub.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for UploadsController - Bug Fix #1: S3 Support
/// Tests that images can be served from both S3 and database
/// </summary>
[TestFixture]
public class UploadsControllerTests : TestBase
{
    private Mock<IFileStorageService> _mockFileStorageService = null!;
    private Mock<ILogger<UploadsController>> _mockLogger = null!;
    private UploadsController _controller = null!;

    [SetUp]
    public new void SetUp()
    {
        base.SetUpDatabase();
        _mockFileStorageService = new Mock<IFileStorageService>();
        _mockLogger = new Mock<ILogger<UploadsController>>();
        _controller = new UploadsController(DbContext, _mockFileStorageService.Object, _mockLogger.Object);
    }

    [Test]
    public async Task GetImage_WithDatabaseData_ReturnsFile()
    {
        // Arrange - Create image stored in database (legacy)
        var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            Data = imageData,
            ContentType = "image/png",
            FileName = "test.png",
            Provider = StorageProvider.Database,
            StoragePath = null
        };
        DbContext.FileUploads.Add(fileUpload);
        DbContext.SaveChanges();

        // Act
        var result = await _controller.GetImage(fileUpload.Id);

        // Assert
        Assert.That(result, Is.InstanceOf<FileContentResult>());
        var fileResult = (FileContentResult)result;
        Assert.That(fileResult.ContentType, Is.EqualTo("image/png"));
        Assert.That(fileResult.FileContents, Is.EqualTo(imageData));
        
        // Verify no S3 call was made
        _mockFileStorageService.Verify(
            x => x.DownloadFileAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task GetImage_WithS3Storage_DownloadsFromS3()
    {
        // Arrange - Create image stored in S3
        var storagePath = "users/test-user/abc123/test.png";
        var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            Data = null, // No database data
            ContentType = "image/png",
            FileName = "test.png",
            Provider = StorageProvider.S3Railway,
            StoragePath = storagePath,
            UserStorageAccountId = null
        };
        DbContext.FileUploads.Add(fileUpload);
        DbContext.SaveChanges();

        // Mock S3 download
        var s3Stream = new MemoryStream(imageData);
        _mockFileStorageService
            .Setup(x => x.DownloadFileAsync(storagePath, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(s3Stream);

        // Act
        var result = await _controller.GetImage(fileUpload.Id);

        // Assert
        Assert.That(result, Is.InstanceOf<FileStreamResult>());
        var fileResult = (FileStreamResult)result;
        Assert.That(fileResult.ContentType, Is.EqualTo("image/png"));
        Assert.That(fileResult.EnableRangeProcessing, Is.True);
        
        // Verify S3 was called
        _mockFileStorageService.Verify(
            x => x.DownloadFileAsync(storagePath, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task GetImage_NotFound_Returns404()
    {
        // Arrange - Non-existent ID
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _controller.GetImage(nonExistentId);

        // Assert
        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task GetImage_WithoutDataOrStoragePath_Returns404()
    {
        // Arrange - File with neither database data nor S3 path (corrupted state)
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            Data = null,
            ContentType = "image/png",
            FileName = "test.png",
            Provider = StorageProvider.Database,
            StoragePath = null
        };
        DbContext.FileUploads.Add(fileUpload);
        DbContext.SaveChanges();

        // Act
        var result = await _controller.GetImage(fileUpload.Id);

        // Assert
        Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task GetImage_S3DownloadFails_Returns500()
    {
        // Arrange
        var storagePath = "users/test-user/abc123/test.png";
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            Data = null,
            ContentType = "image/png",
            FileName = "test.png",
            Provider = StorageProvider.S3Railway,
            StoragePath = storagePath,
            UserStorageAccountId = null
        };
        DbContext.FileUploads.Add(fileUpload);
        DbContext.SaveChanges();

        // Mock S3 failure
        _mockFileStorageService
            .Setup(x => x.DownloadFileAsync(storagePath, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("S3 connection failed"));

        // Act
        var result = await _controller.GetImage(fileUpload.Id);

        // Assert
        Assert.That(result, Is.InstanceOf<ObjectResult>());
        var objectResult = (ObjectResult)result;
        Assert.That(objectResult.StatusCode, Is.EqualTo(500));
    }

    [Test]
    public async Task GetImage_WithDropboxStorage_DownloadsFromDropbox()
    {
        // Arrange
        var storagePath = "/BoxieHub/test-user/test.png";
        var storageAccountId = 123;
        var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            Data = null,
            ContentType = "image/png",
            FileName = "test.png",
            Provider = StorageProvider.Dropbox,
            StoragePath = storagePath,
            UserStorageAccountId = storageAccountId
        };
        DbContext.FileUploads.Add(fileUpload);
        DbContext.SaveChanges();

        // Mock Dropbox download
        var dropboxStream = new MemoryStream(imageData);
        _mockFileStorageService
            .Setup(x => x.DownloadFileAsync(storagePath, storageAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dropboxStream);

        // Act
        var result = await _controller.GetImage(fileUpload.Id);

        // Assert
        Assert.That(result, Is.InstanceOf<FileStreamResult>());
        
        // Verify Dropbox was called with correct storage account ID
        _mockFileStorageService.Verify(
            x => x.DownloadFileAsync(storagePath, storageAccountId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
