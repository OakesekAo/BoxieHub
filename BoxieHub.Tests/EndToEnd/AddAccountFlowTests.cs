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
using System.Security.Claims;
using Xunit;

namespace BoxieHub.Tests.EndToEnd;

public class AddAccountFlowTests : TestContext
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Mock<IBoxieAuthService> _authServiceMock;
    private readonly ICredentialEncryptionService _encryptionService;
    private readonly string _testUserId = "e2e-test-user";

    public AddAccountFlowTests()
    {
        // Setup In-Memory Database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        Services.AddSingleton(_dbContext);

        // Setup Encryption Service
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDataProtection();
        serviceCollection.AddLogging();
        var dataProtectionServices = serviceCollection.BuildServiceProvider();

        var dataProtectionProvider = dataProtectionServices.GetRequiredService<IDataProtectionProvider>();
        var logger = dataProtectionServices.GetRequiredService<ILogger<CredentialEncryptionService>>();

        _encryptionService = new CredentialEncryptionService(dataProtectionProvider, logger);
        Services.AddSingleton(_encryptionService);

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

        // Mock Tonie Auth Service
        _authServiceMock = new Mock<IBoxieAuthService>();
        _authServiceMock.Setup(x => x.GetAccessTokenAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("mock-access-token");

        Services.AddSingleton(_authServiceMock.Object);

        // Add Navigation Manager
        Services.AddSingleton<NavigationManager>(new MockNavigationManager());
    }

    [Fact]
    public void AddAccount_Form_ShouldRenderWithAllFields()
    {
        // Act
        var cut = RenderComponent<AddAccount>();

        // Assert
        cut.Find("input[placeholder='e.g., Family Account']").Should().NotBeNull();
        cut.Find("input[placeholder='your@email.com']").Should().NotBeNull();
        cut.Find("input[type='password']").Should().NotBeNull();
        cut.Find("input[type='checkbox']#defaultCheck").Should().NotBeNull();
        cut.Find("button[type='submit']").Should().NotBeNull();
    }

    [Fact]
    public async Task AddAccount_WithValidCredentials_ShouldSaveToDatabase()
    {
        // Arrange
        var cut = RenderComponent<AddAccount>();

        // Act
        cut.Find("input[placeholder='e.g., Family Account']").Change("My Test Account");
        cut.Find("input[placeholder='your@email.com']").Change("test@example.com");
        cut.Find("input[type='password']").Change("ValidPassword123");

        // Wait for form to process
        await cut.InvokeAsync(async () =>
        {
            var form = cut.Find("form");
            await form.SubmitAsync();
        });

        // Give time for async operations
        await Task.Delay(500);

        // Assert
        var savedCredential = await _dbContext.TonieCredentials
            .FirstOrDefaultAsync(c => c.TonieUsername == "test@example.com");

        savedCredential.Should().NotBeNull();
        savedCredential!.DisplayName.Should().Be("My Test Account");
        savedCredential.UserId.Should().Be(_testUserId);
        savedCredential.IsDefault.Should().BeTrue();
        savedCredential.EncryptedPassword.Should().NotBe("ValidPassword123"); // Should be encrypted
    }

    [Fact]
    public async Task AddAccount_WithInvalidEmail_ShouldShowValidationError()
    {
        // Arrange
        var cut = RenderComponent<AddAccount>();

        // Act
        cut.Find("input[placeholder='your@email.com']").Change("invalid-email");
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

    [Fact]
    public async Task AddAccount_WithShortPassword_ShouldShowValidationError()
    {
        // Arrange
        var cut = RenderComponent<AddAccount>();

        // Act
        cut.Find("input[placeholder='your@email.com']").Change("test@example.com");
        cut.Find("input[type='password']").Change("123"); // Too short

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
    public async Task AddAccount_DuplicateUsername_ShouldShowError()
    {
        // Arrange
        // Add existing account
        var existingCredential = new BoxieHub.Models.TonieCredential
        {
            UserId = _testUserId,
            TonieUsername = "existing@example.com",
            EncryptedPassword = _encryptionService.Protect("password"),
            DisplayName = "Existing Account",
            IsDefault = true,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        _dbContext.TonieCredentials.Add(existingCredential);
        await _dbContext.SaveChangesAsync();

        var cut = RenderComponent<AddAccount>();

        // Act - Try to add duplicate
        cut.Find("input[placeholder='your@email.com']").Change("existing@example.com");
        cut.Find("input[type='password']").Change("AnotherPassword123");

        await cut.InvokeAsync(async () =>
        {
            var form = cut.Find("form");
            await form.SubmitAsync();
        });

        // Assert
        cut.WaitForAssertion(() =>
        {
            var errorAlert = cut.Find(".alert-danger");
            errorAlert.TextContent.Should().Contain("already linked");
        }, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task AddAccount_WhenAuthenticationFails_ShouldShowError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.GetAccessTokenAsync(
            It.IsAny<string>(),
            "WrongPassword",
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Authentication failed"));

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
            errorAlert.TextContent.Should().Contain("Authentication failed");
        }, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void AddAccount_ShouldShowSubmitButtonWithLoadingState()
    {
        // Arrange
        var cut = RenderComponent<AddAccount>();

        // Act
        var submitButton = cut.Find("button[type='submit']");

        // Assert
        submitButton.TextContent.Should().Contain("Save Account");
        submitButton.QuerySelector("i.bi-check-lg").Should().NotBeNull();
    }

    public new void Dispose()
    {
        _dbContext?.Dispose();
        base.Dispose();
    }

    // Mock Navigation Manager for testing
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
