# Storage Provider Fix - Implementation Summary

## ? COMPLETED

### 1. Fixed Immediate Bug - Library Files Now Work with S3
**Problem**: Library items stored in S3 couldn't be uploaded to Tonies because `FileUpload.Data` was NULL.

**Solution**: Modified `Upload.razor` to download files from storage before uploading to Tonie Cloud.

**Changes**:
- `BoxieHub/Components/Pages/Tonies/Upload.razor`:
  - Added `IFileStorageService` injection
  - Check `FileUpload.Provider` to determine storage location
  - For Database provider: use `FileUpload.Data` directly
  - For external providers (S3, Dropbox, GDrive): download via `FileStorageService.DownloadFileAsync()`
  - Proper error handling for storage failures

**Testing**: 
- Upload file to library ? Verify appears in MinIO bucket
- Use library item on Tonie ? Verify downloads from S3 and uploads successfully

### 2. Added Storage Preference Infrastructure
**Created**:
- `BoxieHub/Models/UserStoragePreference.cs` - Model for user storage settings
- `BoxieHub/Services/StoragePreferenceService.cs` - Service for managing preferences
- Database migration: `AddUserStoragePreferences`

**Features**:
- Tracks user's default storage provider
- Remembers last used provider
- Auto-creates default preference (S3Railway) on first use
- Checks which providers are available (based on config and connected accounts)

### 3. Updated MediaLibraryService for Provider Selection
**Changes to `MediaLibraryService`**:
- `AddToLibraryAsync()` now accepts `StorageProvider?` and `storageAccountId` parameters
- Falls back to user's default provider if none specified
- Supports Database storage (legacy) and external storage
- Updates last used provider after successful upload
- Proper logging for each provider type

**Registration**: Added `IStoragePreferenceService` to DI container in `ServiceRegistrationExtensions.cs`

## ?? TODO - Storage Provider Selection UI

### Phase 1: Update Library Upload Page
File: `BoxieHub/Components/Pages/Library/Upload.razor`

**Add to @code section**:
```csharp
private List<StorageProvider> availableProviders = new();
private StorageProvider selectedProvider = StorageProvider.S3Railway;
```

**Add to OnInitializedAsync**:
```csharp
protected override async Task OnInitializedAsync()
{
    var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
    var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    
    if (!string.IsNullOrEmpty(userId))
    {
        availableProviders = await StoragePreferenceService.GetAvailableProvidersAsync(userId);
        selectedProvider = await StoragePreferenceService.GetDefaultProviderAsync(userId);
    }
}
```

**Add UI after file selection** (around line 65):
```razor
<!-- Storage Provider Selection -->
<div class="mb-3">
    <label class="form-label">Storage Provider</label>
    <select @bind="selectedProvider" class="form-select" disabled="@isUploading">
        @foreach (var provider in availableProviders)
        {
            <option value="@provider">
                @GetProviderDisplayName(provider)
            </option>
        }
    </select>
    <div class="form-text">
        @GetProviderDescription(selectedProvider)
    </div>
</div>
```

**Add helper methods**:
```csharp
private string GetProviderDisplayName(StorageProvider provider) => provider switch
{
    StorageProvider.Database => "?? Database (Legacy)",
    StorageProvider.S3Railway => "?? S3 Cloud Storage (Recommended)",
    StorageProvider.Dropbox => "?? Dropbox",
    StorageProvider.GoogleDrive => "?? Google Drive",
    _ => provider.ToString()
};

private string GetProviderDescription(StorageProvider provider) => provider switch
{
    StorageProvider.Database => "Stores files in the database. Limited to small files.",
    StorageProvider.S3Railway => "Stores files in S3-compatible cloud storage (MinIO/Railway). Best for large files.",
    StorageProvider.Dropbox => "Stores files in your connected Dropbox account.",
    StorageProvider.GoogleDrive => "Stores files in your connected Google Drive.",
    _ => ""
};
```

**Update HandleUpload method** to pass provider:
```csharp
// Upload to library (pass selected provider)
var item = await MediaLibraryService.AddToLibraryAsync(
    userId, 
    stream, 
    dto,
    selectedProvider,
    null); // storageAccountId (future: for Dropbox/GDrive)
```

### Phase 2: Add Storage Badge to Library Items
File: `BoxieHub/Components/Pages/Library/Components/LibraryItemCard.razor`

**Add badge showing storage provider** (in card header):
```razor
<div class="card-header d-flex justify-content-between align-items-center">
    <strong>@Item.Title</strong>
    <span class="badge @GetProviderBadgeClass(Item.FileUpload?.Provider ?? StorageProvider.Database)">
        @GetProviderIcon(Item.FileUpload?.Provider ?? StorageProvider.Database)
    </span>
</div>
```

**Add helper methods**:
```csharp
private string GetProviderBadgeClass(StorageProvider provider) => provider switch
{
    StorageProvider.Database => "bg-secondary",
    StorageProvider.S3Railway => "bg-primary",
    StorageProvider.Dropbox => "bg-info",
    StorageProvider.GoogleDrive => "bg-success",
    _ => "bg-secondary"
};

private string GetProviderIcon(StorageProvider provider) => provider switch
{
    StorageProvider.Database => "?? DB",
    StorageProvider.S3Railway => "?? S3",
    StorageProvider.Dropbox => "?? Dropbox",
    StorageProvider.GoogleDrive => "?? GDrive",
    _ => "?"
};
```

