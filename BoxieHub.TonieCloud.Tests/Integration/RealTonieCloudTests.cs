using BoxieHub.TonieCloud.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BoxieHub.TonieCloud.Tests.Integration;

/// <summary>
/// Integration tests with REAL Tonie Cloud API
/// These tests require valid credentials from User Secrets
/// 
/// Setup:
///   dotnet user-secrets set "Tonie:Username" "your_email@example.com"
///   dotnet user-secrets set "Tonie:Password" "your_password"
/// 
/// Run with: dotnet test --filter Category=Integration
/// </summary>
[TestFixture]
[Category("Integration")]
[Explicit] // Don't run automatically in CI/CD
public class RealTonieCloudTests
{
    private string _username = null!;
    private string _password = null!;
    
    // Known from previous testing (update if your data changes)
    private const string EXPECTED_HOUSEHOLD_ID = "b7dc99d3-fa51-4431-ae21-2c8a6aceb16a";
    private const string EXPECTED_TONIE_ID = "72EB4F1D500304E0";
    private const int EXPECTED_CHAPTER_COUNT = 29;
    
    private HttpClient _httpClient = null!;
    private IMemoryCache _memoryCache = null!;
    private ITonieAuthService _authService = null!;
    private ITonieCloudClient _tonieClient = null!;
    
    [SetUp]
    public void SetUp()
    {
        // Load credentials from User Secrets with environment variable fallback
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<RealTonieCloudTests>()
            .AddEnvironmentVariables() // Fallback to environment variables
            .Build();
        
        _username = configuration["Tonie:Username"] 
            ?? Environment.GetEnvironmentVariable("TONIE_USERNAME")
            ?? throw new InvalidOperationException(
                "Tonie:Username not found. Setup:\n" +
                "1. User Secrets: dotnet user-secrets set \"Tonie:Username\" \"your_email@example.com\"\n" +
                "2. Or Environment: $env:TONIE_USERNAME = \"your_email@example.com\"");
        
        _password = configuration["Tonie:Password"] 
            ?? Environment.GetEnvironmentVariable("TONIE_PASSWORD")
            ?? throw new InvalidOperationException(
                "Tonie:Password not found. Setup:\n" +
                "1. User Secrets: dotnet user-secrets set \"Tonie:Password\" \"your_password\"\n" +
                "2. Or Environment: $env:TONIE_PASSWORD = \"your_password\"");
        
        // Log that credentials were loaded (without showing them)
        Console.WriteLine($"? Credentials loaded for user: {_username[..Math.Min(5, _username.Length)]}***");
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.tonie.cloud/v2")
        };
        
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        
        var authLogger = new LoggerFactory().CreateLogger<TonieAuthService>();
        _authService = new TonieAuthService(_httpClient, _memoryCache, authLogger);
        
