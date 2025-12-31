using BoxieHub.Client.Models.Enums;
using BoxieHub.Services.PythonAdapter;
using BoxieHub.Services.PythonAdapter.Dtos;
using BoxieHub.Services.Sync;
using BoxieHub.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BoxieHub.Tests.Integration;

/// <summary>
/// Real integration tests that require Python adapter to be running.
/// These tests verify the complete flow: C# ? HTTP ? Python ? Response ? C#
/// 
/// Prerequisites:
/// 1. Python adapter must be running (python services/tonie-adapter/main.py)
/// 2. Python adapter must be at http://localhost:8000
/// 
/// Run with: dotnet test --filter "FullyQualifiedName~RealIntegrationTests"
/// </summary>
[TestFixture]
[Category("IntegrationReal")]
public class RealIntegrationTests : TestBase
{
    private HttpClient _httpClient;
    private IPythonAdapterClient _pythonClient;
    private ISyncJobService _syncService;
    private ILogger<PythonAdapterClient> _adapterLogger;
    private ILogger<SyncJobService> _syncLogger;
    
    private const string PYTHON_ADAPTER_URL = "http://localhost:8000";
    
    [SetUp]
    public void SetUp()
    {
        base.SetUpDatabase();
        
        // Create real HTTP client pointing to Python adapter with cookie container
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new System.Net.CookieContainer()
        };
        
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(PYTHON_ADAPTER_URL),
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        _adapterLogger = new Mock<ILogger<PythonAdapterClient>>().Object;
        _syncLogger = new Mock<ILogger<SyncJobService>>().Object;
        
        // Create real Python adapter client using the SAME HttpClient (shares session)
        _pythonClient = new PythonAdapterClient(_httpClient, _adapterLogger);
        
