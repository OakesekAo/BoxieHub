using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BoxieHub.Models.BoxieCloud;

namespace BoxieHub.Services.BoxieCloud;

/// <summary>
/// Client for Tonie Cloud API
/// Handles all API calls with proper authentication
/// Based on real API endpoints: https://api.tonie.cloud/v2
/// </summary>
public class BoxieCloudClient : IBoxieCloudClient
{
    private readonly HttpClient _httpClient;
    private readonly IBoxieAuthService _authService;
    private readonly IS3StorageService _s3Storage;
    private readonly ILogger<BoxieCloudClient> _logger;
    
    private const string API_BASE = "https://api.tonie.cloud/v2/";
    
    public BoxieCloudClient(
        HttpClient httpClient,
        IBoxieAuthService authService,
        IS3StorageService s3Storage,
        ILogger<BoxieCloudClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _s3Storage = s3Storage ?? throw new ArgumentNullException(nameof(s3Storage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(API_BASE);
        }
    }
    
    /// <summary>
    /// Get all households for authenticated user
    /// Endpoint: GET /v2/households
    /// </summary>
    public async Task<List<HouseholdDto>> GetHouseholdsAsync(
        string username, 
        string password, 
        CancellationToken ct = default)
    {
        try
        {
            var token = await _authService.GetAccessTokenAsync(username, password, ct);
            
            var request = new HttpRequestMessage(HttpMethod.Get, "households");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            _logger.LogDebug("Requesting households for {Username}", username);
            
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to get households. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }
            
            var households = await response.Content.ReadFromJsonAsync<List<HouseholdDto>>(cancellationToken: ct);
            _logger.LogInformation("Retrieved {Count} household(s) for {Username}", households?.Count ?? 0, username);
            
            return households ?? new List<HouseholdDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error getting households for {Username}", username);
            throw new InvalidOperationException($"Failed to get households for {username}", ex);
        }
    }
    
    /// <summary>
    /// Get all Creative Tonies for a specific household
    /// Endpoint: GET /v2/households/{householdId}/creativetonies
    /// </summary>
    public async Task<List<CreativeTonieDto>> GetCreativeToniesByHouseholdAsync(
        string username, 
        string password, 
        string householdId, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(householdId))
            throw new ArgumentException("Household ID is required", nameof(householdId));
        
        try
        {
            var token = await _authService.GetAccessTokenAsync(username, password, ct);
            
            var request = new HttpRequestMessage(HttpMethod.Get, $"households/{householdId}/creativetonies");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            _logger.LogDebug("Requesting Creative Tonies for household {HouseholdId}", householdId);
            
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to get Creative Tonies. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }
            
            var tonies = await response.Content.ReadFromJsonAsync<List<CreativeTonieDto>>(cancellationToken: ct);
            _logger.LogInformation("Retrieved {Count} Creative Tonie(s) for household {HouseholdId}", 
                tonies?.Count ?? 0, householdId);
            
