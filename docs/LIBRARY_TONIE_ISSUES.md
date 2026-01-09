# Library & Tonie Image Issues - Analysis & Fixes

## Issue #1: Custom Tonie Image Not Loading After Upload ?

### Problem
After uploading a custom image for a Tonie:
1. Upload succeeds (image is saved to database)
2. Modal closes
3. Page tries to refresh via `LoadTonieDetails()`
4. Image still shows star icon instead of custom image

### Root Cause
The `LoadTonieDetails()` method retrieves the Tonie from the cache/database, which now has `CustomImageId` set. However, the `CharacterToDto()` conversion uses `character.DisplayImageUrl`, which should return the custom image URL.

Looking at the `Character` model, `DisplayImageUrl` should be:
```csharp
public string DisplayImageUrl => CustomImageId.HasValue 
    ? $"/uploads/{CustomImageId}" 
    : ImageUrl ?? string.Empty;
```

### Solution
Force a **database context reload** after custom image upload to ensure navigation properties are loaded:

```csharp
// In Details.razor UploadCustomImage method, after success:
await LoadTonieDetails(); // This should work, but may need forceRefresh

// Better: Force refresh from API which will reload from database
tonie = await TonieService.GetCreativeTonieDetailsAsync(
    userId, HouseholdId, TonieId, 
    forceRefresh: true); // Force DB reload
```

### Why It's Not Working
The `GetCreativeTonieDetailsAsync` without `forceRefresh` returns cached data. Even with `forceRefresh`, it fetches from Tonie Cloud API which doesn't have custom image info (custom images are local only).

**The Real Fix**: Need to ensure local database refresh happens, not API sync.

---

## Issue #2: Library Upload is User-Scoped ? CORRECT BEHAVIOR

### Analysis
**This is intentional and correct!**

From the MinIO screenshot: `users/fb74fd61-f734-4eca-87f9-1f70863924cc/12743ddc-53d2-452a-8cb6-58dee3c04f48/call_to_arms.wav`

### Why User-Scoped Storage is Correct:
1. **Reusability**: Same audio file can be used on multiple Tonies
2. **Efficiency**: Don't duplicate the same file across households
3. **User Ownership**: User owns their library, not tied to specific Tonie
4. **Privacy**: User's files are isolated from other users

### How It Works:
```
User uploads "Bedtime Story.mp3" to library
  ?
Stored in MinIO: users/{userId}/{guid}/bedtime-story.mp3
  ?
User can use this file on:
  - Tonie A in Household 1
  - Tonie B in Household 1
  - Tonie C in Household 2
  ?
File stored ONCE, used MANY times
```

### Tracking Usage
The `MediaLibraryUsage` table tracks where each library item is used:
```csharp
public class MediaLibraryUsage
{
    public int MediaLibraryItemId { get; set; }  // Which library file
    public string HouseholdId { get; set; }       // Which household
    public string TonieId { get; set; }            // Which Tonie
    public string ChapterId { get; set; }          // Which chapter
    public DateTimeOffset UsedAt { get; set; }    // When used
}
```

**Verdict**: ? **No changes needed - working as designed!**

---

## Issue #3: "Use on Tonie" Button Poor UX ?

### Current Behavior
Clicking "Use on Tonie" on a library item just navigates to `/tonies` page. User has to:
1. Remember what file they wanted to use
2. Browse to the Tonie
3. Click Upload
4. Select from library again

### Problem
This creates a **disjointed user experience** and loses context.

### Better UX Options

#### Option A: Navigate to Specific Tonie Upload Page (Quick Win)
Add a Tonie selector modal on library details page:

```razor
<!-- Tonie Selector Modal -->
@if (showTonieSelector)
{
    <div class="modal show d-block">
        <div class="modal-dialog">
            <div class="modal-content">
                <div class="modal-header">
                    <h5>Select a Tonie</h5>
                    <button class="btn-close" @onclick="() => showTonieSelector = false"></button>
                </div>
                <div class="modal-body">
                    @if (availableTonies.Any())
                    {
                        <div class="list-group">
                            @foreach (var tonie in availableTonies)
                            {
                                <button class="list-group-item list-group-item-action"
                                        @onclick="() => NavigateToTonieUpload(tonie)">
                                    <img src="@tonie.ImageUrl" style="width: 40px; height: 40px;" />
                                    @tonie.Name
                                    <small class="text-muted">
                                        @tonie.ChaptersPresent chapters
                                    </small>
                                </button>
                            }
                        </div>
                    }
                    else
                    {
                        <p>No Tonies available. Please sync your Tonies first.</p>
                    }
                </div>
            </div>
        </div>
    </div>
}
```

