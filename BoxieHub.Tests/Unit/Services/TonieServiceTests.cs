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

public class TonieServiceTests : IDisposable
{
    private readonly Mock<IBoxieCloudClient> _mockBoxieClient;
    private readonly Mock<ICredentialEncryptionService> _mockEncryption;
    private readonly Mock<IDbContextFactory<ApplicationDbContext>> _mockDbContextFactory;
    private readonly ApplicationDbContext _dbContext;
    private readonly DbContextOptions<ApplicationDbContext> _dbOptions;
    private readonly Mock<ILogger<TonieService>> _mockLogger;
    private readonly TonieService _service;
    private readonly string _testUserId = "test-user-123";

    public TonieServiceTests()
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

        // Create service (no IMemoryCache needed)
        _service = new TonieService(
            _mockBoxieClient.Object,
            _mockEncryption.Object,
            _mockDbContextFactory.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetUserCreativeTonieAsync_WithValidUser_ReturnsAllTonies()
    {
        // Arrange
        await SetupDefaultCredential();
        
        var households = new List<HouseholdDto>
        {
            new HouseholdDto { Id = "household1", Name = "Test Household", Access = "owner", CanLeave = true, ForeignCreativeTonieContent = false }
        };

        var tonies = new List<CreativeTonieDto>
        {
            CreateTestTonie("tonie1", "Tonie 1", "household1"),
            CreateTestTonie("tonie2", "Tonie 2", "household1")
        };

        _mockBoxieClient.Setup(x => x.GetHouseholdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(households);

        _mockBoxieClient.Setup(x => x.GetCreativeToniesByHouseholdAsync(
            It.IsAny<string>(), It.IsAny<string>(), "household1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tonies);

        // Act
        var (result, isStale) = await _service.GetUserCreativeTonieAsync(_testUserId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(t => t.Name == "Tonie 1");
        result.Should().Contain(t => t.Name == "Tonie 2");
    }

    [Fact]
    public async Task GetUserCreativeTonieAsync_WithMultipleHouseholds_ReturnsAllTonies()
    {
        // Arrange
        await SetupDefaultCredential();

        var households = new List<HouseholdDto>
        {
            new HouseholdDto { Id = "household1", Name = "Household 1", Access = "owner", CanLeave = true, ForeignCreativeTonieContent = false },
            new HouseholdDto { Id = "household2", Name = "Household 2", Access = "member", CanLeave = true, ForeignCreativeTonieContent = false }
        };

        var tonies1 = new List<CreativeTonieDto>
        {
            CreateTestTonie("tonie1", "Tonie 1", "household1")
        };

        var tonies2 = new List<CreativeTonieDto>
        {
            CreateTestTonie("tonie2", "Tonie 2", "household2"),
            CreateTestTonie("tonie3", "Tonie 3", "household2")
        };

        _mockBoxieClient.Setup(x => x.GetHouseholdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(households);

        _mockBoxieClient.Setup(x => x.GetCreativeToniesByHouseholdAsync(
            It.IsAny<string>(), It.IsAny<string>(), "household1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tonies1);

        _mockBoxieClient.Setup(x => x.GetCreativeToniesByHouseholdAsync(
            It.IsAny<string>(), It.IsAny<string>(), "household2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tonies2);

        // Act
        var (result, isStale) = await _service.GetUserCreativeTonieAsync(_testUserId);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetUserCreativeTonieAsync_WithNoDefaultCredential_ThrowsInvalidOperationException()
    {
        // Arrange - No credential in database

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetUserCreativeTonieAsync(_testUserId));
    }

    [Fact]
    public async Task GetUserStatsAsync_CalculatesCorrectStatistics()
    {
        // Arrange
        await SetupDefaultCredential();

        var households = new List<HouseholdDto>
        {
            new HouseholdDto { Id = "household1", Name = "Test Household", Access = "owner", CanLeave = true, ForeignCreativeTonieContent = false }
        };

        var tonies = new List<CreativeTonieDto>
        {
            new CreativeTonieDto
            {
                Id = "tonie1",
                HouseholdId = "household1",
                Name = "Tonie 1",
                ImageUrl = "http://example.com/image1.jpg",
                SecondsPresent = 1800, // 30 minutes
                SecondsRemaining = 3600, // 60 minutes
                ChaptersPresent = 5,
                ChaptersRemaining = 10,
                Transcoding = false,
                Live = true,
                Private = false
            },
            new CreativeTonieDto
            {
                Id = "tonie2",
                HouseholdId = "household1",
                Name = "Tonie 2",
                ImageUrl = "http://example.com/image2.jpg",
                SecondsPresent = 3600, // 60 minutes
                SecondsRemaining = 1800, // 30 minutes
                ChaptersPresent = 10,
                ChaptersRemaining = 5,
                Transcoding = false,
                Live = true,
                Private = false
            }
        };

        _mockBoxieClient.Setup(x => x.GetHouseholdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(households);

        _mockBoxieClient.Setup(x => x.GetCreativeToniesByHouseholdAsync(
            It.IsAny<string>(), It.IsAny<string>(), "household1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tonies);

        // Act
        var stats = await _service.GetUserStatsAsync(_testUserId);

        // Assert
        stats.TotalTonies.Should().Be(2);
        stats.TotalChapters.Should().Be(15); // 5 + 10
        stats.TotalSecondsPresent.Should().Be(5400); // 1800 + 3600
        stats.TotalSecondsRemaining.Should().Be(5400); // 3600 + 1800
        stats.TotalDuration.TotalMinutes.Should().Be(90); // 30 + 60
        stats.FormattedDuration.Should().Be("1h 30m");
    }

    [Fact]
    public async Task GetUserStatsAsync_WithNoTonies_ReturnsZeroStats()
    {
        // Arrange
        await SetupDefaultCredential();

        var households = new List<HouseholdDto>
        {
            new HouseholdDto { Id = "household1", Name = "Test Household", Access = "owner", CanLeave = true, ForeignCreativeTonieContent = false }
        };

        _mockBoxieClient.Setup(x => x.GetHouseholdsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(households);

        _mockBoxieClient.Setup(x => x.GetCreativeToniesByHouseholdAsync(
            It.IsAny<string>(), It.IsAny<string>(), "household1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CreativeTonieDto>());

        // Act
        var stats = await _service.GetUserStatsAsync(_testUserId);

        // Assert
        stats.TotalTonies.Should().Be(0);
        stats.TotalChapters.Should().Be(0);
        stats.TotalSecondsPresent.Should().Be(0);
        stats.FormattedDuration.Should().Be("0m");
    }

    [Fact]
    public async Task DeleteChapterAsync_RemovesChapterSuccessfully()
    {
        // Arrange
        await SetupDefaultCredential();

        var tonie = new CreativeTonieDto
        {
            Id = "tonie1",
            HouseholdId = "household1",
            Name = "Test Tonie",
            ImageUrl = "http://example.com/image.jpg",
            SecondsPresent = 1800,
            SecondsRemaining = 3600,
            ChaptersPresent = 2,
            ChaptersRemaining = 13,
            Transcoding = false,
            Live = true,
            Private = false,
            Chapters = new List<ChapterDto>
            {
                new ChapterDto { Id = "chapter1", Title = "Chapter 1", Seconds = 60, File = "file1", Transcoding = false },
                new ChapterDto { Id = "chapter2", Title = "Chapter 2", Seconds = 120, File = "file2", Transcoding = false }
            }
        };

        _mockBoxieClient.Setup(x => x.GetCreativeTonieDetailsAsync(
            It.IsAny<string>(), It.IsAny<string>(), "household1", "tonie1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tonie);

        _mockBoxieClient.Setup(x => x.PatchCreativeTonieAsync(
            It.IsAny<string>(), It.IsAny<string>(), "household1", "tonie1", It.IsAny<string>(), 
            It.Is<List<ChapterDto>>(chapters => chapters.Count == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tonie);

        // Act
        var result = await _service.DeleteChapterAsync(_testUserId, "household1", "tonie1", "chapter1");

        // Assert
        result.Should().BeTrue();
        _mockBoxieClient.Verify(x => x.PatchCreativeTonieAsync(
            It.IsAny<string>(), It.IsAny<string>(), "household1", "tonie1", "Test Tonie",
            It.Is<List<ChapterDto>>(chapters => chapters.Count == 1 && chapters[0].Id == "chapter2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteChapterAsync_WithNonExistentChapter_ReturnsFalse()
    {
        // Arrange
        await SetupDefaultCredential();

        var tonie = new CreativeTonieDto
        {
            Id = "tonie1",
            HouseholdId = "household1",
            Name = "Test Tonie",
            ImageUrl = "http://example.com/image.jpg",
            SecondsPresent = 1800,
            SecondsRemaining = 3600,
            ChaptersPresent = 1,
            ChaptersRemaining = 14,
            Transcoding = false,
            Live = true,
            Private = false,
            Chapters = new List<ChapterDto>
            {
                new ChapterDto { Id = "chapter1", Title = "Chapter 1", Seconds = 60, File = "file1", Transcoding = false }
            }
        };

        _mockBoxieClient.Setup(x => x.GetCreativeTonieDetailsAsync(
            It.IsAny<string>(), It.IsAny<string>(), "household1", "tonie1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(tonie);

        // Act
        var result = await _service.DeleteChapterAsync(_testUserId, "household1", "tonie1", "nonexistent");

        // Assert
        result.Should().BeFalse();
        _mockBoxieClient.Verify(x => x.PatchCreativeTonieAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
            It.IsAny<string>(), It.IsAny<List<ChapterDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Note: LastAuthenticated update test removed because service properly disposes DbContext
    // This is tested in integration tests where we can verify the database state

    private async Task<TonieCredential> SetupDefaultCredential()
    {
        var credential = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "test@example.com",
            EncryptedPassword = "encrypted-password",
            DisplayName = "Test Account",
            IsDefault = true,
            LastAuthenticated = DateTimeOffset.UtcNow.AddHours(-1),
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        _dbContext.TonieCredentials.Add(credential);
        await _dbContext.SaveChangesAsync();

        _mockEncryption.Setup(x => x.Unprotect("encrypted-password"))
            .Returns("plain-password");

        return credential;
    }

    private CreativeTonieDto CreateTestTonie(string id, string name, string householdId)
    {
        return new CreativeTonieDto
        {
            Id = id,
            HouseholdId = householdId,
            Name = name,
            ImageUrl = $"http://example.com/{id}.jpg",
            SecondsPresent = 1800,
            SecondsRemaining = 3600,
            ChaptersPresent = 5,
            ChaptersRemaining = 10,
            Transcoding = false,
            Live = true,
            Private = false
        };
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
