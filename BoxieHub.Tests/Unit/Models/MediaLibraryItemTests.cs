using BoxieHub.Models;
using NUnit.Framework;

namespace BoxieHub.Tests.Unit.Models;

/// <summary>
/// Unit tests for MediaLibraryItem duration formatting
/// Tests Bug Fix #4: Duration detection and display
/// </summary>
[TestFixture]
public class MediaLibraryItemTests
{
    [Test]
    public void FormattedDuration_WithZeroSeconds_ReturnsZero()
    {
        // Arrange - Old behavior before duration fix
        var item = new MediaLibraryItem
        {
            UserId = "test",
            Title = "Test",
            FileUploadId = Guid.NewGuid(),
            DurationSeconds = 0f, // Before fix
            FileSizeBytes = 1000,
            ContentType = "audio/mpeg"
        };

        // Act
        var formatted = item.FormattedDuration;

        // Assert
        Assert.That(formatted, Is.EqualTo("0.0s"));
    }

    [Test]
    public void FormattedDuration_WithSecondsOnly_ShowsSeconds()
    {
        // Arrange
        var item = new MediaLibraryItem
        {
            UserId = "test",
            Title = "Test",
            FileUploadId = Guid.NewGuid(),
            DurationSeconds = 45.7f,
            FileSizeBytes = 1000,
            ContentType = "audio/mpeg"
        };

        // Act
        var formatted = item.FormattedDuration;

        // Assert
        Assert.That(formatted, Is.EqualTo("45.7s"));
    }

    [Test]
    public void FormattedDuration_WithMinutesAndSeconds_ShowsMinutesAndSeconds()
    {
        // Arrange - Most common case
        var item = new MediaLibraryItem
        {
            UserId = "test",
            Title = "Test",
            FileUploadId = Guid.NewGuid(),
            DurationSeconds = 185.7f, // 3 minutes 5.7 seconds
            FileSizeBytes = 1000,
            ContentType = "audio/mpeg"
        };

        // Act
        var formatted = item.FormattedDuration;

        // Assert
        Assert.That(formatted, Is.EqualTo("3m 5s"));
    }

    [Test]
    public void FormattedDuration_WithHours_ShowsHoursAndMinutes()
    {
        // Arrange
        var item = new MediaLibraryItem
        {
            UserId = "test",
            Title = "Test",
            FileUploadId = Guid.NewGuid(),
            DurationSeconds = 3665f, // 1 hour 1 minute 5 seconds
            FileSizeBytes = 1000,
            ContentType = "audio/mpeg"
        };

        // Act
        var formatted = item.FormattedDuration;

        // Assert
        Assert.That(formatted, Is.EqualTo("1h 1m"));
    }

    [Test]
    public void FormattedDuration_ExactlyOneMinute_ShowsMinutes()
    {
        // Arrange
        var item = new MediaLibraryItem
        {
            UserId = "test",
            Title = "Test",
            FileUploadId = Guid.NewGuid(),
            DurationSeconds = 60f,
            FileSizeBytes = 1000,
            ContentType = "audio/mpeg"
        };

        // Act
        var formatted = item.FormattedDuration;

        // Assert
        Assert.That(formatted, Is.EqualTo("1m 0s"));
    }

    [Test]
    public void FormattedFileSize_WithBytes_ShowsBytes()
    {
        // Arrange
        var item = new MediaLibraryItem
        {
            UserId = "test",
            Title = "Test",
            FileUploadId = Guid.NewGuid(),
            DurationSeconds = 60f,
            FileSizeBytes = 512,
            ContentType = "audio/mpeg"
        };

        // Act
        var formatted = item.FormattedFileSize;

        // Assert
        Assert.That(formatted, Is.EqualTo("512 B"));
    }

    [Test]
    public void FormattedFileSize_WithKilobytes_ShowsKB()
    {
        // Arrange
        var item = new MediaLibraryItem
        {
            UserId = "test",
            Title = "Test",
            FileUploadId = Guid.NewGuid(),
            DurationSeconds = 60f,
            FileSizeBytes = 1536, // 1.5 KB
            ContentType = "audio/mpeg"
        };

        // Act
        var formatted = item.FormattedFileSize;

        // Assert
        Assert.That(formatted, Is.EqualTo("1.5 KB"));
    }

    [Test]
    public void FormattedFileSize_WithMegabytes_ShowsMB()
    {
        // Arrange
        var item = new MediaLibraryItem
        {
            UserId = "test",
            Title = "Test",
            FileUploadId = Guid.NewGuid(),
            DurationSeconds = 60f,
            FileSizeBytes = 3 * 1024 * 1024, // 3 MB
            ContentType = "audio/mpeg"
        };

        // Act
        var formatted = item.FormattedFileSize;

        // Assert
        Assert.That(formatted, Is.EqualTo("3 MB"));
    }

    [Test]
    [TestCase(0f, "0.0s")]
    [TestCase(30.5f, "30.5s")]
    [TestCase(60f, "1m 0s")]
    [TestCase(90f, "1m 30s")]
    [TestCase(185.7f, "3m 5s")]
    [TestCase(3600f, "1h 0m")]
    [TestCase(3665f, "1h 1m")]
    [TestCase(7325f, "2h 2m")]
    public void FormattedDuration_VariousDurations_FormatsCorrectly(float seconds, string expected)
    {
        // Arrange
        var item = new MediaLibraryItem
        {
            UserId = "test",
            Title = "Test",
            FileUploadId = Guid.NewGuid(),
            DurationSeconds = seconds,
            FileSizeBytes = 1000,
            ContentType = "audio/mpeg"
        };

        // Act & Assert
        Assert.That(item.FormattedDuration, Is.EqualTo(expected));
    }
}
