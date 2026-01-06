using BoxieHub.Data;
using BoxieHub.Models;
using BoxieHub.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BoxieHub.Tests.Integration;

/// <summary>
/// Negative integration tests for Tonie Account Management
/// Testing error conditions, race conditions, and boundary scenarios
/// </summary>
public class TonieAccountManagementNegativeTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICredentialEncryptionService _encryptionService;
    private readonly string _testUserId = "negative-test-user";

    public TonieAccountManagementNegativeTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDataProtection();
        serviceCollection.AddLogging();
        var services = serviceCollection.BuildServiceProvider();

        var dataProtectionProvider = services.GetRequiredService<IDataProtectionProvider>();
        var logger = services.GetRequiredService<ILogger<CredentialEncryptionService>>();

        _encryptionService = new CredentialEncryptionService(dataProtectionProvider, logger);
    }

    [Fact]
    public async Task AddAccount_WithMissingUserId_ShouldNotSave()
    {
        // Arrange
        var credential = new TonieCredential
        {
            UserId = null!, // Missing required field
            TonieUsername = "test@example.com",
            EncryptedPassword = _encryptionService.Protect("password"),
            DisplayName = "Test",
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        // Act & Assert
        _dbContext.TonieCredentials.Add(credential);
        await Assert.ThrowsAsync<DbUpdateException>(async () => await _dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task AddAccount_WithMissingTonieUsername_ShouldNotSave()
    {
        // Arrange
        var credential = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = null!, // Missing required field
            EncryptedPassword = _encryptionService.Protect("password"),
            DisplayName = "Test",
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        // Act & Assert
        _dbContext.TonieCredentials.Add(credential);
        await Assert.ThrowsAsync<DbUpdateException>(async () => await _dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task AddAccount_WithMissingEncryptedPassword_ShouldNotSave()
    {
        // Arrange
        var credential = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "test@example.com",
            EncryptedPassword = null!, // Missing required field
            DisplayName = "Test",
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        // Act & Assert
        _dbContext.TonieCredentials.Add(credential);
        await Assert.ThrowsAsync<DbUpdateException>(async () => await _dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task DeleteAccount_WithInvalidId_ShouldNotThrow()
    {
        // Arrange
        var nonExistentId = 99999;
        var credential = await _dbContext.TonieCredentials.FindAsync(nonExistentId);

        // Act - Trying to remove null should not throw
        if (credential != null)
        {
            _dbContext.TonieCredentials.Remove(credential);
        }

        // Assert - Should complete without exception
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task SetDefault_WhenNoAccountsExist_ShouldNotThrow()
    {
        // Arrange - Empty database

        // Act
        var defaultAccount = await _dbContext.TonieCredentials
            .FirstOrDefaultAsync(c => c.UserId == _testUserId && c.IsDefault);

        // Assert
        defaultAccount.Should().BeNull();
    }

    [Fact]
    public async Task AddAccount_WithVeryLongDisplayName_ShouldStoreData()
    {
        // Arrange - DisplayName has max length of 256
        // Note: InMemory DB doesn't enforce this, but real DB would
        var tooLongName = new string('A', 300);

        var credential = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "test@example.com",
            EncryptedPassword = _encryptionService.Protect("password"),
            DisplayName = tooLongName,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        // Act - InMemory DB will accept this, but we document the expected behavior
        _dbContext.TonieCredentials.Add(credential);
        await _dbContext.SaveChangesAsync();

        // Assert - In production with real DB, this would be truncated or rejected
        var saved = await _dbContext.TonieCredentials.FindAsync(credential.Id);
        saved.Should().NotBeNull();
        // In real SQL Server/PostgreSQL, DisplayName column has MaxLength(256)
    }

    [Fact]
    public async Task AddAccount_WithVeryLongEmail_ShouldStoreData()
    {
        // Arrange - Email has max length of 256
        // Note: InMemory DB doesn't enforce this, but real DB would
        var tooLongEmail = new string('a', 250) + "@example.com";

        var credential = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = tooLongEmail,
            EncryptedPassword = _encryptionService.Protect("password"),
            DisplayName = "Test",
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        // Act - InMemory DB will accept this
        _dbContext.TonieCredentials.Add(credential);
        await _dbContext.SaveChangesAsync();

        // Assert - In production with real DB, this would be rejected
        var saved = await _dbContext.TonieCredentials.FindAsync(credential.Id);
        saved.Should().NotBeNull();
        // In real SQL Server/PostgreSQL, TonieUsername column has MaxLength(256)
    }

    [Fact]
    public async Task GetAccounts_ForNonExistentUser_ShouldReturnEmpty()
    {
        // Arrange
        var nonExistentUserId = "non-existent-user-999";

        // Act
        var credentials = await _dbContext.TonieCredentials
            .Where(c => c.UserId == nonExistentUserId)
            .ToListAsync();

        // Assert
        credentials.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAllAccounts_ForUser_ShouldLeaveOtherUsersIntact()
    {
        // Arrange
        var user1Id = "user1";
        var user2Id = "user2";

        _dbContext.TonieCredentials.AddRange(
            new TonieCredential
            {
                UserId = user1Id,
                TonieUsername = "user1@example.com",
                EncryptedPassword = _encryptionService.Protect("pass1"),
                DisplayName = "User 1",
                Created = DateTimeOffset.UtcNow,
                Modified = DateTimeOffset.UtcNow
            },
            new TonieCredential
            {
                UserId = user2Id,
                TonieUsername = "user2@example.com",
                EncryptedPassword = _encryptionService.Protect("pass2"),
                DisplayName = "User 2",
                Created = DateTimeOffset.UtcNow,
                Modified = DateTimeOffset.UtcNow
            }
        );
        await _dbContext.SaveChangesAsync();

        // Act - Delete user1's accounts
        var user1Accounts = await _dbContext.TonieCredentials
            .Where(c => c.UserId == user1Id)
            .ToListAsync();

        _dbContext.TonieCredentials.RemoveRange(user1Accounts);
        await _dbContext.SaveChangesAsync();

        // Assert - User2's account should still exist
        var user2Accounts = await _dbContext.TonieCredentials
            .Where(c => c.UserId == user2Id)
            .ToListAsync();

        user2Accounts.Should().HaveCount(1);
        user2Accounts[0].TonieUsername.Should().Be("user2@example.com");
    }

    [Fact]
    public async Task UpdateLastAuthenticated_WithFutureDate_ShouldSucceed()
    {
        // Arrange
        var credential = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "test@example.com",
            EncryptedPassword = _encryptionService.Protect("password"),
            DisplayName = "Test",
            LastAuthenticated = DateTimeOffset.UtcNow,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        _dbContext.TonieCredentials.Add(credential);
        await _dbContext.SaveChangesAsync();

        // Act - Set future date (edge case, but should be allowed by DB)
        credential.LastAuthenticated = DateTimeOffset.UtcNow.AddYears(1);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _dbContext.TonieCredentials.FindAsync(credential.Id);
        updated!.LastAuthenticated!.Value.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    // Note: Concurrent update test removed because InMemory DB doesn't support
    // GetDbConnection() and concurrency tokens would need a real database
    // to test properly (SQL Server with rowversion or PostgreSQL with xmin)

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
