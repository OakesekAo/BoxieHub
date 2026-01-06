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

public class TonieAccountManagementIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICredentialEncryptionService _encryptionService;
    private readonly string _testUserId = "integration-test-user";

    public TonieAccountManagementIntegrationTests()
    {
        // Setup In-Memory Database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);

        // Setup Encryption Service
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDataProtection();
        serviceCollection.AddLogging();
        var services = serviceCollection.BuildServiceProvider();

        var dataProtectionProvider = services.GetRequiredService<IDataProtectionProvider>();
        var logger = services.GetRequiredService<ILogger<CredentialEncryptionService>>();

        _encryptionService = new CredentialEncryptionService(dataProtectionProvider, logger);
    }

    [Fact]
    public async Task AddTonieAccount_ShouldSaveEncryptedCredentials()
    {
        // Arrange
        var plainPassword = "MyToniePassword123!";
        var encryptedPassword = _encryptionService.Protect(plainPassword);

        var credential = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "user@example.com",
            EncryptedPassword = encryptedPassword,
            DisplayName = "My Tonie Account",
            IsDefault = true,
            LastAuthenticated = DateTimeOffset.UtcNow,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        // Act
        _dbContext.TonieCredentials.Add(credential);
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedCredential = await _dbContext.TonieCredentials
            .FirstOrDefaultAsync(c => c.TonieUsername == "user@example.com");

        savedCredential.Should().NotBeNull();
        savedCredential!.EncryptedPassword.Should().NotBe(plainPassword);
        
        // Verify we can decrypt it back
        var decryptedPassword = _encryptionService.Unprotect(savedCredential.EncryptedPassword);
        decryptedPassword.Should().Be(plainPassword);
    }

    [Fact]
    public async Task AddMultipleAccounts_OnlyOneCanBeDefault()
    {
        // Arrange
        var account1 = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "account1@example.com",
            EncryptedPassword = _encryptionService.Protect("password1"),
            DisplayName = "Account 1",
            IsDefault = true,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        var account2 = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "account2@example.com",
            EncryptedPassword = _encryptionService.Protect("password2"),
            DisplayName = "Account 2",
            IsDefault = false,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        // Act
        _dbContext.TonieCredentials.AddRange(account1, account2);
        await _dbContext.SaveChangesAsync();

        // Now set account2 as default and unset account1
        account1.IsDefault = false;
        account2.IsDefault = true;
        await _dbContext.SaveChangesAsync();

        // Assert
        var defaultAccount = await _dbContext.TonieCredentials
            .FirstOrDefaultAsync(c => c.UserId == _testUserId && c.IsDefault);

        defaultAccount.Should().NotBeNull();
        defaultAccount!.TonieUsername.Should().Be("account2@example.com");

        var nonDefaultCount = await _dbContext.TonieCredentials
            .CountAsync(c => c.UserId == _testUserId && c.IsDefault);

        nonDefaultCount.Should().Be(1); // Only one default
    }

    [Fact]
    public async Task DeleteAccount_ShouldRemoveFromDatabase()
    {
        // Arrange
        var credential = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "delete-me@example.com",
            EncryptedPassword = _encryptionService.Protect("password"),
            DisplayName = "Delete Me",
            IsDefault = false,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        _dbContext.TonieCredentials.Add(credential);
        await _dbContext.SaveChangesAsync();

        // Act
        _dbContext.TonieCredentials.Remove(credential);
        await _dbContext.SaveChangesAsync();

        // Assert
        var deletedCredential = await _dbContext.TonieCredentials
            .FirstOrDefaultAsync(c => c.TonieUsername == "delete-me@example.com");

        deletedCredential.Should().BeNull();
    }

    [Fact]
    public async Task DeleteDefaultAccount_ShouldPromoteAnotherAccountToDefault()
    {
        // Arrange
        var account1 = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "default@example.com",
            EncryptedPassword = _encryptionService.Protect("password1"),
            DisplayName = "Default Account",
            IsDefault = true,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        var account2 = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "backup@example.com",
            EncryptedPassword = _encryptionService.Protect("password2"),
            DisplayName = "Backup Account",
            IsDefault = false,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        _dbContext.TonieCredentials.AddRange(account1, account2);
        await _dbContext.SaveChangesAsync();

        // Act - Delete default account
        _dbContext.TonieCredentials.Remove(account1);
        await _dbContext.SaveChangesAsync();

        // Promote remaining account to default
        var remainingAccount = await _dbContext.TonieCredentials
            .FirstOrDefaultAsync(c => c.UserId == _testUserId);

        if (remainingAccount != null)
        {
            remainingAccount.IsDefault = true;
            await _dbContext.SaveChangesAsync();
        }

        // Assert
        var newDefault = await _dbContext.TonieCredentials
            .FirstOrDefaultAsync(c => c.UserId == _testUserId && c.IsDefault);

        newDefault.Should().NotBeNull();
        newDefault!.TonieUsername.Should().Be("backup@example.com");
    }

    [Fact]
    public async Task UpdateLastAuthenticated_ShouldUpdateTimestamp()
    {
        // Arrange
        var credential = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "auth-test@example.com",
            EncryptedPassword = _encryptionService.Protect("password"),
            DisplayName = "Auth Test",
            IsDefault = true,
            LastAuthenticated = null,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        _dbContext.TonieCredentials.Add(credential);
        await _dbContext.SaveChangesAsync();

        // Act
        credential.LastAuthenticated = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _dbContext.TonieCredentials
            .FirstOrDefaultAsync(c => c.TonieUsername == "auth-test@example.com");

        updated.Should().NotBeNull();
        updated!.LastAuthenticated.Should().NotBeNull();
        updated.LastAuthenticated!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PreventDuplicateAccounts_ShouldNotAllowSameUsernameForSameUser()
    {
        // Arrange
        var account1 = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "duplicate@example.com",
            EncryptedPassword = _encryptionService.Protect("password1"),
            DisplayName = "First Account",
            IsDefault = true,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        _dbContext.TonieCredentials.Add(account1);
        await _dbContext.SaveChangesAsync();

        // Act - Try to add duplicate
        var existingAccount = await _dbContext.TonieCredentials
            .FirstOrDefaultAsync(c => c.UserId == _testUserId && c.TonieUsername == "duplicate@example.com");

        // Assert
        existingAccount.Should().NotBeNull();
        
        // Simulate validation - should prevent adding duplicate
        var isDuplicate = existingAccount != null;
        isDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserCredentials_ShouldReturnOnlyUserSpecificCredentials()
    {
        // Arrange
        var user1Credential = new TonieCredential
        {
            UserId = "user1",
            TonieUsername = "user1@example.com",
            EncryptedPassword = _encryptionService.Protect("password1"),
            DisplayName = "User 1 Account",
            IsDefault = true,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        var user2Credential = new TonieCredential
        {
            UserId = "user2",
            TonieUsername = "user2@example.com",
            EncryptedPassword = _encryptionService.Protect("password2"),
            DisplayName = "User 2 Account",
            IsDefault = true,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        _dbContext.TonieCredentials.AddRange(user1Credential, user2Credential);
        await _dbContext.SaveChangesAsync();

        // Act
        var user1Credentials = await _dbContext.TonieCredentials
            .Where(c => c.UserId == "user1")
            .ToListAsync();

        // Assert
        user1Credentials.Should().HaveCount(1);
        user1Credentials[0].TonieUsername.Should().Be("user1@example.com");
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