### Phase 3: Create Storage Settings Page
File: `BoxieHub/Components/Pages/Storage/Settings.razor`

**Content**:
```razor
@page "/storage/settings"
@attribute [Authorize]
@inject IStoragePreferenceService StoragePreferenceService
@inject AuthenticationStateProvider AuthStateProvider

<PageTitle>Storage Settings - BoxieHub</PageTitle>

<div class="container mt-4">
    <h2><i class="bi bi-hdd"></i> Storage Settings</h2>
    
    <div class="card mt-4">
        <div class="card-header">
            <h5 class="mb-0">Default Storage Provider</h5>
        </div>
        <div class="card-body">
            <div class="mb-3">
                <label class="form-label">Select your preferred storage provider for new uploads:</label>
                <select @bind="defaultProvider" class="form-select" @onchange="SaveDefaultProvider">
                    @foreach (var provider in availableProviders)
                    {
                        <option value="@provider">@GetProviderDisplayName(provider)</option>
                    }
                </select>
            </div>
            
            @if (!string.IsNullOrEmpty(statusMessage))
            {
                <div class="alert alert-success">
                    <i class="bi bi-check-circle"></i> @statusMessage
                </div>
            }
        </div>
    </div>
    
    <div class="card mt-4">
        <div class="card-header">
            <h5 class="mb-0">Storage Providers</h5>
        </div>
        <div class="card-body">
            <div class="list-group">
                @foreach (var provider in availableProviders)
                {
                    <div class="list-group-item d-flex justify-content-between align-items-center">
                        <div>
                            <h6 class="mb-1">@GetProviderDisplayName(provider)</h6>
                            <small class="text-muted">@GetProviderDescription(provider)</small>
                        </div>
                        @if (provider == defaultProvider)
                        {
                            <span class="badge bg-success">Default</span>
                        }
                    </div>
                }
            </div>
        </div>
    </div>
</div>

@code {
    private List<StorageProvider> availableProviders = new();
    private StorageProvider defaultProvider = StorageProvider.S3Railway;
    private string? statusMessage;
    
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (!string.IsNullOrEmpty(userId))
        {
            availableProviders = await StoragePreferenceService.GetAvailableProvidersAsync(userId);
            defaultProvider = await StoragePreferenceService.GetDefaultProviderAsync(userId);
        }
    }
    
    private async Task SaveDefaultProvider(ChangeEventArgs e)
    {
        if (Enum.TryParse<StorageProvider>(e.Value?.ToString(), out var provider))
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var userId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            if (!string.IsNullOrEmpty(userId))
            {
                await StoragePreferenceService.SetDefaultProviderAsync(userId, provider);
                statusMessage = $"Default storage provider updated to {GetProviderDisplayName(provider)}";
                StateHasChanged();
                
                await Task.Delay(3000);
                statusMessage = null;
                StateHasChanged();
            }
        }
    }
    
    // Helper methods (same as Library Upload)
    private string GetProviderDisplayName(StorageProvider provider) => provider switch
    {
        StorageProvider.Database => "?? Database (Legacy)",
        StorageProvider.S3Railway => "?? S3 Cloud Storage (Recommended)",
        StorageProvider.Dropbox => "?? Dropbox",
        StorageProvider.GoogleDrive => "?? Google Drive",
        _ => provider.ToString()
    };
    
    private string GetProviderDescription(StorageProvider provider) => provider switch
    {
        StorageProvider.Database => "Stores files in the database. Limited to small files.",
        StorageProvider.S3Railway => "Stores files in S3-compatible cloud storage. Best for large files.",
        StorageProvider.Dropbox => "Stores files in your connected Dropbox account.",
        StorageProvider.GoogleDrive => "Stores files in your connected Google Drive.",
        _ => ""
    };
}
```

### Phase 4: Add Navigation Link
File: `BoxieHub/Components/Layout/NavMenu.razor`

**Add after Library link**:
```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="storage/settings">
        <span class="bi bi-hdd-rack nav-icon" aria-hidden="true"></span> Storage
    </NavLink>
</div>
```

## ?? Testing Checklist

- [ ] Run migration: `dotnet ef database update`
- [ ] Upload file to library (default S3) ? Check MinIO bucket
- [ ] Use library item on Tonie ? Verify downloads and uploads
- [ ] Change default provider in settings ? Verify saves
- [ ] Upload another file ? Verify uses new default
- [ ] View library items ? Verify storage badges appear
- [ ] Delete library item ? Verify deletes from S3

## ?? Database Migration

Already created: `AddUserStoragePreferences`

**Apply migration**:
```bash
cd BoxieHub
dotnet ef database update
```

## ?? Key Benefits

1. **Multi-provider support** - Users can choose where to store files
2. **User preferences** - Remember favorite storage provider
3. **Transparency** - Users see where each file is stored
4. **Flexibility** - Easy to add new storage providers (Dropbox, GDrive)
5. **Organization** - S3 paths are well-structured: `users/{userId}/{guid}/{filename}`

## ?? Industry Standards Met

? **S3 Object Keys**: Using proper hierarchical structure  
? **Provider Abstraction**: `IFileStorageService` works with any provider  
? **Metadata Tracking**: `FileUpload` tracks provider, path, and account  
? **User Control**: Users choose and manage their storage preferences  
? **Backward Compatible**: Database storage still supported for legacy files
