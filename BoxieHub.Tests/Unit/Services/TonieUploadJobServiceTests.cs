using BoxieHub.Data;
using BoxieHub.Models;
using BoxieHub.Services.Tonie;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BoxieHub.Tests.Unit.Services;

[Trait("Category", "Unit")]
public class TonieUploadJobServiceTests : IDisposable
{
    private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;
    private readonly Mock<IDbContextFactory<ApplicationDbContext>> _mockDbContextFactory;
    private readonly Mock<ILogger<TonieUploadJobService>> _mockLogger;
    private readonly TonieUploadJobService _service;
    private readonly string _testUserId = "test-user-123";
    private readonly string _testHouseholdId = "household-abc";
    private readonly string _testTonieId = "tonie-xyz";

    public TonieUploadJobServiceTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TonieUploadJobServiceTests_{Guid.NewGuid()}")
            .Options;

        _mockDbContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        _mockDbContextFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ApplicationDbContext(_dbContextOptions));

        _mockLogger = new Mock<ILogger<TonieUploadJobService>>();

        _service = new TonieUploadJobService(
            _mockDbContextFactory.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CreateUploadJobAsync_WithValidData_CreatesJob()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();
        var chapterTitle = "Test Chapter";

        // Act
        var job = await _service.CreateUploadJobAsync(
            _testUserId,
            libraryItemId,
            _testHouseholdId,
            _testTonieId,
            chapterTitle);

        // Assert
        job.Should().NotBeNull();
        job.Id.Should().BeGreaterThan(0);
        job.UserId.Should().Be(_testUserId);
        job.MediaLibraryItemId.Should().Be(libraryItemId);
        job.HouseholdId.Should().Be(_testHouseholdId);
        job.TonieId.Should().Be(_testTonieId);
        job.ChapterTitle.Should().Be(chapterTitle);
        job.StatusEnum.Should().Be(TonieUploadJobStatus.Pending);
        job.ProgressPercentage.Should().Be(0);
        job.StatusMessage.Should().Be("Upload queued...");
        job.Created.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateUploadJobAsync_WithNonExistentLibraryItem_ThrowsException()
    {
        // Arrange
        var nonExistentId = 99999;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.CreateUploadJobAsync(
                _testUserId,
                nonExistentId,
                _testHouseholdId,
                _testTonieId));

        exception.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task CreateUploadJobAsync_WithWrongUser_ThrowsException()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem("different-user");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.CreateUploadJobAsync(
                _testUserId,
                libraryItemId,
                _testHouseholdId,
                _testTonieId));

        exception.Message.Should().Contain("not found or access denied");
    }

    [Fact]
    public async Task CreateUploadJobAsync_WithoutCustomTitle_UsesLibraryItemTitle()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();

        // Act
        var job = await _service.CreateUploadJobAsync(
            _testUserId,
            libraryItemId,
            _testHouseholdId,
            _testTonieId);

        // Assert
        job.ChapterTitle.Should().Be("Test Audio");
    }

    [Fact]
    public async Task GetJobByIdAsync_WithExistingJob_ReturnsJobWithRelations()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();
        var createdJob = await _service.CreateUploadJobAsync(
            _testUserId,
            libraryItemId,
            _testHouseholdId,
            _testTonieId);

        // Act
        var job = await _service.GetJobByIdAsync(createdJob.Id);

        // Assert
        job.Should().NotBeNull();
        job!.Id.Should().Be(createdJob.Id);
        job.MediaLibraryItem.Should().NotBeNull();
        job.MediaLibraryItem!.FileUpload.Should().NotBeNull();
    }

    [Fact]
    public async Task GetJobByIdAsync_WithNonExistentJob_ReturnsNull()
    {
        // Act
        var job = await _service.GetJobByIdAsync(99999);

        // Assert
        job.Should().BeNull();
    }

    [Fact]
    public async Task GetUserJobsAsync_ReturnsUserJobsOnly()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();
        await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);
        await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);

        // Act
        var jobs = await _service.GetUserJobsAsync(_testUserId, take: 10);

        // Assert
        jobs.Should().HaveCount(2);
        jobs.Should().AllSatisfy(j => j.UserId.Should().Be(_testUserId));
        jobs.Should().BeInDescendingOrder(j => j.Created);
    }

    [Fact]
    public async Task GetUserJobsAsync_RespectsLimitParameter()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();
        for (int i = 0; i < 5; i++)
        {
            await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);
        }

        // Act
        var jobs = await _service.GetUserJobsAsync(_testUserId, take: 3);

        // Assert
        jobs.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetActiveJobsAsync_ReturnsOnlyActiveJobs()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();
        
        var job1 = await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);
        var job2 = await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);
        var job3 = await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);

        await _service.UpdateJobStatusAsync(job1.Id, TonieUploadJobStatus.Uploading);
        await _service.CompleteJobAsync(job2.Id);

        // Act
        var activeJobs = await _service.GetActiveJobsAsync(_testUserId);

        // Assert
        activeJobs.Should().HaveCount(2);
        activeJobs.Should().Contain(j => j.Id == job1.Id);
        activeJobs.Should().Contain(j => j.Id == job3.Id);
        activeJobs.Should().NotContain(j => j.Id == job2.Id);
    }

    [Fact]
    public async Task GetActiveJobsAsync_ExcludesFailedAndCancelledJobs()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();
        
        var job1 = await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);
        var job2 = await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);
        var job3 = await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);

        await _service.FailJobAsync(job1.Id, "Test error");
        await _service.CancelJobAsync(job2.Id);

        // Act
        var activeJobs = await _service.GetActiveJobsAsync(_testUserId);

        // Assert
        activeJobs.Should().HaveCount(1);
        activeJobs.Should().Contain(j => j.Id == job3.Id);
    }

    [Fact]
    public async Task UpdateJobStatusAsync_UpdatesStatusAndProgress()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();
        var job = await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);

        // Act
        await _service.UpdateJobStatusAsync(
            job.Id,
            TonieUploadJobStatus.Uploading,
            progressPercentage: 50,
            statusMessage: "Uploading to Tonie...");

        // Assert
        var updatedJob = await _service.GetJobByIdAsync(job.Id);
        updatedJob!.StatusEnum.Should().Be(TonieUploadJobStatus.Uploading);
        updatedJob.ProgressPercentage.Should().Be(50);
        updatedJob.StatusMessage.Should().Be("Uploading to Tonie...");
        updatedJob.Modified.Should().BeAfter(job.Modified);
    }

    [Fact]
    public async Task UpdateJobStatusAsync_SetsStartedTimeOnFirstUpdate()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();
        var job = await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);
        job.Started.Should().BeNull();

        // Act
        await _service.UpdateJobStatusAsync(job.Id, TonieUploadJobStatus.Preparing);

        // Assert
        var updatedJob = await _service.GetJobByIdAsync(job.Id);
        updatedJob!.Started.Should().NotBeNull();
        updatedJob.Started.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateJobStatusAsync_DoesNotOverwriteStartedTime()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();
        var job = await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);
        await _service.UpdateJobStatusAsync(job.Id, TonieUploadJobStatus.Preparing);
        var firstUpdate = await _service.GetJobByIdAsync(job.Id);
        var firstStartedTime = firstUpdate!.Started;

        await Task.Delay(100); // Small delay

        // Act
        await _service.UpdateJobStatusAsync(job.Id, TonieUploadJobStatus.Uploading);

        // Assert
        var secondUpdate = await _service.GetJobByIdAsync(job.Id);
        secondUpdate!.Started.Should().Be(firstStartedTime);
    }

    [Fact]
    public async Task CompleteJobAsync_MarksJobAsCompleted()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();
        var job = await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);
        var syncResult = "{\"success\":true}";

        // Act
        await _service.CompleteJobAsync(job.Id, syncResult);

        // Assert
        var completedJob = await _service.GetJobByIdAsync(job.Id);
        completedJob!.StatusEnum.Should().Be(TonieUploadJobStatus.Completed);
        completedJob.ProgressPercentage.Should().Be(100);
        completedJob.StatusMessage.Should().Be("Upload completed successfully");
        completedJob.SyncResultJson.Should().Be(syncResult);
        completedJob.Completed.Should().NotBeNull();
        completedJob.Completed.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task FailJobAsync_MarksJobAsFailed()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();
        var job = await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);
        var errorMessage = "Upload failed due to network error";

        // Act
        await _service.FailJobAsync(job.Id, errorMessage);

        // Assert
        var failedJob = await _service.GetJobByIdAsync(job.Id);
        failedJob!.StatusEnum.Should().Be(TonieUploadJobStatus.Failed);
        failedJob.StatusMessage.Should().Be("Upload failed");
        failedJob.ErrorMessage.Should().Be(errorMessage);
        failedJob.Completed.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelJobAsync_FromPending_MarksCancelled()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();
        var job = await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);

        // Act
        await _service.CancelJobAsync(job.Id);

        // Assert
        var cancelledJob = await _service.GetJobByIdAsync(job.Id);
        cancelledJob!.StatusEnum.Should().Be(TonieUploadJobStatus.Cancelled);
        cancelledJob.StatusMessage.Should().Be("Upload cancelled by user");
        cancelledJob.Completed.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelJobAsync_FromCompleted_DoesNotCancel()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();
        var job = await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);
        await _service.CompleteJobAsync(job.Id);

        // Act
        await _service.CancelJobAsync(job.Id);

        // Assert
        var completedJob = await _service.GetJobByIdAsync(job.Id);
        completedJob!.StatusEnum.Should().Be(TonieUploadJobStatus.Completed);
    }

    [Fact]
    public async Task CancelJobAsync_FromFailed_DoesNotCancel()
    {
        // Arrange
        var libraryItemId = await CreateTestLibraryItem();
        var job = await _service.CreateUploadJobAsync(_testUserId, libraryItemId, _testHouseholdId, _testTonieId);
        await _service.FailJobAsync(job.Id, "Error");

        // Act
        await _service.CancelJobAsync(job.Id);

        // Assert
        var failedJob = await _service.GetJobByIdAsync(job.Id);
        failedJob!.StatusEnum.Should().Be(TonieUploadJobStatus.Failed);
    }

    [Fact]
    public async Task UpdateJobStatusAsync_WithNonExistentJob_DoesNotThrow()
    {
        // Act & Assert - should not throw
        await _service.UpdateJobStatusAsync(99999, TonieUploadJobStatus.Uploading);
    }

    [Fact]
    public async Task CompleteJobAsync_WithNonExistentJob_DoesNotThrow()
    {
        // Act & Assert - should not throw
        await _service.CompleteJobAsync(99999);
    }

    [Fact]
    public async Task FailJobAsync_WithNonExistentJob_DoesNotThrow()
    {
        // Act & Assert - should not throw
        await _service.FailJobAsync(99999, "Error");
    }

    [Fact]
    public async Task CancelJobAsync_WithNonExistentJob_DoesNotThrow()
    {
        // Act & Assert - should not throw
        await _service.CancelJobAsync(99999);
    }

    private async Task<int> CreateTestLibraryItem(string? userId = null)
    {
        userId ??= _testUserId;

        await using var dbContext = new ApplicationDbContext(_dbContextOptions);

        // Create user if not exists
        if (!await dbContext.Users.AnyAsync(u => u.Id == userId))
        {
            dbContext.Users.Add(new ApplicationUser 
            { 
                Id = userId, 
                UserName = $"{userId}@test.com",
                Email = $"{userId}@test.com",
                Name = "Test User"
            });
            await dbContext.SaveChangesAsync();
        }

        // Create FileUpload
        var fileUpload = new FileUpload
        {
            Id = Guid.NewGuid(),
            ContentType = "audio/mpeg",
            FileName = "test.mp3",
            FileCategory = "Audio",
            FileSizeBytes = 1024000,
            Provider = StorageProvider.Database,
            Data = new byte[1024]
        };
        dbContext.FileUploads.Add(fileUpload);

        // Create MediaLibraryItem
        var libraryItem = new MediaLibraryItem
        {
            UserId = userId,
            Title = "Test Audio",
            Description = "Test Description",
            FileUploadId = fileUpload.Id,
            DurationSeconds = 180,
            FileSizeBytes = 1024000,
            ContentType = "audio/mpeg",
            OriginalFileName = "test.mp3",
            Category = "Music",
            Created = DateTimeOffset.UtcNow
        };
        dbContext.MediaLibraryItems.Add(libraryItem);

        await dbContext.SaveChangesAsync();
        return libraryItem.Id;
    }

    public void Dispose()
    {
        using var dbContext = new ApplicationDbContext(_dbContextOptions);
        dbContext.Database.EnsureDeleted();
    }
}
