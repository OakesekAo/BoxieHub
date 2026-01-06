using BoxieHub.Models;
using FluentAssertions;
using Xunit;

namespace BoxieHub.Tests.Unit.Models;

public class TonieCredentialTests
{
    [Fact]
    public void TonieCredential_ShouldInitializeWithDefaultValues()
    {
        // Act
        var credential = new TonieCredential();

        // Assert
        credential.Id.Should().Be(0);
        credential.IsDefault.Should().BeFalse();
        credential.LastAuthenticated.Should().BeNull();
    }

    [Fact]
    public void TonieCredential_Created_ShouldConvertToUtc()
    {
        // Arrange
        var credential = new TonieCredential();
        var localTime = new DateTimeOffset(2025, 1, 5, 14, 30, 0, TimeSpan.FromHours(-5)); // EST

        // Act
        credential.Created = localTime;

        // Assert
        credential.Created.Offset.Should().Be(TimeSpan.Zero); // UTC
        credential.Created.Hour.Should().Be(19); // 14:30 EST = 19:30 UTC
    }

    [Fact]
    public void TonieCredential_Modified_ShouldConvertToUtc()
    {
        // Arrange
        var credential = new TonieCredential();
        var localTime = new DateTimeOffset(2025, 1, 5, 10, 0, 0, TimeSpan.FromHours(1)); // CET

        // Act
        credential.Modified = localTime;

        // Assert
        credential.Modified.Offset.Should().Be(TimeSpan.Zero); // UTC
        credential.Modified.Hour.Should().Be(9); // 10:00 CET = 09:00 UTC
    }

    [Fact]
    public void TonieCredential_WithRequiredFields_ShouldBeValid()
    {
        // Act
        var credential = new TonieCredential
        {
            UserId = "user-123",
            TonieUsername = "test@example.com",
            EncryptedPassword = "encrypted-data",
            DisplayName = "Test Account",
            IsDefault = true,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow
        };

        // Assert
        credential.UserId.Should().Be("user-123");
        credential.TonieUsername.Should().Be("test@example.com");
        credential.EncryptedPassword.Should().Be("encrypted-data");
        credential.DisplayName.Should().Be("Test Account");
        credential.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void TonieCredential_DisplayName_CanBeNull()
    {
        // Act
        var credential = new TonieCredential
        {
            UserId = "user-123",
            TonieUsername = "test@example.com",
            EncryptedPassword = "encrypted-data",
            DisplayName = null
        };

        // Assert
        credential.DisplayName.Should().BeNull();
    }
}