```csharp
@code {
    private bool showTonieSelector = false;
    private List<CreativeTonieDto> availableTonies = new();
    
    private async Task ShowTonieSelector()
    {
        showTonieSelector = true;
        // Load tonies
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            var (tonies, _) = await TonieService.GetUserCreativeTonieAsync(userId);
            availableTonies = tonies;
        }
    }
    
    private void NavigateToTonieUpload(CreativeTonieDto tonie)
    {
        // Navigate with query parameter to auto-select this library item
        Navigation.NavigateTo(
            $"/tonies/{tonie.HouseholdId}/{tonie.Id}/upload?libraryItemId={item.Id}");
    }
}
```

#### Option B: Direct Upload from Library (Better)
Add a service method to upload library item directly to Tonie, showing progress modal:

```csharp
// In Details.razor
private async Task UseOnTonie(CreativeTonieDto tonie)
{
    isUploading = true;
    StateHasChanged();
    
    try
    {
        // Get user ID
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Download from storage
        var fileUpload = item.FileUpload;
        Stream stream;
        
        if (fileUpload.Provider == StorageProvider.Database && fileUpload.Data != null)
        {
            stream = new MemoryStream(fileUpload.Data);
        }
        else
        {
            stream = await FileStorageService.DownloadFileAsync(
                fileUpload.StoragePath, 
                fileUpload.UserStorageAccountId);
        }
        
        // Upload to Tonie
        var result = await TonieService.UploadAudioToTonieAsync(
            userId!,
            tonie.HouseholdId,
            tonie.Id,
            stream,
            item.Title); // Use library item title as chapter title
        
        if (result.Success)
        {
            successMessage = $"Successfully uploaded to {tonie.Name}!";
            // Track usage
            await MediaLibraryService.TrackUsageAsync(
                item.Id, tonie.HouseholdId, tonie.Id, 
                "chapter-id-here", tonie.Name, item.Title);
        }
        else
        {
            errorMessage = result.Message;
        }
    }
    catch (Exception ex)
    {
        errorMessage = $"Upload failed: {ex.Message}";
    }
    finally
    {
        isUploading = false;
    }
}
```

---

## Recommended Implementation Order

### Priority 1: Fix Custom Image Not Loading (Critical UX Bug)
**Files to Change**: `BoxieHub/Components/Pages/Tonies/Details.razor`

**Change**:
```csharp
private async Task UploadCustomImage()
{
    // ... existing upload code ...
    
    if (success)
    {
        ShowSuccess("Custom image uploaded successfully!");
        
        // FORCE database reload (not API sync)
        await LoadTonieDetails();
        
        // Also force a StateHasChanged to re-render
        await InvokeAsync(StateHasChanged);
        
        // Close modal
        showImageUploadModal = false;
        // ... rest of reset code ...
    }
}
```

### Priority 2: Improve "Use on Tonie" UX (User Experience)
**Files to Change**: `BoxieHub/Components/Pages/Library/Details.razor`

**Approach**: Add Tonie selector modal (Option A - Quick Win)

### Priority 3: Documentation (Explain User-Scoped Library)
Add help text to library pages explaining the storage model.

---

## Testing Checklist

### Custom Image Fix
- [ ] Upload custom image for Tonie
- [ ] Verify image updates immediately
- [ ] Refresh page - verify image persists
- [ ] Delete custom image - verify reverts to default

### Tonie Selector
- [ ] Click "Use on Tonie" from library item
- [ ] Modal shows list of Tonies
- [ ] Select Tonie - navigate to upload page
- [ ] Library item pre-selected on upload page
- [ ] Upload succeeds

---

## Summary

1. ? **Custom Image Not Loading**: Needs force refresh after upload
2. ? **User-Scoped Library**: Working as designed - this is correct!
3. ? **Use on Tonie UX**: Needs Tonie selector modal or direct upload