        // Create sync service with real Python client
        _syncService = new SyncJobService(DbContext, _pythonClient, _syncLogger);
    }
    
    [TearDown]
    public void TearDown()
    {
        base.TearDown();
        _httpClient?.Dispose();
    }
    
    [Test]
    public async Task PythonAdapter_HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await _httpClient.GetAsync("/health");
        
        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        var health = await response.Content.ReadFromJsonAsync<HealthResponseDto>();
        Assert.That(health, Is.Not.Null);
        Assert.That(health.Status, Is.Not.Null.And.Not.Empty);
        Assert.That(health.Version, Is.Not.Null.And.Not.Empty);
    }
    
    [Test]
    public async Task PythonAdapter_Login_ReturnsSuccess()
    {
        // Arrange
        var loginRequest = new
        {
            username = "testuser",
            password = "testpass"
        };
        
        // Act
        var response = await _httpClient.PostAsJsonAsync("/auth/login", loginRequest);
        
        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.That(loginResponse, Is.Not.Null);
        Assert.That(loginResponse.Success, Is.True);
        Assert.That(loginResponse.SessionId, Is.Not.Null.And.Not.Empty);
    }
    
    [Test]
    public async Task PythonAdapter_ListCreativeTonies_AfterLogin_ReturnsDevices()
    {
        // Arrange - Login first
        await _httpClient.PostAsJsonAsync("/auth/login", new
        {
            username = "testuser",
            password = "testpass"
        });
        
        // Act
        var response = await _httpClient.GetAsync("/creative-tonies");
        
        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        var result = await response.Content.ReadFromJsonAsync<CreativeTonieListResponseDto>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Tonies, Is.Not.Null);
        Assert.That(result.Count, Is.GreaterThan(0));
    }
    
    [Test]
    public async Task PythonAdapter_Sync_AfterLogin_ReturnsSuccess()
    {
        // Arrange - Login first
        await _httpClient.PostAsJsonAsync("/auth/login", new
        {
            username = "testuser",
            password = "testpass"
        });
        
        var syncRequest = new
        {
            creativeTonieExternalId = "device-001",
            tracks = new[]
            {
                new { title = "Test Story", sourceUrl = "http://example.com/audio.mp3" }
            }
        };
        
        // Act
        var response = await _httpClient.PostAsJsonAsync("/sync", syncRequest);
        
        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        var syncResponse = await response.Content.ReadFromJsonAsync<SyncResponseDto>();
        Assert.That(syncResponse, Is.Not.Null);
        Assert.That(syncResponse.Success, Is.True);
        Assert.That(syncResponse.TracksProcessed, Is.EqualTo(1));
    }
    
    [Test]
    public async Task PythonAdapterClient_GetHealthAsync_CallsRealPythonService()
    {
        // Act
        var health = await _pythonClient.GetHealthAsync();
        
        // Assert
        Assert.That(health, Is.Not.Null);
        Assert.That(health.Status, Is.Not.Null);
        Assert.That(health.Version, Is.Not.Null);
        Assert.That(health.CheckedAt, Is.Not.EqualTo(default(DateTime)));
    }
    
    [Test]
    public async Task PythonAdapterClient_SyncAsync_CallsRealPythonService()
    {
        // Arrange - Need to login to Python adapter first
        await _httpClient.PostAsJsonAsync("/auth/login", new
        {
            username = "testuser",
            password = "testpass"
        });
        
        var syncRequest = new SyncRequestDto
        {
            CreativeTonieExternalId = "device-001",
            Tracks = new List<SyncTrackDto>
            {
                new SyncTrackDto
                {
                    Title = "Story 1",
                    SourceUrl = "http://example.com/story1.mp3"
                }
            }
        };
        
        // Act
        var response = await _pythonClient.SyncAsync(syncRequest);
        
        // Assert
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Success, Is.True);
        Assert.That(response.Message, Does.Contain("synced"));
        Assert.That(response.TracksProcessed, Is.EqualTo(1));
    }
    
    [Test]
    public async Task SyncJobService_ExecuteSyncAsync_WithRealPythonAdapter_CreatesCompletedJob()
    {
        // Arrange - Login to Python adapter
        await _httpClient.PostAsJsonAsync("/auth/login", new
        {
            username = "testuser",
            password = "testpass"
        });
        
        // Create test data
        var household = CreateTestHousehold("Integration Test House");
        var device = CreateTestDevice(household, "Test Tonie", "tonie-integration-001");
        var upload = CreateTestImageUpload("integration-test.mp3");
        var contentItem = CreateTestContentItem(household, upload, "Integration Test Story");
        var assignment = CreateTestContentAssignment(household, device, contentItem);
        
        // Act - Call real service with real Python adapter
        var job = await _syncService.ExecuteSyncAsync(device.Id, assignment.Id, "integration-test-user");
        
        // Assert
        Assert.That(job, Is.Not.Null);
        Assert.That(job.Status, Is.EqualTo(SyncStatus.Completed));
        Assert.That(job.ErrorMessage, Is.Null);
        Assert.That(job.Started, Is.Not.Null);
        Assert.That(job.Completed, Is.Not.Null);
        
        // Verify job was saved to database
        var savedJob = await _syncService.GetJobAsync(job.Id);
        Assert.That(savedJob, Is.Not.Null);
        Assert.That(savedJob.Status, Is.EqualTo(SyncStatus.Completed));
    }
    
    [Test]
    public async Task CompleteWorkflow_LoginSyncLogout_WorksEndToEnd()
    {
        // Arrange - Create test data
        var household = CreateTestHousehold("Workflow Test House");
        var device = CreateTestDevice(household, "Workflow Tonie", "tonie-workflow-001");
        var upload = CreateTestImageUpload("workflow.mp3");
        var contentItem = CreateTestContentItem(household, upload, "Workflow Story");
        var assignment = CreateTestContentAssignment(household, device, contentItem);
        
        // Act & Assert - Step 1: Login
        var loginResponse = await _httpClient.PostAsJsonAsync("/auth/login", new
        {
            username = "testuser",
            password = "testpass"
        });
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        // Act & Assert - Step 2: Check health (should show authenticated)
        var healthResponse = await _httpClient.GetAsync("/health");
        var health = await healthResponse.Content.ReadFromJsonAsync<HealthResponseDto>();
        Assert.That(health.Authenticated, Is.True);
        Assert.That(health.Status, Is.EqualTo("healthy"));
        
        // Act & Assert - Step 3: Execute sync through service
        var job = await _syncService.ExecuteSyncAsync(device.Id, assignment.Id, "workflow-user");
        Assert.That(job.Status, Is.EqualTo(SyncStatus.Completed));
        
        // Act & Assert - Step 4: Verify job in database
        var savedJob = await _syncService.GetJobAsync(job.Id);
        Assert.That(savedJob, Is.Not.Null);
        Assert.That(savedJob.Status, Is.EqualTo(SyncStatus.Completed));
        
        // Act & Assert - Step 5: Verify job appears in household jobs
        var householdJobs = await _syncService.GetJobsAsync(household.Id);
        Assert.That(householdJobs.Any(j => j.Id == job.Id), Is.True);
        
        // Act & Assert - Step 6: Logout
        var logoutResponse = await _httpClient.PostAsync("/auth/logout", null);
        Assert.That(logoutResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        // Act & Assert - Step 7: Health check should show not authenticated
        var healthAfterLogout = await _httpClient.GetAsync("/health");
        var healthData = await healthAfterLogout.Content.ReadFromJsonAsync<HealthResponseDto>();
        Assert.That(healthData.Authenticated, Is.False);
    }
    
    [Test]
    public async Task MultipleDevicesSync_WithRealPythonAdapter_AllSucceed()
    {
        // Arrange - Login
        await _httpClient.PostAsJsonAsync("/auth/login", new
        {
            username = "testuser",
            password = "testpass"
        });
        
        // Create test data with 3 devices
        var household = CreateTestHousehold("Multi-Device House");
        var device1 = CreateTestDevice(household, "Device 1", "tonie-multi-001");
        var device2 = CreateTestDevice(household, "Device 2", "tonie-multi-002");
        var device3 = CreateTestDevice(household, "Device 3", "tonie-multi-003");
        
        var upload = CreateTestImageUpload("multi-story.mp3");
        var contentItem = CreateTestContentItem(household, upload, "Multi Story");
        
        var assignment1 = CreateTestContentAssignment(household, device1, contentItem);
        var assignment2 = CreateTestContentAssignment(household, device2, contentItem);
        var assignment3 = CreateTestContentAssignment(household, device3, contentItem);
        
        // Act - Sync to all 3 devices
        var job1 = await _syncService.ExecuteSyncAsync(device1.Id, assignment1.Id, "multi-user");
        var job2 = await _syncService.ExecuteSyncAsync(device2.Id, assignment2.Id, "multi-user");
        var job3 = await _syncService.ExecuteSyncAsync(device3.Id, assignment3.Id, "multi-user");
        
        // Assert - All jobs completed
        Assert.That(job1.Status, Is.EqualTo(SyncStatus.Completed));
        Assert.That(job2.Status, Is.EqualTo(SyncStatus.Completed));
        Assert.That(job3.Status, Is.EqualTo(SyncStatus.Completed));
        
        // Verify all jobs in household history
        var householdJobs = await _syncService.GetJobsAsync(household.Id);
        Assert.That(householdJobs.Count(), Is.EqualTo(3));
        Assert.That(householdJobs.All(j => j.Status == SyncStatus.Completed), Is.True);
    }
}

/// <summary>
/// DTOs for deserializing Python adapter responses
/// </summary>
public class LoginResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string SessionId { get; set; }
}

public class CreativeTonieListResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public List<CreativeTonieDto> Tonies { get; set; }
    public int Count { get; set; }
}

public class CreativeTonieDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Status { get; set; }
    public DateTime? LastSeen { get; set; }
    public string CurrentContent { get; set; }
}
