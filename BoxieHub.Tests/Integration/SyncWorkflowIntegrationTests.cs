using BoxieHub.Client.Models.Enums;
using BoxieHub.Services.PythonAdapter;
using BoxieHub.Services.PythonAdapter.Dtos;
using BoxieHub.Services.Sync;
using BoxieHub.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BoxieHub.Tests.Integration;

/// <summary>
/// Integration tests for the complete sync workflow.
/// Tests the interaction between SyncJobService, PythonAdapterClient, and database.
/// </summary>
[TestFixture]
public class SyncWorkflowIntegrationTests : TestBase
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
    public async Task FullSyncWorkflow_SuccessfulScenario()
    {
        // Arrange - Set up test data
        var household = CreateTestHousehold("Smith Family");
        var device = CreateTestDevice(household, "Creative Tonie - Bedroom", "bedroom-tonie-001");
        var upload = CreateTestImageUpload("story.mp3");
        var contentItem = CreateTestContentItem(household, upload, "Bedtime Stories");
        var assignment = CreateTestContentAssignment(household, device, contentItem);

        // Configure adapter to return success
        _mockAdapterClient
            .Setup(x => x.SyncAsync(It.IsAny<SyncRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResponseDto
            {
                Success = true,
                Message = "Successfully synced to device",
                TracksProcessed = 1
            });

        // Act - Execute sync
        var job = await _service.ExecuteSyncAsync(device.Id, assignment.Id, "admin@smith.com");

        // Assert - Verify sync succeeded
        Assert.That(job.Status, Is.EqualTo(SyncStatus.Completed));
        Assert.That(job.ErrorMessage, Is.Null);

        // Verify adapter was called with correct data
        _mockAdapterClient.Verify(
            x => x.SyncAsync(It.IsAny<SyncRequestDto>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify job can be retrieved with status
        var retrievedJob = await _service.GetJobAsync(job.Id);
        Assert.That(retrievedJob.Status, Is.EqualTo(SyncStatus.Completed));

        // Verify job appears in household jobs list
        var householdJobs = await _service.GetJobsAsync(household.Id);
        Assert.That(householdJobs.Any(j => j.Id == job.Id), Is.True);
    }

    [Test]
    public async Task FullSyncWorkflow_AdapterFailureScenario()
    {
        // Arrange
        var household = CreateTestHousehold("Johnson Family");
        var device = CreateTestDevice(household, "Creative Tonie - Playroom", "playroom-tonie-002");
        var upload = CreateTestImageUpload("adventure.mp3");
        var contentItem = CreateTestContentItem(household, upload, "Adventure Stories");
        var assignment = CreateTestContentAssignment(household, device, contentItem);

        // Configure adapter to return failure
        _mockAdapterClient
            .Setup(x => x.SyncAsync(It.IsAny<SyncRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResponseDto
            {
                Success = false,
                Message = "Sync failed",
                ErrorDetails = "Device is offline",
                TracksProcessed = 0
            });

        // Act
        var job = await _service.ExecuteSyncAsync(device.Id, assignment.Id, "admin@johnson.com");

        // Assert
        Assert.That(job.Status, Is.EqualTo(SyncStatus.Failed));
        Assert.That(job.ErrorMessage, Is.EqualTo("Device is offline"));

        // Verify failure can be retrieved
        var retrievedJob = await _service.GetJobAsync(job.Id);
        Assert.That(retrievedJob.Status, Is.EqualTo(SyncStatus.Failed));
    }

    [Test]
    public async Task FullSyncWorkflow_MultipleDevicesInHousehold()
    {
        // Arrange - Set up multiple devices in one household
        var household = CreateTestHousehold("Multi-Device House");
        var device1 = CreateTestDevice(household, "Living Room", "living-room-001");
        var device2 = CreateTestDevice(household, "Bedroom", "bedroom-001");
        var device3 = CreateTestDevice(household, "Playroom", "playroom-001");

        var upload = CreateTestImageUpload("story.mp3");
        var contentItem = CreateTestContentItem(household, upload);

        var assignment1 = CreateTestContentAssignment(household, device1, contentItem);
        var assignment2 = CreateTestContentAssignment(household, device2, contentItem);
        var assignment3 = CreateTestContentAssignment(household, device3, contentItem);

        _mockAdapterClient
            .Setup(x => x.SyncAsync(It.IsAny<SyncRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResponseDto { Success = true, Message = "OK", TracksProcessed = 1 });

        // Act - Sync to all devices
        var job1 = await _service.ExecuteSyncAsync(device1.Id, assignment1.Id, "admin");
        var job2 = await _service.ExecuteSyncAsync(device2.Id, assignment2.Id, "admin");
        var job3 = await _service.ExecuteSyncAsync(device3.Id, assignment3.Id, "admin");

        // Assert - All jobs succeeded
        Assert.That(job1.Status, Is.EqualTo(SyncStatus.Completed));
        Assert.That(job2.Status, Is.EqualTo(SyncStatus.Completed));
        Assert.That(job3.Status, Is.EqualTo(SyncStatus.Completed));

        // Verify all three jobs appear in household history
        var householdJobs = await _service.GetJobsAsync(household.Id);
        Assert.That(householdJobs.Count(), Is.EqualTo(3));
        Assert.That(householdJobs.All(j => j.HouseholdId == household.Id), Is.True);

        // Verify adapter was called 3 times
        _mockAdapterClient.Verify(
            x => x.SyncAsync(It.IsAny<SyncRequestDto>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Test]
    public async Task FullSyncWorkflow_MultipleContentItems()
    {
        // Arrange - One device with multiple content assignments
        var household = CreateTestHousehold("Story House");
        var device = CreateTestDevice(household, "Main Tonie", "main-001");
        var upload1 = CreateTestImageUpload("bedtime-stories.mp3");
        var upload2 = CreateTestImageUpload("adventure-tales.mp3");
        var upload3 = CreateTestImageUpload("fairytales.mp3");

        var content1 = CreateTestContentItem(household, upload1, "Bedtime Stories");
        var content2 = CreateTestContentItem(household, upload2, "Adventure Tales");
        var content3 = CreateTestContentItem(household, upload3, "Classic Fairytales");

        var assignment1 = CreateTestContentAssignment(household, device, content1);
        var assignment2 = CreateTestContentAssignment(household, device, content2);
        var assignment3 = CreateTestContentAssignment(household, device, content3);

        _mockAdapterClient
            .Setup(x => x.SyncAsync(It.IsAny<SyncRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResponseDto { Success = true, Message = "OK", TracksProcessed = 1 });

        // Act - Sync each content to the device
        var job1 = await _service.ExecuteSyncAsync(device.Id, assignment1.Id, "admin");
        var job2 = await _service.ExecuteSyncAsync(device.Id, assignment2.Id, "admin");
        var job3 = await _service.ExecuteSyncAsync(device.Id, assignment3.Id, "admin");

        // Assert
        Assert.That(job1.Status, Is.EqualTo(SyncStatus.Completed));
        Assert.That(job2.Status, Is.EqualTo(SyncStatus.Completed));
        Assert.That(job3.Status, Is.EqualTo(SyncStatus.Completed));

        // Verify device has 3 completed sync jobs
        var deviceJobs = await _service.GetJobsAsync(household.Id);
        var devicesJobsForThisDevice = deviceJobs.Where(j => j.DeviceId == device.Id).ToList();
        Assert.That(devicesJobsForThisDevice.Count(), Is.EqualTo(3));
        Assert.That(devicesJobsForThisDevice.All(j => j.Status == SyncStatus.Completed), Is.True);
    }

    [Test]
    public async Task FullSyncWorkflow_RetryAfterFailure()
    {
        // Arrange
        var household = CreateTestHousehold("Retry House");
        var device = CreateTestDevice(household, "Flakey Tonie", "flakey-001");
        var upload = CreateTestImageUpload("story.mp3");
        var contentItem = CreateTestContentItem(household, upload);
        var assignment = CreateTestContentAssignment(household, device, contentItem);

        // First attempt fails
        _mockAdapterClient
            .SetupSequence(x => x.SyncAsync(It.IsAny<SyncRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResponseDto { Success = false, Message = "Device offline", ErrorDetails = "Timeout", TracksProcessed = 0 })
            .ReturnsAsync(new SyncResponseDto { Success = true, Message = "OK", TracksProcessed = 1 });

        // Act - First attempt fails
        var job1 = await _service.ExecuteSyncAsync(device.Id, assignment.Id, "admin");
        Assert.That(job1.Status, Is.EqualTo(SyncStatus.Failed));

        // Act - Retry succeeds
        var job2 = await _service.ExecuteSyncAsync(device.Id, assignment.Id, "admin");
        Assert.That(job2.Status, Is.EqualTo(SyncStatus.Completed));

        // Assert - Both jobs are in history
        var householdJobs = await _service.GetJobsAsync(household.Id);
        Assert.That(householdJobs.Count(), Is.EqualTo(2));

        var failedJob = householdJobs.FirstOrDefault(j => j.Id == job1.Id);
        var succeededJob = householdJobs.FirstOrDefault(j => j.Id == job2.Id);

        Assert.That(failedJob.Status, Is.EqualTo(SyncStatus.Failed));
        Assert.That(succeededJob.Status, Is.EqualTo(SyncStatus.Completed));
    }

    [Test]
    public async Task FullSyncWorkflow_JobHistoryTracking()
    {
        // Arrange
        var household = CreateTestHousehold("Tracking House");
        var device = CreateTestDevice(household, "Tracked Tonie", "tracked-001");
        var upload = CreateTestImageUpload("story.mp3");
        var contentItem = CreateTestContentItem(household, upload);
        var assignment = CreateTestContentAssignment(household, device, contentItem);

        _mockAdapterClient
            .Setup(x => x.SyncAsync(It.IsAny<SyncRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResponseDto { Success = true, Message = "OK", TracksProcessed = 1 });

        // Act - Create multiple jobs over time
        var jobs = new List<Models.SyncJob>();
        for (int i = 0; i < 5; i++)
        {
            var job = await _service.ExecuteSyncAsync(device.Id, assignment.Id, $"admin{i}");
            jobs.Add(job);
            System.Threading.Thread.Sleep(10);
        }

        // Assert - All jobs are tracked
        var householdJobs = (await _service.GetJobsAsync(household.Id)).ToList();
        Assert.That(householdJobs.Count(), Is.EqualTo(5));

        // Verify they're in reverse chronological order
        for (int i = 0; i < 4; i++)
        {
            Assert.That(householdJobs[i].Created, Is.GreaterThanOrEqualTo(householdJobs[i + 1].Created));
        }

        // Verify all have start and completion times
        Assert.That(householdJobs.All(j => j.Started.HasValue), Is.True);
        Assert.That(householdJobs.All(j => j.Completed.HasValue), Is.True);
    }
}
