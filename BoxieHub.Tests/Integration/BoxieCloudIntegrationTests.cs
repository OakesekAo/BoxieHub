using BoxieHub.Services.BoxieCloud;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace BoxieHub.Tests.Integration;

/// <summary>
/// Integration tests with REAL Tonie Cloud API via BoxieCloud services
/// These tests require valid credentials from User Secrets
/// 
/// Setup:
///   dotnet user-secrets set "Tonie:Username" "your_email@example.com" --project BoxieHub.Tests
///   dotnet user-secrets set "Tonie:Password" "your_password" --project BoxieHub.Tests
/// 
/// Run with: dotnet test --filter Category=Integration
/// </summary>
[TestFixture]
[Category("Integration")]
[Explicit] // Don't run automatically in CI/CD
public class BoxieCloudIntegrationTests
{
    private string _username = null!;
    private string _password = null!;
    
    // Update these if your data changes
    private string? _expectedHouseholdId;
    private string? _expectedTonieId;
    
    private HttpClient _apiHttpClient = null!;
    private HttpClient _authHttpClient = null!;
    private IMemoryCache _memoryCache = null!;
    private IBoxieAuthService _authService = null!;
    private IS3StorageService _s3Storage = null!;
    private IBoxieCloudClient _boxieClient = null!;
    
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Load credentials from multiple sources (first found wins):
        // 1. appsettings.local.json (not committed, for local dev)
        // 2. User Secrets (dotnet user-secrets)
        // 3. Environment Variables
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.local.json", optional: true) // Local file, add to .gitignore
            .AddUserSecrets<BoxieCloudIntegrationTests>() // Uses the UserSecretsId from BoxieHub.Tests.csproj
            .AddEnvironmentVariables() // Fallback to environment variables
            .Build();
        
        _username = configuration["Tonie:Username"] 
            ?? Environment.GetEnvironmentVariable("TONIE_USERNAME")
            ?? throw new InvalidOperationException(
                "Tonie:Username not found. Setup:\n" +
                "1. User Secrets: dotnet user-secrets set \"Tonie:Username\" \"your_email@example.com\" --project BoxieHub.Tests\n" +
                "2. Or Environment: $env:TONIE_USERNAME = \"your_email@example.com\"\n" +
                "3. Or create BoxieHub.Tests/bin/Debug/net8.0/appsettings.local.json with credentials");
        
        _password = configuration["Tonie:Password"] 
            ?? Environment.GetEnvironmentVariable("TONIE_PASSWORD")
            ?? throw new InvalidOperationException(
                "Tonie:Password not found. Setup:\n" +
                "1. User Secrets: dotnet user-secrets set \"Tonie:Password\" \"your_password\" --project BoxieHub.Tests\n" +
                "2. Or Environment: $env:TONIE_PASSWORD = \"your_password\"\n" +
                "3. Or create BoxieHub.Tests/bin/Debug/net8.0/appsettings.local.json with credentials");
        
        // Log that credentials were loaded (without showing them)
        Console.WriteLine($"? Credentials loaded for user: {_username[..Math.Min(5, _username.Length)]}***");
        Console.WriteLine($"  Username length: {_username.Length}, Password length: {_password.Length}");
        Console.WriteLine($"  Username contains @: {_username.Contains("@")}");
        Console.WriteLine($"  Username has whitespace: {_username.Any(char.IsWhiteSpace)}");
        Console.WriteLine($"  Password has whitespace: {_password.Any(char.IsWhiteSpace)}");
        
        // Separate HttpClient for authentication (no BaseAddress - uses absolute URLs)
        _authHttpClient = new HttpClient();
        
