using BoxieHub.Client.Models.Enums;
using BoxieHub.Data;
using BoxieHub.Models;
using BoxieHub.Models.BoxieCloud;
using BoxieHub.Services;
using BoxieHub.Services.BoxieCloud;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BoxieHub.Tests.Unit.Services;

public class TonieServiceCascadeDeleteTests : IDisposable
{
    private readonly Mock<IBoxieCloudClient> _mockBoxieClient;
    private readonly Mock<ICredentialEncryptionService> _mockEncryption;
    private readonly Mock<IDbContextFactory<ApplicationDbContext>> _mockDbContextFactory;
    private readonly ApplicationDbContext _dbContext;
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions;
    private readonly Mock<ILogger<TonieService>> _mockLogger;
    private readonly TonieService _service;
    private readonly string _testUserId = "test-user-123";

    public TonieServiceCascadeDeleteTests()
    {
        // Setup in-memory database OPTIONS (shared across contexts)
        _dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new ApplicationDbContext(_dbOptions);

        // Setup mocks
        _mockBoxieClient = new Mock<IBoxieCloudClient>();
        _mockEncryption = new Mock<ICredentialEncryptionService>();
        _mockLogger = new Mock<ILogger<TonieService>>();

        // Mock IDbContextFactory to create NEW contexts each time (to avoid disposal issues)
        _mockDbContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        _mockDbContextFactory.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ApplicationDbContext(_dbOptions)); // Create new instance

        // Create service
        _service = new TonieService(
            _mockBoxieClient.Object,
            _mockEncryption.Object,
            _mockDbContextFactory.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task DeleteAccountDataAsync_RemovesHouseholdsAndCreativeTonies()
    {
        // Arrange
        var credential = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "test@example.com",
            EncryptedPassword = "encrypted",
            DisplayName = "Test Account",
            IsDefault = true,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };
        _dbContext.TonieCredentials.Add(credential);
        await _dbContext.SaveChangesAsync();

        // Create household synced from Tonie Cloud (has ExternalId)
        var syncedHousehold = new Household
        {
            Name = "Synced Household",
            ExternalId = "tonie-household-123", // From Tonie Cloud
            Created = DateTimeOffset.UtcNow,
            LastSyncedAt = DateTimeOffset.UtcNow
        };
        _dbContext.Households.Add(syncedHousehold);
        await _dbContext.SaveChangesAsync();

        // Link user to household
        var householdMember = new HouseholdMember
        {
            HouseholdId = syncedHousehold.Id,
            UserId = _testUserId,
            Role = Role.Admin,
            Created = DateTimeOffset.UtcNow
        };
        _dbContext.HouseholdMembers.Add(householdMember);
        await _dbContext.SaveChangesAsync();

        // Create creative tonies (synced from Tonie Cloud)
        var creativeTonie1 = new Character
        {
            Type = CharacterType.Creative,
            Name = "Creative Tonie 1",
            ExternalCharacterId = "tonie-123",
            HouseholdId = syncedHousehold.Id,
            Created = DateTimeOffset.UtcNow,
            LastSyncedAt = DateTimeOffset.UtcNow
        };
        var creativeTonie2 = new Character
        {
            Type = CharacterType.Creative,
            Name = "Creative Tonie 2",
            ExternalCharacterId = "tonie-456",
            HouseholdId = syncedHousehold.Id,
            Created = DateTimeOffset.UtcNow,
            LastSyncedAt = DateTimeOffset.UtcNow
        };
        _dbContext.Characters.AddRange(creativeTonie1, creativeTonie2);
        await _dbContext.SaveChangesAsync();
        
        _dbContext.ChangeTracker.Clear(); // Clear tracking

        // Act
        await _service.DeleteAccountDataAsync(credential.Id);

        // Assert - Create fresh context to verify changes
        using var assertContext = new ApplicationDbContext(_dbOptions);
        var remainingHouseholds = await assertContext.Households.ToListAsync();
        var remainingCharacters = await assertContext.Characters.ToListAsync();

        remainingHouseholds.Should().BeEmpty("synced households should be deleted");
        remainingCharacters.Should().BeEmpty("creative tonies should be deleted");
    }

    [Fact]
    public async Task DeleteAccountDataAsync_DoesNotDeleteLocalContentOrDevices()
    {
        // Arrange
        var credential = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "test@example.com",
            EncryptedPassword = "encrypted",
            DisplayName = "Test Account",
            IsDefault = true,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };
        _dbContext.TonieCredentials.Add(credential);
        await _dbContext.SaveChangesAsync();

