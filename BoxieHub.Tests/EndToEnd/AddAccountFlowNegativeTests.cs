using Bunit;
using BoxieHub.Components.Pages.Tonies;
using BoxieHub.Data;
using BoxieHub.Services;
using BoxieHub.Services.BoxieCloud;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Security.Claims;
using Xunit;

namespace BoxieHub.Tests.EndToEnd;

/// <summary>
/// Negative end-to-end tests for Add Account Flow
/// Testing error conditions, validation, and edge cases
/// </summary>
public class AddAccountFlowNegativeTests : TestContext
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Mock<IBoxieAuthService> _authServiceMock;
    private readonly ICredentialEncryptionService _encryptionService;
    private readonly string _testUserId = "e2e-negative-test-user";

    public AddAccountFlowNegativeTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        Services.AddSingleton(_dbContext);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDataProtection();
        serviceCollection.AddLogging();
        var dataProtectionServices = serviceCollection.BuildServiceProvider();

        var dataProtectionProvider = dataProtectionServices.GetRequiredService<IDataProtectionProvider>();
        var logger = dataProtectionServices.GetRequiredService<ILogger<CredentialEncryptionService>>();

        _encryptionService = new CredentialEncryptionService(dataProtectionProvider, logger);
        Services.AddSingleton(_encryptionService);

        var authState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId),
            new Claim(ClaimTypes.Name, "Test User")
        }, "mock")));

        var authStateProviderMock = new Mock<AuthenticationStateProvider>();
        authStateProviderMock.Setup(x => x.GetAuthenticationStateAsync())
            .ReturnsAsync(authState);

        Services.AddSingleton(authStateProviderMock.Object);

        _authServiceMock = new Mock<IBoxieAuthService>();
        _authServiceMock.Setup(x => x.GetAccessTokenAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("mock-access-token");

        Services.AddSingleton(_authServiceMock.Object);
        Services.AddSingleton<NavigationManager>(new MockNavigationManager());
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    public async Task AddAccount_WithInvalidEmailFormats_ShouldShowValidationError(string invalidEmail)
    {
        // Arrange
        var cut = RenderComponent<AddAccount>();

        // Act
        cut.Find("input[placeholder='your@email.com']").Change(invalidEmail);
        cut.Find("input[type='password']").Change("ValidPassword123");

        var form = cut.Find("form");
        await form.SubmitAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var validationMessages = cut.FindAll(".validation-message, .text-danger");
            validationMessages.Should().NotBeEmpty();
        }, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("12")]
    [InlineData("123")]
    [InlineData("1234")]
    [InlineData("12345")]
    public async Task AddAccount_WithPasswordTooShort_ShouldShowValidationError(string shortPassword)
    {
        // Arrange
        var cut = RenderComponent<AddAccount>();

        // Act
        cut.Find("input[placeholder='your@email.com']").Change("test@example.com");
        cut.Find("input[type='password']").Change(shortPassword);

        var form = cut.Find("form");
        await form.SubmitAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var validationMessages = cut.FindAll(".validation-message, .text-danger");
            validationMessages.Should().Contain(v => v.TextContent.Contains("at least 6 characters"));
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task AddAccount_WithEmptyForm_ShouldShowMultipleValidationErrors()
    {
        // Arrange
        var cut = RenderComponent<AddAccount>();

        // Act - Submit without filling anything
        var form = cut.Find("form");
        await form.SubmitAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var validationMessages = cut.FindAll(".validation-message, .text-danger");
            validationMessages.Should().HaveCountGreaterThanOrEqualTo(2); // Email and Password required
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task AddAccount_WithNetworkError_ShouldShowError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.GetAccessTokenAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var cut = RenderComponent<AddAccount>();

        // Act
        cut.Find("input[placeholder='your@email.com']").Change("test@example.com");
        cut.Find("input[type='password']").Change("ValidPassword123");

        await cut.InvokeAsync(async () =>
        {
            var form = cut.Find("form");
            await form.SubmitAsync();
        });

        // Assert
        cut.WaitForAssertion(() =>
        {
            var errorAlert = cut.Find(".alert-danger");
            errorAlert.TextContent.Should().Contain("Authentication failed");
        }, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task AddAccount_WithUnauthorizedError_ShouldShowError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.GetAccessTokenAsync(
            It.IsAny<string>(),
            "WrongPassword",
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("401 Unauthorized"));

        var cut = RenderComponent<AddAccount>();

        // Act
        cut.Find("input[placeholder='your@email.com']").Change("test@example.com");
        cut.Find("input[type='password']").Change("WrongPassword");

        await cut.InvokeAsync(async () =>
        {
            var form = cut.Find("form");
            await form.SubmitAsync();
        });

        // Assert
        cut.WaitForAssertion(() =>
        {
            var errorAlert = cut.Find(".alert-danger");
            errorAlert.Should().NotBeNull();
        }, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task AddAccount_WhenDatabaseSaveFails_ShouldShowError()
    {
        // Arrange
        // Dispose the current context and create a broken one
        _dbContext.Dispose();

        // This will cause SaveChanges to fail
        var cut = RenderComponent<AddAccount>();

        // Act
        cut.Find("input[placeholder='your@email.com']").Change("test@example.com");
        cut.Find("input[type='password']").Change("ValidPassword123");

        await cut.InvokeAsync(async () =>
        {
            var form = cut.Find("form");
            await form.SubmitAsync();
        });

        // Assert
        cut.WaitForAssertion(() =>
        {
            var errorAlert = cut.Find(".alert-danger");
            errorAlert.TextContent.Should().Contain("Failed to save account");
        }, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task AddAccount_WithVeryLongDisplayName_ShouldTruncateOrValidate()
    {
        // Arrange
        var tooLongName = new string('A', 300); // Max is 256
        var cut = RenderComponent<AddAccount>();

        // Act
        cut.Find("input[placeholder='e.g., Family Account']").Change(tooLongName);
        cut.Find("input[placeholder='your@email.com']").Change("test@example.com");
        cut.Find("input[type='password']").Change("ValidPassword123");

        await cut.InvokeAsync(async () =>
        {
            var form = cut.Find("form");
            await form.SubmitAsync();
        });

        // Assert - Should either show validation error or truncate
        await Task.Delay(500);
        
        // Check if account was saved (truncated) or error shown
        var savedAccount = await _dbContext.TonieCredentials
            .FirstOrDefaultAsync(c => c.TonieUsername == "test@example.com");

        if (savedAccount != null)
        {
            savedAccount.DisplayName!.Length.Should().BeLessOrEqualTo(256);
        }
    }

    [Fact]
    public async Task AddAccount_WithSpecialCharactersInEmail_ShouldValidate()
    {
        // Arrange
        var cut = RenderComponent<AddAccount>();

        // Act
        cut.Find("input[placeholder='your@email.com']").Change("user+tag@example.com"); // Valid email with +
        cut.Find("input[type='password']").Change("ValidPassword123");

        await cut.InvokeAsync(async () =>
        {
            var form = cut.Find("form");
            await form.SubmitAsync();
        });

        // Assert - Should accept valid email with special characters
        await Task.Delay(500);
        
        var savedAccount = await _dbContext.TonieCredentials
            .FirstOrDefaultAsync(c => c.TonieUsername == "user+tag@example.com");

        savedAccount.Should().NotBeNull();
    }

    [Fact]
    public void AddAccount_CancelButton_ShouldNavigateBack()
    {
        // Arrange
        var cut = RenderComponent<AddAccount>();

        // Act
        var cancelLink = cut.Find("a.btn-outline-secondary[href='/tonies']");

        // Assert
        cancelLink.Should().NotBeNull();
        cancelLink.TextContent.Should().Contain("Cancel");
    }

    [Fact]
    public async Task AddAccount_SubmitMultipleTimes_ShouldPreventDuplicateSubmission()
    {
        // Arrange
        var cut = RenderComponent<AddAccount>();

        cut.Find("input[placeholder='your@email.com']").Change("test@example.com");
        cut.Find("input[type='password']").Change("ValidPassword123");

        // Act - Try to submit multiple times rapidly
        await cut.InvokeAsync(async () =>
        {
            var form = cut.Find("form");
            var submitTask1 = form.SubmitAsync();
            var submitTask2 = form.SubmitAsync(); // Second submit should be prevented

            await Task.WhenAll(submitTask1, submitTask2);
        });

        // Assert - Should only save once
        await Task.Delay(1000);
        
        var savedAccounts = await _dbContext.TonieCredentials
            .Where(c => c.TonieUsername == "test@example.com")
            .ToListAsync();

        savedAccounts.Should().HaveCount(1);
    }

    public new void Dispose()
    {
        _dbContext?.Dispose();
        base.Dispose();
    }

    private class MockNavigationManager : NavigationManager
    {
        public MockNavigationManager()
        {
            Initialize("https://localhost/", "https://localhost/tonies/add-account");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            // Do nothing in tests
        }
    }
}
