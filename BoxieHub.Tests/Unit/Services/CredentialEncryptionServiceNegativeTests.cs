using BoxieHub.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BoxieHub.Tests.Unit.Services;

/// <summary>
/// Negative test cases for CredentialEncryptionService
/// Testing error conditions, edge cases, and boundary scenarios
/// </summary>
public class CredentialEncryptionServiceNegativeTests
{
    private readonly ICredentialEncryptionService _encryptionService;

    public CredentialEncryptionServiceNegativeTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDataProtection();
        serviceCollection.AddLogging();
        var services = serviceCollection.BuildServiceProvider();

        var dataProtectionProvider = services.GetRequiredService<IDataProtectionProvider>();
        var logger = services.GetRequiredService<ILogger<CredentialEncryptionService>>();

        _encryptionService = new CredentialEncryptionService(dataProtectionProvider, logger);
    }

    [Fact]
    public void Protect_WithWhitespaceOnly_ShouldSucceed()
    {
        // Arrange
        var whitespace = "   ";

        // Act
        var encrypted = _encryptionService.Protect(whitespace);
        var decrypted = _encryptionService.Unprotect(encrypted);

        // Assert - Should treat whitespace as valid data
        encrypted.Should().NotBeNullOrEmpty();
        decrypted.Should().Be(whitespace);
    }

    [Theory]
    [InlineData("NotBase64!@#$")]
    [InlineData("Invalid==Base64==")]
    [InlineData("12345")]
    [InlineData("abc")]
    public void Unprotect_WithMalformedCiphertext_ShouldThrowInvalidOperationException(string malformed)
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _encryptionService.Unprotect(malformed));
    }

    [Fact]
    public void Unprotect_WithValidBase64ButNotEncrypted_ShouldThrowInvalidOperationException()
    {
        // Arrange - Valid Base64 but not encrypted data
        var validBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Just plain text"));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _encryptionService.Unprotect(validBase64));
    }

    [Fact]
    public void Unprotect_WithTruncatedCiphertext_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var password = "ValidPassword123";
        var encrypted = _encryptionService.Protect(password);
        
        // Truncate the encrypted string (simulate data corruption)
        var truncated = encrypted.Substring(0, encrypted.Length / 2);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _encryptionService.Unprotect(truncated));
    }

    [Fact]
    public void Unprotect_WithModifiedCiphertext_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var password = "ValidPassword123";
        var encrypted = _encryptionService.Protect(password);
        
        // Modify one character (simulate data tampering)
        var modified = encrypted.Length > 10 
            ? encrypted.Substring(0, 10) + "X" + encrypted.Substring(11)
            : encrypted + "X";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _encryptionService.Unprotect(modified));
    }

    [Fact]
    public void Protect_WithVeryLongPassword_ShouldSucceed()
    {
        // Arrange - 10,000 character password
        var longPassword = new string('A', 10000);

        // Act
        var encrypted = _encryptionService.Protect(longPassword);
        var decrypted = _encryptionService.Unprotect(encrypted);

        // Assert
        encrypted.Should().NotBeNullOrEmpty();
        decrypted.Should().Be(longPassword);
    }

    [Theory]
    [InlineData("a")] // 1 character
    [InlineData("ab")] // 2 characters
    [InlineData("abc")] // 3 characters
    public void Protect_WithVeryShortPassword_ShouldSucceed(string shortPassword)
    {
        // Act
        var encrypted = _encryptionService.Protect(shortPassword);
        var decrypted = _encryptionService.Unprotect(encrypted);

        // Assert
        decrypted.Should().Be(shortPassword);
    }

    [Theory]
    [InlineData("Password123!@#")]
    [InlineData("??????123")] // Cyrillic
    [InlineData("??123")] // Chinese
    [InlineData("?????123")] // Japanese
    [InlineData("???????")] // Emojis
    public void Protect_WithSpecialCharacters_ShouldSucceed(string specialPassword)
    {
        // Act
        var encrypted = _encryptionService.Protect(specialPassword);
        var decrypted = _encryptionService.Unprotect(encrypted);

        // Assert
        decrypted.Should().Be(specialPassword);
    }

    [Fact]
    public void Unprotect_AfterMultipleEncryptions_ShouldReturnCorrectValue()
    {
        // Arrange
        var password = "TestPassword";
        var encrypted1 = _encryptionService.Protect(password);
        var encrypted2 = _encryptionService.Protect(password);
        var encrypted3 = _encryptionService.Protect(password);

        // Act & Assert - All should decrypt correctly
        _encryptionService.Unprotect(encrypted1).Should().Be(password);
        _encryptionService.Unprotect(encrypted2).Should().Be(password);
        _encryptionService.Unprotect(encrypted3).Should().Be(password);
    }
}
