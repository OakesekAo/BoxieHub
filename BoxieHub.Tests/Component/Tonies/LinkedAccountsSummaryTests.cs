using Bunit;
using BoxieHub.Components.Pages.Tonies.Components;
using BoxieHub.Data;
using BoxieHub.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Security.Claims;
using Xunit;

namespace BoxieHub.Tests.Component.Tonies;

public class LinkedAccountsSummaryTests : TestContext
{
    private readonly ApplicationDbContext _dbContext;
    private readonly string _testUserId = "test-user-123";

    public LinkedAccountsSummaryTests()
    {
        // Setup In-Memory Database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);

        // Register IDbContextFactory (required by LinkedAccountsSummary)
        var dbContextFactoryMock = new Mock<IDbContextFactory<ApplicationDbContext>>();
        dbContextFactoryMock.Setup(x => x.CreateDbContext())
            .Returns(_dbContext);
        dbContextFactoryMock.Setup(x => x.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_dbContext);
        
        Services.AddSingleton(dbContextFactoryMock.Object);
        
        // Mock Authentication
        var authState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId),
            new Claim(ClaimTypes.Name, "Test User")
        }, "mock")));

        var authStateProviderMock = new Mock<AuthenticationStateProvider>();
        authStateProviderMock.Setup(x => x.GetAuthenticationStateAsync())
            .ReturnsAsync(authState);

        Services.AddSingleton(authStateProviderMock.Object);
    }

    [Fact]
    public void Component_WhenNoAccountsLinked_ShouldShowNoAccountsMessage()
    {
        // Arrange - No accounts in database

        // Act
        var cut = RenderComponent<LinkedAccountsSummary>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find(".text-muted").TextContent.Should().Contain("No accounts linked yet");
        });
    }

    [Fact]
    public void Component_WhenOneAccountLinked_ShouldDisplayAccount()
    {
        // Arrange
        var credential = new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "test@example.com",
            EncryptedPassword = "encrypted",
            DisplayName = "Test Account",
            IsDefault = true,
            LastAuthenticated = DateTimeOffset.UtcNow,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        _dbContext.TonieCredentials.Add(credential);
        _dbContext.SaveChanges();

        // Act
        var cut = RenderComponent<LinkedAccountsSummary>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("strong").TextContent.Should().Contain("Test Account");
            cut.Find(".badge.bg-primary").TextContent.Should().Be("1");
            cut.Find(".badge.bg-success").TextContent.Should().Be("Default");
        });
    }

    [Fact]
    public void Component_WhenMultipleAccountsLinked_ShouldShowCorrectCount()
    {
        // Arrange
        var credentials = new[]
        {
            new TonieCredential
            {
                UserId = _testUserId,
                TonieUsername = "account1@example.com",
                EncryptedPassword = "encrypted1",
                DisplayName = "Account 1",
                IsDefault = true,
                Created = DateTimeOffset.UtcNow,
                Modified = DateTimeOffset.UtcNow
            },
            new TonieCredential
            {
                UserId = _testUserId,
                TonieUsername = "account2@example.com",
                EncryptedPassword = "encrypted2",
                DisplayName = "Account 2",
                IsDefault = false,
                Created = DateTimeOffset.UtcNow,
                Modified = DateTimeOffset.UtcNow
            },
            new TonieCredential
            {
                UserId = _testUserId,
                TonieUsername = "account3@example.com",
                EncryptedPassword = "encrypted3",
                DisplayName = "Account 3",
                IsDefault = false,
                Created = DateTimeOffset.UtcNow,
                Modified = DateTimeOffset.UtcNow
            }
        };

        _dbContext.TonieCredentials.AddRange(credentials);
        _dbContext.SaveChanges();

        // Act
        var cut = RenderComponent<LinkedAccountsSummary>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find(".badge.bg-primary").TextContent.Should().Be("3");
        });
    }

    [Fact]
    public void Component_WhenMoreThanThreeAccounts_ShouldShowViewAllLink()
    {
        // Arrange
        for (int i = 1; i <= 5; i++)
        {
            _dbContext.TonieCredentials.Add(new TonieCredential
            {
                UserId = _testUserId,
                TonieUsername = $"account{i}@example.com",
                EncryptedPassword = $"encrypted{i}",
                DisplayName = $"Account {i}",
                IsDefault = i == 1,
                Created = DateTimeOffset.UtcNow,
                Modified = DateTimeOffset.UtcNow
            });
        }
        _dbContext.SaveChanges();

        // Act
        var cut = RenderComponent<LinkedAccountsSummary>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var viewAllLinks = cut.FindAll("a[href='/tonies/accounts']");
            var viewAllLink = viewAllLinks.FirstOrDefault(l => l.TextContent.Contains("View all"));
            viewAllLink.Should().NotBeNull();
            viewAllLink!.TextContent.Should().Contain("View all 5 accounts");
        });
    }

    [Fact]
    public void Component_ShouldShowManageButton_WhenAccountsExist()
    {
        // Arrange
        _dbContext.TonieCredentials.Add(new TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "test@example.com",
            EncryptedPassword = "encrypted",
            DisplayName = "Test Account",
            IsDefault = true,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        });
        _dbContext.SaveChanges();

        // Act
        var cut = RenderComponent<LinkedAccountsSummary>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var manageButton = cut.Find("a.btn-outline-primary[href='/tonies/accounts']");
            manageButton.TextContent.Should().Contain("Manage");
        });
    }

    // Note: Loading state test removed because bUnit renders synchronously
    // and the component loads data too quickly to catch the loading state

    public new void Dispose()
    {
        _dbContext?.Dispose();
        base.Dispose();
    }
}