            return tonies ?? new List<CreativeTonieDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error getting Creative Tonies for household {HouseholdId}", householdId);
            throw new InvalidOperationException($"Failed to get Creative Tonies for household {householdId}", ex);
        }
    }
    
    /// <summary>
    /// Get detailed information for a specific Creative Tonie including all chapters
    /// Endpoint: GET /v2/households/{householdId}/creativetonies/{tonieId}
    /// </summary>
    public async Task<CreativeTonieDto> GetCreativeTonieDetailsAsync(
        string username, 
        string password, 
        string householdId, 
        string tonieId, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(householdId))
            throw new ArgumentException("Household ID is required", nameof(householdId));
        if (string.IsNullOrEmpty(tonieId))
            throw new ArgumentException("Tonie ID is required", nameof(tonieId));
        
        try
        {
            var token = await _authService.GetAccessTokenAsync(username, password, ct);
            
            var request = new HttpRequestMessage(HttpMethod.Get, 
                $"households/{householdId}/creativetonies/{tonieId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            _logger.LogDebug("Requesting details for Tonie {TonieId} in household {HouseholdId}", 
                tonieId, householdId);
            
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to get Tonie details. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }
            
            var tonie = await response.Content.ReadFromJsonAsync<CreativeTonieDto>(cancellationToken: ct);
            
            if (tonie == null)
                throw new InvalidOperationException($"Failed to deserialize Tonie details for {tonieId}");
            
            _logger.LogInformation("Retrieved details for Tonie {TonieId} with {ChapterCount} chapter(s)", 
                tonieId, tonie.Chapters?.Count ?? 0);
            
            return tonie;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error getting Tonie details for {TonieId}", tonieId);
            throw new InvalidOperationException($"Failed to get Tonie details for {tonieId}", ex);
        }
    }
    
    /// <summary>
    /// Get S3-compatible upload token from Tonie Cloud
    /// Endpoint: POST /v2/file
    /// </summary>
    public async Task<S3UploadTokenDto> GetUploadTokenAsync(
        string username, 
        string password, 
        CancellationToken ct = default)
    {
        try
        {
            var token = await _authService.GetAccessTokenAsync(username, password, ct);
            
            var request = new HttpRequestMessage(HttpMethod.Post, "file");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            _logger.LogDebug("Requesting S3 upload token");
            
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to get upload token. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }
            
            var uploadToken = await response.Content.ReadFromJsonAsync<S3UploadTokenDto>(cancellationToken: ct);
            
            if (uploadToken == null)
                throw new InvalidOperationException("Failed to deserialize upload token response");
            
            _logger.LogInformation("Retrieved S3 upload token with fileId: {FileId}", uploadToken.FileId);
            
            return uploadToken;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error getting upload token");
            throw new InvalidOperationException("Failed to get S3 upload token", ex);
        }
    }
    
    /// <summary>
    /// Update Creative Tonie with new chapters
    /// Endpoint: PATCH /v2/households/{householdId}/creativetonies/{tonieId}
    /// </summary>
    public async Task<CreativeTonieDto> PatchCreativeTonieAsync(
        string username, 
        string password, 
        string householdId, 
        string tonieId, 
        string name, 
        List<ChapterDto> chapters, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(householdId))
            throw new ArgumentException("Household ID is required", nameof(householdId));
        if (string.IsNullOrEmpty(tonieId))
            throw new ArgumentException("Tonie ID is required", nameof(tonieId));
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Name is required", nameof(name));
        if (chapters == null || chapters.Count == 0)
            throw new ArgumentException("Chapters are required", nameof(chapters));
        
        try
        {
            var token = await _authService.GetAccessTokenAsync(username, password, ct);
            
            var patchData = new
            {
                name = name,
                chapters = chapters.Select(c => new
                {
                    title = c.Title,
                    file = c.File
                }).ToList()
            };
            
            var json = JsonSerializer.Serialize(patchData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Patch, 
                $"households/{householdId}/creativetonies/{tonieId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = content;
            
            _logger.LogInformation("Patching Tonie {TonieId} with {ChapterCount} chapter(s)", 
                tonieId, chapters.Count);
            
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to patch Tonie. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }
            
            var updatedTonie = await response.Content.ReadFromJsonAsync<CreativeTonieDto>(cancellationToken: ct);
            
            if (updatedTonie == null)
                throw new InvalidOperationException($"Failed to deserialize patched Tonie response for {tonieId}");
            
            _logger.LogInformation("Successfully patched Tonie {TonieId}", tonieId);
            
            return updatedTonie;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error patching Tonie {TonieId}", tonieId);
            throw new InvalidOperationException($"Failed to patch Tonie {tonieId}", ex);
        }
    }
    
    /// <summary>
    /// Upload audio file to Creative Tonie (simplified method)
    /// Handles the full S3 upload workflow internally:
    /// 1. Get upload token
    /// 2. Upload file to S3
    /// 3. Create chapter on Tonie
    /// </summary>
    public async Task<SyncResultDto> SyncAudioAsync(
        string username, 
        string password, 
        string householdId, 
        string tonieId, 
        Stream audioStream, 
        string title, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(householdId))
            throw new ArgumentException("Household ID is required", nameof(householdId));
        if (string.IsNullOrEmpty(tonieId))
            throw new ArgumentException("Tonie ID is required", nameof(tonieId));
        if (audioStream == null)
            throw new ArgumentNullException(nameof(audioStream));
        if (string.IsNullOrEmpty(title))
            throw new ArgumentException("Title is required", nameof(title));
        
        try
        {
            _logger.LogInformation("Starting audio sync for Tonie {TonieId}: '{Title}'", tonieId, title);
            
            // Step 1: Get upload token
            var uploadToken = await GetUploadTokenAsync(username, password, ct);
            
            // Step 2: Upload to S3
            var fields = new Dictionary<string, string>
            {
                ["key"] = uploadToken.Request.Fields.Key,
                ["x-amz-algorithm"] = uploadToken.Request.Fields.AmazonAlgorithm,
                ["x-amz-credential"] = uploadToken.Request.Fields.AmazonCredential,
                ["x-amz-date"] = uploadToken.Request.Fields.AmazonDate,
                ["policy"] = uploadToken.Request.Fields.Policy,
                ["x-amz-signature"] = uploadToken.Request.Fields.AmazonSignature
            };
            
            if (!string.IsNullOrEmpty(uploadToken.Request.Fields.AmazonSecurityToken))
            {
                fields["x-amz-security-token"] = uploadToken.Request.Fields.AmazonSecurityToken;
            }
            
            await _s3Storage.UploadFileAsync(
                uploadToken.Request.Url, 
                fields, 
                uploadToken.FileId, 
                audioStream, 
                ct);
            
            // Step 3: Get current chapters and add new one
            var currentTonie = await GetCreativeTonieDetailsAsync(username, password, householdId, tonieId, ct);
            var chapters = currentTonie.Chapters ?? new List<ChapterDto>();
            
            // Add new chapter
            chapters.Add(new ChapterDto
            {
                Id = Guid.NewGuid().ToString(),
                Title = title,
                Seconds = 0, // Will be calculated by Tonie Cloud
                File = uploadToken.FileId,
                Transcoding = false
            });
            
            // Step 4: Patch Tonie with updated chapters
            await PatchCreativeTonieAsync(
                username, 
                password, 
                householdId, 
                tonieId, 
                currentTonie.Name, 
                chapters, 
                ct);
            
            _logger.LogInformation("Successfully synced audio '{Title}' to Tonie {TonieId}", title, tonieId);
            
            return new SyncResultDto
            {
                Success = true,
                Message = $"Successfully uploaded '{title}' to Tonie",
                TracksProcessed = 1
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing audio to Tonie {TonieId}", tonieId);
            return new SyncResultDto
            {
                Success = false,
                Message = "Upload failed",
                ErrorDetails = ex.Message,
                TracksProcessed = 0
            };
        }
    }
}