        // Separate HttpClient for API calls (note trailing slash is required for proper relative URL combining)
        _apiHttpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.tonie.cloud/v2/")
        };
        
        // Create cache ONCE for all tests - this allows token caching to work across tests
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        
        var authLogger = new LoggerFactory().CreateLogger<BoxieAuthService>();
        _authService = new BoxieAuthService(_authHttpClient, _memoryCache, authLogger);
        
        var s3Logger = new LoggerFactory().CreateLogger<S3StorageService>();
        _s3Storage = new S3StorageService(_apiHttpClient, s3Logger);
        
        var clientLogger = new LoggerFactory().CreateLogger<BoxieCloudClient>();
        _boxieClient = new BoxieCloudClient(_apiHttpClient, _authService, _s3Storage, clientLogger);
    }
    
    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _apiHttpClient?.Dispose();
        _authHttpClient?.Dispose();
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
        
        Console.WriteLine($"✅ Credentials loaded successfully");
        Console.WriteLine($"  Username: {_username[..Math.Min(10, _username.Length)]}***");
        Console.WriteLine($"  Password: ***{_password[Math.Max(0, _password.Length - 3)..]}***");
    }
    
    [Test]
    [Order(1)]
    public async Task Diagnostic_VerifyAuthenticationWorks()
    {
        Console.WriteLine("🔐 Testing authentication flow...");
        Console.WriteLine($"📋 Username: {_username[..Math.Min(10, _username.Length)]}***");
        Console.WriteLine($"📋 Password length: {_password.Length} characters");
        Console.WriteLine($"📋 Auth endpoint: {TOKEN_ENDPOINT}");
        
        try
        {
            // Attempt authentication
            var token = await _authService.GetAccessTokenAsync(_username, _password);
            
            // Verify token format
            Assert.That(token, Is.Not.Null.And.Not.Empty, "Token should not be null or empty");
            Assert.That(token, Does.StartWith("eyJ"), "Token should be a JWT (starts with 'eyJ')");
            Console.WriteLine($"  ✅ Token received: {token[..Math.Min(30, token.Length)]}...");
            
            // Verify token is actually valid by making an API call
            Console.WriteLine("  📡 Validating token with API call...");
            var request = new HttpRequestMessage(HttpMethod.Get, "households");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            var response = await _apiHttpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Assert.Fail($"Token validation failed: {response.StatusCode}\nError: {errorContent}");
            }
            
            Console.WriteLine($"  ✅ Token is valid (API returned {response.StatusCode})");
            Console.WriteLine("\n✅ Authentication system working correctly!");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to authenticate"))
        {
            Console.WriteLine($"\n❌ Authentication FAILED!");
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"\n📝 Troubleshooting steps:");
            Console.WriteLine($"1. Verify credentials are correct:");
            Console.WriteLine($"   Username: {_username}");
            Console.WriteLine($"   Password: (length {_password.Length})");
            Console.WriteLine($"2. Check if Tonie Cloud API is accessible:");
            Console.WriteLine($"   https://login.tonies.com/auth/realms/tonies/protocol/openid-connect/token");
            Console.WriteLine($"3. Verify your Tonie account is active and not locked");
            Console.WriteLine($"4. Check for rate limiting (too many auth requests)");
            Console.WriteLine($"\n💡 To update credentials:");
            Console.WriteLine($"   dotnet user-secrets set \"Tonie:Username\" \"your_email@example.com\" --project BoxieHub.Tests");
            Console.WriteLine($"   dotnet user-secrets set \"Tonie:Password\" \"your_password\" --project BoxieHub.Tests");
            
            throw; // Re-throw to fail the test
        }
    }
    
    // Add a constant for the token endpoint so it can be logged
    private const string TOKEN_ENDPOINT = "https://login.tonies.com/auth/realms/tonies/protocol/openid-connect/token";
    
    [Test]
    [Order(2)]
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
    [Order(3)]
    public async Task RealAPI_GetHouseholds_ReturnsData()
    {
        // Act
        var households = await _boxieClient.GetHouseholdsAsync(_username, _password);
        
        // Assert
        Assert.That(households, Is.Not.Null);
        Assert.That(households, Is.Not.Empty);
        
        // Store for later tests
        _expectedHouseholdId = households[0].Id;
        
        Console.WriteLine($"? Found {households.Count} household(s):");
        foreach (var household in households)
        {
            Console.WriteLine($"  - {household.Name} (ID: {household.Id})");
            Console.WriteLine($"    Access: {household.Access}");
        }
    }
    
    [Test]
    [Order(4)]
    public async Task RealAPI_GetCreativeToniesByHousehold_ReturnsData()
    {
        // This test demonstrates a complete workflow
        Console.WriteLine("=== Complete BoxieCloud Workflow ===\n");

        // Step 1: Authenticate
        Console.WriteLine("1??  Authenticating...");
        var token = await _authService.GetAccessTokenAsync(_username, _password);
        Console.WriteLine($"   ? Authenticated (token: {token[..15]}...)\n");

        // Step 2: Get households
        Console.WriteLine("2??  Fetching households...");
        var households = await _boxieClient.GetHouseholdsAsync(_username, _password);
        Console.WriteLine($"   ? Found {households.Count} household(s): {string.Join(", ", households.Select(h => h.Name))}\n");


            
        Assert.That(households, Is.Not.Empty, "Need at least one household");
        
        var householdId = households[0].Id;
        
        // Act
        var tonies = await _boxieClient.GetCreativeToniesByHouseholdAsync(
            _username, 
            _password, 
            householdId);
        
        // Assert
        Assert.That(tonies, Is.Not.Null);
        
        if (tonies.Any())
        {
            _expectedTonieId = tonies[0].Id;
            
            Console.WriteLine($"? Found {tonies.Count} Creative Tonie(s) in household {households[0].Name}:");
            foreach (var tonie in tonies)
            {
                Console.WriteLine($"  - {tonie.Name} (ID: {tonie.Id})");
                Console.WriteLine($"    Chapters: {tonie.ChaptersPresent}/{tonie.ChaptersPresent + tonie.ChaptersRemaining}");
                Console.WriteLine($"    Storage: {tonie.SecondsPresent/60:F1}min used / {tonie.SecondsRemaining/60:F1}min free");
            }
        }
        else
        {
            Console.WriteLine($"? No Creative Tonies found in household (this is OK)");
        }
    }
    
    [Test]
    [Order(5)]
    public async Task RealAPI_GetTonieDetails_ReturnsChapters()
    {
        // Arrange - get households and tonies first
        // This test demonstrates a complete workflow
        Console.WriteLine("=== Complete BoxieCloud Workflow ===\n");

        // Step 1: Authenticate
        Console.WriteLine("1??  Authenticating...");
        var token = await _authService.GetAccessTokenAsync(_username, _password);
        Console.WriteLine($"   ? Authenticated (token: {token[..15]}...)\n");

        // Step 2: Get households
        Console.WriteLine("2??  Fetching households...");
        var households = await _boxieClient.GetHouseholdsAsync(_username, _password);
        Console.WriteLine($"   ? Found {households.Count} household(s): {string.Join(", ", households.Select(h => h.Name))}\n");

        households = await _boxieClient.GetHouseholdsAsync(_username, _password);
        Assert.That(households, Is.Not.Empty);
        
        var householdId = households[0].Id;
        var tonies = await _boxieClient.GetCreativeToniesByHouseholdAsync(_username, _password, householdId);
        
        if (!tonies.Any())
        {
            Assert.Ignore("No Creative Tonies available to test");
            return;
        }
        
        var tonieId = tonies[0].Id;
        
        // Act
        var tonie = await _boxieClient.GetCreativeTonieDetailsAsync(
            _username, 
            _password,
            householdId,
            tonieId);
        
        // Assert
        Assert.That(tonie, Is.Not.Null);
        Assert.That(tonie.Id, Is.EqualTo(tonieId));
        Assert.That(tonie.Chapters, Is.Not.Null);
        
        Console.WriteLine($"? Retrieved Tonie details for '{tonie.Name}':");
        Console.WriteLine($"  ID: {tonie.Id}");
        Console.WriteLine($"  Household: {tonie.HouseholdId}");
        Console.WriteLine($"  Chapters: {tonie.Chapters!.Count}");
        Console.WriteLine($"  Storage: {tonie.SecondsPresent/60:F1}min / {(tonie.SecondsPresent + tonie.SecondsRemaining)/60:F1}min");
        Console.WriteLine($"  Transcoding: {tonie.Transcoding}");
        Console.WriteLine($"  Last Update: {tonie.LastUpdate}");
        
        if (tonie.Chapters.Any())
        {
            Console.WriteLine($"\n  First 5 Chapters:");
            foreach (var chapter in tonie.Chapters.Take(5))
            {
                Console.WriteLine($"    - {chapter.Title} ({chapter.Seconds}s)");
            }
        }
    }
    
    [Test]
    [Order(6)]
    public async Task RealAPI_GetUploadToken_ReturnsValidToken()
    {
        // Act
        var uploadToken = await _boxieClient.GetUploadTokenAsync(_username, _password);
        
        // Assert
        Assert.That(uploadToken, Is.Not.Null);
        Assert.That(uploadToken.FileId, Is.Not.Null.And.Not.Empty);
        Assert.That(uploadToken.Request, Is.Not.Null);
        Assert.That(uploadToken.Request.Url, Is.Not.Null.And.Not.Empty);
        Assert.That(uploadToken.Request.Fields, Is.Not.Null);
        Assert.That(uploadToken.Request.Fields.Key, Is.Not.Null.And.Not.Empty);
        
        Console.WriteLine($"? Successfully retrieved S3 upload token");
        Console.WriteLine($"  File ID: {uploadToken.FileId}");
        Console.WriteLine($"  Upload URL: {uploadToken.Request.Url}");
        Console.WriteLine($"  Key: {uploadToken.Request.Fields.Key}");
    }
    
    [Test]
    [Order(7)]
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
    [Order(8)]
    public async Task RealAPI_CompleteWorkflow_AuthListAndDetails()
    {
        // This test demonstrates a complete workflow
        Console.WriteLine("=== Complete BoxieCloud Workflow ===\n");
        
        // Step 1: Authenticate
        Console.WriteLine("1??  Authenticating...");
        var token = await _authService.GetAccessTokenAsync(_username, _password);
        Console.WriteLine($"   ? Authenticated (token: {token[..15]}...)\n");
        
        // Step 2: Get households
        Console.WriteLine("2??  Fetching households...");
        var households = await _boxieClient.GetHouseholdsAsync(_username, _password);
        Console.WriteLine($"   ? Found {households.Count} household(s): {string.Join(", ", households.Select(h => h.Name))}\n");
        
        // Step 3: Get Creative Tonies
        Console.WriteLine("3??  Fetching Creative Tonies...");
        var householdId = households[0].Id;
        var tonies = await _boxieClient.GetCreativeToniesByHouseholdAsync(_username, _password, householdId);
        Console.WriteLine($"   ? Found {tonies.Count} Creative Tonie(s)\n");
        
        // Step 4: Get details for first Tonie
        if (tonies.Any())
        {
            var firstTonie = tonies[0];
            Console.WriteLine($"4??  Fetching details for '{firstTonie.Name}'...");
            var details = await _boxieClient.GetCreativeTonieDetailsAsync(
                _username, 
                _password,
                householdId,
                firstTonie.Id);
            Console.WriteLine($"   ? Retrieved {details.Chapters?.Count ?? 0} chapter(s)");
            Console.WriteLine($"   ? Storage: {details.SecondsPresent/60:F1}min used, {details.SecondsRemaining/60:F1}min free\n");
        }
        
        Console.WriteLine("=== Workflow Complete ===");
        
        // Assert
        Assert.That(token, Is.Not.Empty);
        Assert.That(households, Is.Not.Empty);
    }
}