        var clientLogger = new LoggerFactory().CreateLogger<TonieCloudClient>();
        _tonieClient = new TonieCloudClient(_httpClient, _authService, clientLogger);
    }
    
    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
        _memoryCache?.Dispose();
    }
    
    [Test]
    [Order(0)]
    public void Diagnostic_VerifyCredentialsLoaded()
    {
        // This test verifies that credentials are loaded correctly
        Assert.That(_username, Is.Not.Null.And.Not.Empty, "Username should be loaded");
        Assert.That(_password, Is.Not.Null.And.Not.Empty, "Password should be loaded");
        Assert.That(_username, Does.Contain("@"), "Username should be an email");
        
        Console.WriteLine($"? Credentials loaded successfully");
        Console.WriteLine($"  Username: {_username[..Math.Min(10, _username.Length)]}***");
        Console.WriteLine($"  Password: ***{_password[Math.Max(0, _password.Length - 3)..]}");
    }
    
    [Test]
    public async Task RealAPI_Authenticate_Succeeds()
    {
        // Act
        var token = await _authService.GetAccessTokenAsync(_username, _password);
        
        // Assert
        Assert.That(token, Is.Not.Null);
        Assert.That(token, Is.Not.Empty);
        Assert.That(token, Does.StartWith("eyJ")); // JWT tokens start with eyJ
        
        Console.WriteLine($"? Successfully authenticated");
        Console.WriteLine($"  Token (first 20 chars): {token[..Math.Min(20, token.Length)]}...");
    }
    
    [Test]
    public async Task RealAPI_GetHouseholds_ReturnsAndrewHousehold()
    {
        // Act
        var households = await _tonieClient.GetHouseholdsAsync(_username, _password);
        
        // Assert
        Assert.That(households, Is.Not.Null);
        Assert.That(households, Is.Not.Empty);
        Assert.That(households[0].Id, Is.EqualTo(EXPECTED_HOUSEHOLD_ID));
        Assert.That(households[0].Name, Does.Contain("Andrew"));
        Assert.That(households[0].Access, Is.EqualTo("owner"));
        
        Console.WriteLine($"? Found {households.Count} household(s):");
        foreach (var household in households)
        {
            Console.WriteLine($"  - {household.Name} (ID: {household.Id})");
            Console.WriteLine($"    Access: {household.Access}");
        }
    }
    
    [Test]
    public async Task RealAPI_GetCreativeTonies_ReturnsYourTonie()
    {
        // Act
        var tonies = await _tonieClient.GetCreativeToniesByUserAsync(_username, _password);
        
        // Assert
        Assert.That(tonies, Is.Not.Null);
        Assert.That(tonies, Has.Count.GreaterThanOrEqualTo(1));
        
        var yourTonie = tonies.FirstOrDefault(t => t.Id == EXPECTED_TONIE_ID);
        Assert.That(yourTonie, Is.Not.Null, "Your Creative Tonie should be in the list");
        Assert.That(yourTonie!.Name, Is.EqualTo("Creative-Tonie"));
        Assert.That(yourTonie.ChaptersPresent, Is.GreaterThan(0));
        
        Console.WriteLine($"? Found {tonies.Count} Creative Tonie(s):");
        foreach (var tonie in tonies)
        {
            Console.WriteLine($"  - {tonie.Name} (ID: {tonie.Id})");
            Console.WriteLine($"    Chapters: {tonie.ChaptersPresent}/{tonie.ChaptersPresent + tonie.ChaptersRemaining}");
            Console.WriteLine($"    Storage: {tonie.SecondsPresent/60:F1}min used / {tonie.SecondsRemaining/60:F1}min free");
        }
    }
    
    [Test]
    public async Task RealAPI_GetTonieDetails_ReturnsAll29Chapters()
    {
        // Act
        var tonie = await _tonieClient.GetCreativeTonieDetailsAsync(
            _username, 
            _password, 
            EXPECTED_TONIE_ID);
        
        // Assert
        Assert.That(tonie, Is.Not.Null);
        Assert.That(tonie.Id, Is.EqualTo(EXPECTED_TONIE_ID));
        Assert.That(tonie.Chapters, Is.Not.Null);
        Assert.That(tonie.Chapters, Has.Count.EqualTo(EXPECTED_CHAPTER_COUNT));
        
        Console.WriteLine($"? Retrieved Tonie details for '{tonie.Name}':");
        Console.WriteLine($"  ID: {tonie.Id}");
        Console.WriteLine($"  Household: {tonie.HouseholdId}");
        Console.WriteLine($"  Chapters: {tonie.Chapters!.Count}");
        Console.WriteLine($"  Storage: {tonie.SecondsPresent/60:F1}min / {(tonie.SecondsPresent + tonie.SecondsRemaining)/60:F1}min");
        Console.WriteLine($"  Transcoding: {tonie.Transcoding}");
        Console.WriteLine($"  Last Update: {tonie.LastUpdate}");
        
        Console.WriteLine($"\n  All {tonie.Chapters.Count} Chapters:");
        for (int i = 0; i < tonie.Chapters.Count; i++)
        {
            var chapter = tonie.Chapters[i];
            Console.WriteLine($"    {i+1}. {chapter.Title} ({chapter.Seconds}s)");
            
            if (i >= 4 && tonie.Chapters.Count > 10) // Show first 5 and last 3
            {
                if (i < tonie.Chapters.Count - 3)
                    continue;
                if (i == tonie.Chapters.Count - 3)
                    Console.WriteLine($"    ... ({tonie.Chapters.Count - 8} more chapters) ...");
            }
        }
    }
    
    [Test]
    public async Task RealAPI_TokenCaching_UsesCache()
    {
        // Act - First call
        var token1 = await _authService.GetAccessTokenAsync(_username, _password);
        
        // Act - Second call (should use cache)
        var token2 = await _authService.GetAccessTokenAsync(_username, _password);
        
        // Assert
        Assert.That(token1, Is.EqualTo(token2), "Cached token should be the same");
        
        Console.WriteLine($"? Token caching working correctly");
        Console.WriteLine($"  First call returned token: {token1[..20]}...");
        Console.WriteLine($"  Second call returned same token from cache");
    }
    
    [Test]
    public async Task RealAPI_CompleteWorkflow_AuthListAndDetails()
    {
        // This test demonstrates a complete workflow
        Console.WriteLine("=== Complete Tonie Cloud Workflow ===\n");
        
        // Step 1: Authenticate
        Console.WriteLine("1??  Authenticating...");
        var token = await _authService.GetAccessTokenAsync(_username, _password);
        Console.WriteLine($"   ? Authenticated (token: {token[..15]}...)\n");
        
        // Step 2: Get households
        Console.WriteLine("2??  Fetching households...");
        var households = await _tonieClient.GetHouseholdsAsync(_username, _password);
        Console.WriteLine($"   ? Found {households.Count} household(s): {string.Join(", ", households.Select(h => h.Name))}\n");
        
        // Step 3: Get Creative Tonies
        Console.WriteLine("3??  Fetching Creative Tonies...");
        var tonies = await _tonieClient.GetCreativeToniesByUserAsync(_username, _password);
        Console.WriteLine($"   ? Found {tonies.Count} Creative Tonie(s)\n");
        
        // Step 4: Get details for first Tonie
        if (tonies.Any())
        {
            var firstTonie = tonies[0];
            Console.WriteLine($"4??  Fetching details for '{firstTonie.Name}'...");
            var details = await _tonieClient.GetCreativeTonieDetailsAsync(
                _username, 
                _password, 
                firstTonie.Id);
            Console.WriteLine($"   ? Retrieved {details.Chapters?.Count ?? 0} chapter(s)");
            Console.WriteLine($"   ? Storage: {details.SecondsPresent/60:F1}min used, {details.SecondsRemaining/60:F1}min free\n");
        }
        
        Console.WriteLine("=== Workflow Complete ===");
        
        // Assert
        Assert.That(token, Is.Not.Empty);
        Assert.That(households, Is.Not.Empty);
        Assert.That(tonies, Is.Not.Empty);
    }
}
