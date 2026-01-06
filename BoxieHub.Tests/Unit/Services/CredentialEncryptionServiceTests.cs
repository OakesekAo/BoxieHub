using BoxieHub.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BoxieHub.Tests.Unit.Services;

public class CredentialEncryptionServiceTests
{
    private readonly ICredentialEncryptionService _encryptionService;

    public CredentialEncryptionServiceTests()
    {
        // Setup Data Protection with in-memory key storage for testing
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDataProtection();
        serviceCollection.AddLogging();
        var services = serviceCollection.BuildServiceProvider();

        var dataProtectionProvider = services.GetRequiredService<IDataProtectionProvider>();
        var logger = services.GetRequiredService<ILogger<CredentialEncryptionService>>();

        _encryptionService = new CredentialEncryptionService(dataProtectionProvider, logger);
    }

    [Fact]
    public void Protect_ShouldEncryptPlaintext()
    {
        // Arrange
        var plaintext = "MySecretPassword123!";

        // Act
        var encrypted = _encryptionService.Protect(plaintext);

        // Assert
        encrypted.Should().NotBeNullOrEmpty();
        encrypted.Should().NotBe(plaintext);
    }

    [Fact]
    public void Unprotect_ShouldDecryptToOriginalPlaintext()
    {
        // Arrange
        var plaintext = "MySecretPassword123!";
        var encrypted = _encryptionService.Protect(plaintext);

        // Act
        var decrypted = _encryptionService.Unprotect(encrypted);

        // Assert
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Protect_ThenUnprotect_ShouldReturnOriginalValue()
    {
        // Arrange
        var originalPassword = "TestPassword456@";

        // Act
        var encrypted = _encryptionService.Protect(originalPassword);
        var decrypted = _encryptionService.Unprotect(encrypted);

        // Assert
        decrypted.Should().Be(originalPassword);
    }

    [Fact]
    public void Protect_ShouldProduceDifferentCiphertextForSameInput()
    {
        // Arrange
        var plaintext = "SamePassword";

        // Act
        var encrypted1 = _encryptionService.Protect(plaintext);
        var encrypted2 = _encryptionService.Protect(plaintext);

        // Assert - Due to random IVs, encrypted values should differ
        encrypted1.Should().NotBe(encrypted2);
        
        // But both should decrypt to same value
        _encryptionService.Unprotect(encrypted1).Should().Be(plaintext);
        _encryptionService.Unprotect(encrypted2).Should().Be(plaintext);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Protect_WithNullOrEmptyInput_ShouldThrowArgumentException(string? input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _encryptionService.Protect(input!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Unprotect_WithNullOrEmptyInput_ShouldThrowArgumentException(string? input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _encryptionService.Unprotect(input!));
    }

    [Fact]
    public void Unprotect_WithInvalidCiphertext_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidCiphertext = "ThisIsNotValidEncryptedData";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _encryptionService.Unprotect(invalidCiphertext));
    }
}
