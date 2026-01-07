using BoxieHub.Client.Models.Enums;
using BoxieHub.Data;
using BoxieHub.Models;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace BoxieHub.Tests.Fixtures;

/// <summary>
/// Base class for tests that need an in-memory database context.
/// Provides utilities for setting up test data.
/// </summary>
public abstract class TestBase
{
    protected ApplicationDbContext DbContext { get; private set; }

    [SetUp]
    public void SetUpDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"BoxieHub_Test_{Guid.NewGuid()}")
            .Options;

        DbContext = new ApplicationDbContext(options);
        DbContext.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        DbContext?.Dispose();
    }

    /// <summary>
    /// Creates a test household with a given name.
    /// </summary>
    protected Household CreateTestHousehold(string name = "Test Household", string? externalId = null)
    {
        var household = new Household
        {
            Name = name,
            ExternalId = externalId ?? $"test-household-{Guid.NewGuid()}",
            Created = DateTimeOffset.UtcNow
        };
        DbContext.Households.Add(household);
        DbContext.SaveChanges();
        return household;
    }

    /// <summary>
    /// Creates a test device with required dependencies.
    /// </summary>
    protected Device CreateTestDevice(Household household, string name = "Test Device", string deviceIdentifier = "tonie-001")
    {
        var device = new Device
        {
            HouseholdId = household.Id,
            Name = name,
            DeviceIdentifier = deviceIdentifier,
            Status = DeviceStatus.Unknown,
            Created = DateTimeOffset.UtcNow
        };
        DbContext.Devices.Add(device);
        DbContext.SaveChanges();
        return device;
    }

    /// <summary>
    /// Creates a test image upload.
    /// </summary>
    protected ImageUpload CreateTestImageUpload(string fileName = "test.mp3")
    {
        var upload = new ImageUpload
        {
            Id = Guid.NewGuid(),
            Data = new byte[] { 0x01, 0x02, 0x03, 0x04 },
            ContentType = "audio/mpeg",
            FileName = fileName,
            FileSizeBytes = 4
        };
        DbContext.Images.Add(upload);
        DbContext.SaveChanges();
        return upload;
    }

    /// <summary>
    /// Creates a test content item with required dependencies.
    /// </summary>
    protected ContentItem CreateTestContentItem(Household household, ImageUpload upload, string title = "Test Audio")
    {
        var contentItem = new ContentItem
        {
            HouseholdId = household.Id,
            Title = title,
            Description = "Test content item",
            ContentType = ContentType.Audio,
            UploadId = upload.Id,
            Created = DateTimeOffset.UtcNow
        };
        DbContext.ContentItems.Add(contentItem);
        DbContext.SaveChanges();
        return contentItem;
    }

    /// <summary>
    /// Creates a test content assignment.
    /// </summary>
    protected ContentAssignment CreateTestContentAssignment(
        Household household,
        Device device,
        ContentItem contentItem,
        bool isActive = true)
    {
        var assignment = new ContentAssignment
        {
            HouseholdId = household.Id,
            ContentItemId = contentItem.Id,
            DeviceId = device.Id,
            TargetType = AssignmentTarget.Device,
            IsActive = isActive,
            Created = DateTimeOffset.UtcNow
        };
        DbContext.ContentAssignments.Add(assignment);
        DbContext.SaveChanges();
        return assignment;
    }

    /// <summary>
    /// Creates a test sync job.
    /// </summary>
    protected SyncJob CreateTestSyncJob(
        Household household,
        Device device,
        ContentItem? contentItem = null,
        SyncStatus status = SyncStatus.Pending)
    {
        var job = new SyncJob
        {
            HouseholdId = household.Id,
            DeviceId = device.Id,
            ContentItemId = contentItem?.Id,
            Status = status,
            JobType = "Upload",
            Created = DateTimeOffset.UtcNow
        };
        DbContext.SyncJobs.Add(job);
        DbContext.SaveChanges();
        return job;
    }
}
