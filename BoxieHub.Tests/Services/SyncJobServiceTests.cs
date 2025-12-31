using BoxieHub.Client.Models.Enums;
using BoxieHub.Services.PythonAdapter;
using BoxieHub.Services.PythonAdapter.Dtos;
using BoxieHub.Services.Sync;
using BoxieHub.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BoxieHub.Tests.Services.Sync;

/// <summary>
/// Unit tests for SyncJobService.
/// Tests sync job creation, execution, and status tracking.
/// </summary>
[TestFixture]
public class SyncJobServiceTests : TestBase
{
    private Mock<IPythonAdapterClient> _mockAdapterClient;
    private ISyncJobService _service;
    private ILogger<SyncJobService> _logger;

    [SetUp]
    public void SetUp()
    {
        base.SetUpDatabase();
        _mockAdapterClient = new Mock<IPythonAdapterClient>();
        _logger = new Mock<ILogger<SyncJobService>>().Object;
        _service = new SyncJobService(DbContext, _mockAdapterClient.Object, _logger);
    }

    [Test]
    public async Task ExecuteSyncAsync_CreatesJobWithPendingStatus()
    {
        // Arrange
        var household = CreateTestHousehold();
        var upload = CreateTestImageUpload();
        var contentItem = CreateTestContentItem(household, upload);
        var device = CreateTestDevice(household);
        var assignment = CreateTestContentAssignment(household, device, contentItem);

        _mockAdapterClient
            .Setup(x => x.SyncAsync(It.IsAny<SyncRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResponseDto
            {
                Success = true,
                Message = "Sync successful",
                TracksProcessed = 1
            });

        // Act
        var job = await _service.ExecuteSyncAsync(device.Id, assignment.Id, "testuser");

        // Assert
        Assert.That(job, Is.Not.Null);
        Assert.That(job.HouseholdId, Is.EqualTo(household.Id));
        Assert.That(job.DeviceId, Is.EqualTo(device.Id));
        Assert.That(job.Status, Is.EqualTo(SyncStatus.Completed));
        Assert.That(job.Created, Is.Not.EqualTo(default(DateTimeOffset)));
    }

    [Test]
    public async Task ExecuteSyncAsync_CallsPythonAdapterWithCorrectPayload()
    {
        // Arrange
        var household = CreateTestHousehold();
        var upload = CreateTestImageUpload();
        var contentItem = CreateTestContentItem(household, upload, "Test Story");
        var device = CreateTestDevice(household, deviceIdentifier: "tonie-xyz");
        var assignment = CreateTestContentAssignment(household, device, contentItem);

        SyncRequestDto capturedRequest = null;
        _mockAdapterClient
            .Setup(x => x.SyncAsync(It.IsAny<SyncRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<SyncRequestDto, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new SyncResponseDto
            {
                Success = true,
                Message = "OK",
                TracksProcessed = 1
            });

        // Act
        await _service.ExecuteSyncAsync(device.Id, assignment.Id, "testuser");

        // Assert
        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest.CreativeTonieExternalId, Is.EqualTo("tonie-xyz"));
        Assert.That(capturedRequest.Tracks, Is.Not.Empty);
        Assert.That(capturedRequest.Tracks.First().Title, Is.EqualTo("Test Story"));
    }

    [Test]
    public async Task ExecuteSyncAsync_WhenAdapterFails_SetsJobStatusToFailed()
    {
        // Arrange
        var household = CreateTestHousehold();
        var upload = CreateTestImageUpload();
        var contentItem = CreateTestContentItem(household, upload);
        var device = CreateTestDevice(household);
        var assignment = CreateTestContentAssignment(household, device, contentItem);

        _mockAdapterClient
            .Setup(x => x.SyncAsync(It.IsAny<SyncRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResponseDto
            {
                Success = false,
                Message = "Sync failed",
                ErrorDetails = "Device offline",
                TracksProcessed = 0
            });

        // Act
        var job = await _service.ExecuteSyncAsync(device.Id, assignment.Id, "testuser");

        // Assert
        Assert.That(job.Status, Is.EqualTo(SyncStatus.Failed));
        Assert.That(job.ErrorMessage, Is.EqualTo("Device offline"));
    }

    [Test]
    public async Task ExecuteSyncAsync_WhenAdapterThrowsException_CatchesAndSetsFailed()
    {
        // Arrange
        var household = CreateTestHousehold();
        var upload = CreateTestImageUpload();
        var contentItem = CreateTestContentItem(household, upload);
        var device = CreateTestDevice(household);
        var assignment = CreateTestContentAssignment(household, device, contentItem);

        _mockAdapterClient
            .Setup(x => x.SyncAsync(It.IsAny<SyncRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection timeout"));

        // Act
        var job = await _service.ExecuteSyncAsync(device.Id, assignment.Id, "testuser");

        // Assert
        Assert.That(job.Status, Is.EqualTo(SyncStatus.Failed));
        Assert.That(job.ErrorMessage, Does.Contain("Connection timeout"));
    }

    [Test]
    public async Task ExecuteSyncAsync_WithInvalidDevice_ThrowsArgumentException()
    {
        // Arrange
        var invalidDeviceId = 99999;
        var invalidAssignmentId = 99999;

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(() => 
            _service.ExecuteSyncAsync(invalidDeviceId, invalidAssignmentId, "testuser"));
        Assert.That(ex.Message, Does.Contain("Device"));
    }

    [Test]
    public async Task ExecuteSyncAsync_WithInvalidAssignment_ThrowsArgumentException()
    {
        // Arrange
        var household = CreateTestHousehold();
        var device = CreateTestDevice(household);
        var invalidAssignmentId = 99999;

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ExecuteSyncAsync(device.Id, invalidAssignmentId, "testuser"));
        Assert.That(ex.Message, Does.Contain("Assignment"));
    }

    [Test]
    public async Task ExecuteSyncAsync_SetsStartedAndCompletedTimes()
    {
        // Arrange
        var household = CreateTestHousehold();
        var upload = CreateTestImageUpload();
        var contentItem = CreateTestContentItem(household, upload);
        var device = CreateTestDevice(household);
        var assignment = CreateTestContentAssignment(household, device, contentItem);

        _mockAdapterClient
            .Setup(x => x.SyncAsync(It.IsAny<SyncRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResponseDto
            {
                Success = true,
                Message = "OK",
                TracksProcessed = 1
            });

        var beforeExecution = DateTimeOffset.UtcNow;

        // Act
        var job = await _service.ExecuteSyncAsync(device.Id, assignment.Id, "testuser");

        var afterExecution = DateTimeOffset.UtcNow;

        // Assert
        Assert.That(job.Started, Is.Not.Null);
        Assert.That(job.Completed, Is.Not.Null);
        Assert.That(job.Started, Is.GreaterThanOrEqualTo(beforeExecution));
        Assert.That(job.Completed, Is.LessThanOrEqualTo(afterExecution));
        Assert.That(job.Completed, Is.GreaterThanOrEqualTo(job.Started));
    }

    [Test]
    public async Task GetJobAsync_ReturnsJobWhenFound()
    {
        // Arrange
        var household = CreateTestHousehold();
        var device = CreateTestDevice(household);
        var job = CreateTestSyncJob(household, device, status: SyncStatus.Completed);

        // Act
        var retrievedJob = await _service.GetJobAsync(job.Id);

        // Assert
        Assert.That(retrievedJob, Is.Not.Null);
        Assert.That(retrievedJob.Id, Is.EqualTo(job.Id));
        Assert.That(retrievedJob.Status, Is.EqualTo(SyncStatus.Completed));
    }

    [Test]
    public async Task GetJobAsync_ReturnsNullWhenNotFound()
    {
        // Act
        var job = await _service.GetJobAsync(99999);

        // Assert
        Assert.That(job, Is.Null);
    }

    [Test]
    public async Task GetJobsAsync_ReturnsJobsForHousehold()
    {
        // Arrange
        var household1 = CreateTestHousehold("Household 1");
        var household2 = CreateTestHousehold("Household 2");

        var device1 = CreateTestDevice(household1);
        var device2 = CreateTestDevice(household2);

        var job1 = CreateTestSyncJob(household1, device1);
        var job2 = CreateTestSyncJob(household1, device1);
        var job3 = CreateTestSyncJob(household2, device2);

        // Act
        var result = await _service.GetJobsAsync(household1.Id);

        // Assert
        Assert.That(result.Count(), Is.EqualTo(2));
        Assert.That(result.All(j => j.HouseholdId == household1.Id), Is.True);
    }

    [Test]
    public async Task GetJobsAsync_ReturnsJobsOrderedByMostRecentFirst()
    {
        // Arrange
        var household = CreateTestHousehold();
        var device = CreateTestDevice(household);

        var job1 = CreateTestSyncJob(household, device);
        System.Threading.Thread.Sleep(10);
        var job2 = CreateTestSyncJob(household, device);
        System.Threading.Thread.Sleep(10);
        var job3 = CreateTestSyncJob(household, device);

        // Act
        var result = await _service.GetJobsAsync(household.Id);

        // Assert
        var orderedJobs = result.ToList();
        Assert.That(orderedJobs[0].Id, Is.EqualTo(job3.Id));
        Assert.That(orderedJobs[1].Id, Is.EqualTo(job2.Id));
        Assert.That(orderedJobs[2].Id, Is.EqualTo(job1.Id));
    }

    [Test]
    public async Task GetJobsAsync_WithLimit_ReturnsOnlySpecifiedNumber()
    {
        // Arrange
        var household = CreateTestHousehold();
        var device = CreateTestDevice(household);

        CreateTestSyncJob(household, device);
        CreateTestSyncJob(household, device);
        CreateTestSyncJob(household, device);
        CreateTestSyncJob(household, device);
        CreateTestSyncJob(household, device);

        // Act
        var result = await _service.GetJobsAsync(household.Id, limit: 3);

        // Assert
        Assert.That(result.Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task GetJobsAsync_WithNonexistentHousehold_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetJobsAsync(99999);

        // Assert
        Assert.That(result.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task ExecuteSyncAsync_PersistsJobToDatabase()
    {
        // Arrange
        var household = CreateTestHousehold();
        var upload = CreateTestImageUpload();
        var contentItem = CreateTestContentItem(household, upload);
        var device = CreateTestDevice(household);
        var assignment = CreateTestContentAssignment(household, device, contentItem);

        _mockAdapterClient
            .Setup(x => x.SyncAsync(It.IsAny<SyncRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResponseDto { Success = true, Message = "OK", TracksProcessed = 1 });

        // Act
        var job = await _service.ExecuteSyncAsync(device.Id, assignment.Id, "testuser");

        // Assert - Verify it was actually saved to DB
        var savedJob = await _service.GetJobAsync(job.Id);
        Assert.That(savedJob, Is.Not.Null);
        Assert.That(savedJob.Status, Is.EqualTo(SyncStatus.Completed));
    }
}