        // Create a LOCAL household (no ExternalId)
        var localHousehold = new Household
        {
            Name = "Local Household",
            ExternalId = null, // Local, not from Tonie Cloud
            Created = DateTimeOffset.UtcNow
        };
        _dbContext.Households.Add(localHousehold);
        await _dbContext.SaveChangesAsync();

        // Create a local device
        var device = new Device
        {
            HouseholdId = localHousehold.Id,
            Name = "My Toniebox",
            DeviceIdentifier = "device-001",
            Status = DeviceStatus.Online,
            Created = DateTimeOffset.UtcNow
        };
        _dbContext.Devices.Add(device);

        // Create a content item (user's audio)
        var upload = new ImageUpload
        {
            Id = Guid.NewGuid(),
            Data = new byte[] { 0x01, 0x02, 0x03 },
            ContentType = "audio/mpeg",
            FileName = "test.mp3",
            FileSizeBytes = 3
        };
        _dbContext.Images.Add(upload);

        var contentItem = new ContentItem
        {
            HouseholdId = localHousehold.Id,
            Title = "My Audio",
            ContentType = ContentType.Audio,
            UploadId = upload.Id,
            Created = DateTimeOffset.UtcNow
        };
        _dbContext.ContentItems.Add(contentItem);

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear(); // Clear tracking

        // Act
        await _service.DeleteAccountDataAsync(credential.Id);

        // Assert - Create fresh context to verify changes
        using var assertContext = new ApplicationDbContext(_dbOptions);
        var remainingHouseholds = await assertContext.Households.ToListAsync();
        var remainingDevices = await assertContext.Devices.ToListAsync();
        var remainingContent = await assertContext.ContentItems.ToListAsync();

        remainingHouseholds.Should().ContainSingle("local household should NOT be deleted");
        remainingDevices.Should().ContainSingle("devices should NOT be deleted");
        remainingContent.Should().ContainSingle("content items should NOT be deleted");
    }

    [Fact]
    public async Task DeleteAccountDataAsync_OnlyDeletesCreativeTonies_NotContentTonies()
    {
        // Arrange
        var credential = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "test@example.com",
            EncryptedPassword = "encrypted",
            DisplayName = "Test Account",
            IsDefault = true,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };
        _dbContext.TonieCredentials.Add(credential);
        await _dbContext.SaveChangesAsync();

        var syncedHousehold = new Household
        {
            Name = "Synced Household",
            ExternalId = "tonie-household-123",
            Created = DateTimeOffset.UtcNow
        };
        _dbContext.Households.Add(syncedHousehold);
        await _dbContext.SaveChangesAsync();

        var householdMember = new HouseholdMember
        {
            HouseholdId = syncedHousehold.Id,
            UserId = _testUserId,
            Role = Role.Admin,
            Created = DateTimeOffset.UtcNow
        };
        _dbContext.HouseholdMembers.Add(householdMember);
        await _dbContext.SaveChangesAsync();

        // Create a CONTENT tonie (store-bought, not Creative)
        var contentTonie = new Character
        {
            Type = CharacterType.Content,
            Name = "Peppa Pig Tonie",
            ExternalCharacterId = "content-tonie-123",
            HouseholdId = syncedHousehold.Id,
            Created = DateTimeOffset.UtcNow
        };

        // Create a CREATIVE tonie
        var creativeTonie = new Character
        {
            Type = CharacterType.Creative,
            Name = "My Creative Tonie",
            ExternalCharacterId = "creative-tonie-456",
            HouseholdId = syncedHousehold.Id,
            Created = DateTimeOffset.UtcNow
        };

        _dbContext.Characters.AddRange(contentTonie, creativeTonie);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear(); // Clear tracking

        // Act
        await _service.DeleteAccountDataAsync(credential.Id);

        // Assert - Create fresh context to verify changes
        using var assertContext = new ApplicationDbContext(_dbOptions);
        var remainingCharacters = await assertContext.Characters.ToListAsync();
        var remainingHouseholds = await assertContext.Households.ToListAsync();
        
        // Only Creative Tonies should be deleted (the service only deletes CharacterType.Creative)
        // Content Tonies remain, but the household is gone
        remainingCharacters.Should().ContainSingle("only content tonie should remain");
        remainingCharacters.Single().Type.Should().Be(CharacterType.Content);
        remainingHouseholds.Should().BeEmpty("synced household should be deleted");
    }

    [Fact]
    public async Task DeleteAccountDataAsync_WithNonExistentCredential_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        await _service.DeleteAccountDataAsync(99999); // Non-existent ID

        // Verify no changes were made - Create fresh context
        using var assertContext = new ApplicationDbContext(_dbOptions);
        var households = await assertContext.Households.ToListAsync();
        var characters = await assertContext.Characters.ToListAsync();

        households.Should().BeEmpty();
        characters.Should().BeEmpty();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
