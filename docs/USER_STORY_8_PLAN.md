# User Story 8: Media Library Management - Implementation Plan

## Overview
Build a media library system where users can store, organize, and reuse audio files across multiple Tonies.

---

## Database Schema

### New/Updated Models

#### 1. MediaLibraryItem (New)
```csharp
public class MediaLibraryItem
{
    public int Id { get; set; }
    public string UserId { get; set; } // Owner
    public string Title { get; set; }
    public string? Description { get; set; }
    
    // File reference
    public Guid FileUploadId { get; set; }
    public FileUpload FileUpload { get; set; }
    
    // Metadata
    public float DurationSeconds { get; set; }
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; }
    
    // Organization
    public string? Tags { get; set; } // JSON array of tags
    public string? Category { get; set; } // "Music", "Story", "Educational", etc.
    
    // Tracking
    public int UseCount { get; set; } // How many times used
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? LastUsed { get; set; }
    
    // Navigation
    public ApplicationUser User { get; set; }
}
```

#### 2. MediaLibraryUsage (New) - Track where media is used
```csharp
public class MediaLibraryUsage
{
    public int Id { get; set; }
    public int MediaLibraryItemId { get; set; }
    public MediaLibraryItem MediaLibraryItem { get; set; }
    
    public string HouseholdId { get; set; }
    public string TonieId { get; set; }
    public string ChapterId { get; set; }
    
    public DateTimeOffset UsedAt { get; set; }
}
```

---

## Features to Implement

### Phase 1: Core Library (MVP)
1. **Library Dashboard** (`/library`)
   - List all media items
   - Search by title
   - Filter by category
   - Sort by date, name, duration
   - Display: thumbnail, title, duration, file size, tags

2. **Upload to Library** (`/library/upload`)
   - Similar to Tonie upload but saves to library
   - Auto-detect metadata (duration, size)
   - Add title, description, tags, category
   - No Tonie selection required

3. **Library Item Details** (`/library/{id}`)
   - View all metadata
   - Edit title, description, tags, category
   - Play audio preview
   - See where it's used (which Tonies)
   - Delete from library

4. **Use from Library** (Enhanced Upload page)
   - On Tonie upload page: "Use from Library" button
   - Modal with library browser
   - Select item ? Auto-fills chapter title
   - Upload to Tonie

### Phase 2: Enhanced Features
5. **Batch Operations**
   - Select multiple items
   - Bulk delete
   - Bulk tag editing
   - Bulk category assignment

6. **Audio Preview**
   - In-browser audio player
   - Waveform visualization
   - Play/pause controls

7. **Statistics**
   - Total library size
   - Most used items
   - Category breakdown
   - Storage usage

---

## API Endpoints

### LibraryService (New)
```csharp
public interface IMediaLibraryService
{
    // CRUD
    Task<List<MediaLibraryItem>> GetUserLibraryAsync(string userId, CancellationToken ct = default);
    Task<MediaLibraryItem?> GetLibraryItemAsync(int id, string userId, CancellationToken ct = default);
    Task<MediaLibraryItem> AddToLibraryAsync(string userId, Stream audioStream, MediaLibraryItemDto dto, CancellationToken ct = default);
    Task<bool> UpdateLibraryItemAsync(int id, string userId, MediaLibraryItemDto dto, CancellationToken ct = default);
    Task<bool> DeleteLibraryItemAsync(int id, string userId, CancellationToken ct = default);
    
    // Search & Filter
    Task<List<MediaLibraryItem>> SearchLibraryAsync(string userId, string query, CancellationToken ct = default);
    Task<List<MediaLibraryItem>> GetLibraryByCategoryAsync(string userId, string category, CancellationToken ct = default);
    
    // Usage tracking
    Task TrackUsageAsync(int mediaLibraryItemId, string householdId, string tonieId, string chapterId, CancellationToken ct = default);
    Task<List<MediaLibraryUsage>> GetItemUsageAsync(int mediaLibraryItemId, CancellationToken ct = default);
    
    // Statistics
    Task<LibraryStatsDto> GetLibraryStatsAsync(string userId, CancellationToken ct = default);
}
```

---

## UI Components

### 1. Library Index Page (`/library`)
```razor
@page "/library"
@attribute [Authorize]

<PageTitle>Media Library - BoxieHub</PageTitle>

<div class="container">
    <div class="d-flex justify-content-between">
        <h2>My Media Library</h2>
        <div>
            <a href="/library/upload" class="btn btn-primary">
                <i class="bi bi-upload"></i> Add to Library
            </a>
        </div>
    </div>
    
    <!-- Search & Filter -->
    <div class="row my-3">
        <div class="col-md-6">
            <input type="text" class="form-control" placeholder="Search..." @bind="searchQuery" @bind:event="oninput" />
        </div>
        <div class="col-md-3">
            <select class="form-select" @bind="selectedCategory">
                <option value="">All Categories</option>
                <option value="Music">Music</option>
                <option value="Story">Story</option>
                <option value="Educational">Educational</option>
            </select>
        </div>
        <div class="col-md-3">
            <select class="form-select" @bind="sortBy">
                <option value="recent">Recently Added</option>
                <option value="name">Name</option>
                <option value="duration">Duration</option>
                <option value="mostUsed">Most Used</option>
            </select>
        </div>
    </div>
    
    <!-- Library Grid -->
    <div class="row">
        @foreach (var item in filteredItems)
        {
            <div class="col-md-4 col-lg-3 mb-4">
                <LibraryItemCard Item="@item" OnSelect="@(() => NavigateToDetails(item.Id))" />
            </div>
        }
    </div>
</div>
```

### 2. Library Upload Page (`/library/upload`)
```razor
@page "/library/upload"
@attribute [Authorize]

<PageTitle>Add to Library - BoxieHub</PageTitle>

<div class="container">
    <h2>Add Audio to Library</h2>
    
    <div class="card">
        <div class="card-body">
            <!-- File Selection -->
            <div class="mb-3">
                <label class="form-label">Audio File *</label>
                <InputFile OnChange="HandleFileSelected" accept=".mp3,.m4a,.ogg,.wav" />
            </div>
            
            <!-- Metadata -->
            <div class="mb-3">
                <label class="form-label">Title *</label>
                <input type="text" class="form-control" @bind="title" />
            </div>
            
            <div class="mb-3">
                <label class="form-label">Description</label>
                <textarea class="form-control" @bind="description" rows="3"></textarea>
            </div>
            
            <div class="mb-3">
                <label class="form-label">Category</label>
                <select class="form-select" @bind="category">
                    <option value="">-- Select --</option>
                    <option value="Music">Music</option>
                    <option value="Story">Story</option>
                    <option value="Educational">Educational</option>
                </select>
            </div>
            
            <div class="mb-3">
                <label class="form-label">Tags (comma separated)</label>
                <input type="text" class="form-control" @bind="tags" placeholder="children, bedtime, soothing" />
            </div>
            
            <button class="btn btn-primary" @onclick="UploadToLibrary" disabled="@isUploading">
                <i class="bi bi-upload"></i> Add to Library
            </button>
        </div>
    </div>
</div>
```

### 3. Library Item Card (Component)
```razor
<div class="card library-item-card" @onclick="OnSelect">
    <div class="card-body">
        <div class="d-flex align-items-start">
            <div class="flex-grow-1">
                <h6 class="card-title">@Item.Title</h6>
                <small class="text-muted">
                    <i class="bi bi-clock"></i> @FormatDuration(Item.DurationSeconds)
                    · @FormatFileSize(Item.FileSizeBytes)
                </small>
                @if (!string.IsNullOrEmpty(Item.Category))
                {
                    <span class="badge bg-secondary mt-2">@Item.Category</span>
                }
            </div>
            <div class="dropdown">
                <button class="btn btn-sm btn-link" data-bs-toggle="dropdown">
                    <i class="bi bi-three-dots-vertical"></i>
                </button>
                <ul class="dropdown-menu">
                    <li><a class="dropdown-item" href="/library/@Item.Id"><i class="bi bi-eye"></i> View</a></li>
                    <li><a class="dropdown-item" @onclick:preventDefault @onclick="() => OnPlay?.Invoke(Item)"><i class="bi bi-play"></i> Preview</a></li>
                    <li><a class="dropdown-item" @onclick:preventDefault @onclick="() => OnDelete?.Invoke(Item)"><i class="bi bi-trash"></i> Delete</a></li>
                </ul>
            </div>
        </div>
        
        @if (Item.UseCount > 0)
        {
            <div class="mt-2">
                <small class="text-muted">
                    <i class="bi bi-disc"></i> Used @Item.UseCount time(s)
                </small>
            </div>
        }
    </div>
</div>
```

### 4. Library Browser Modal (for Tonie Upload)
```razor
<!-- Add to Upload.razor -->
<button class="btn btn-outline-primary" @onclick="() => showLibraryBrowser = true">
    <i class="bi bi-collection"></i> Use from Library
</button>

@if (showLibraryBrowser)
{
    <div class="modal fade show d-block">
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                <div class="modal-header">
                    <h5>Select from Library</h5>
                    <button type="button" class="btn-close" @onclick="() => showLibraryBrowser = false"></button>
                </div>
                <div class="modal-body">
                    <input type="text" class="form-control mb-3" placeholder="Search library..." @bind="librarySearch" />
                    
                    <div class="list-group">
                        @foreach (var item in libraryItems)
                        {
                            <button class="list-group-item list-group-item-action" @onclick="() => SelectLibraryItem(item)">
                                <div class="d-flex justify-content-between">
                                    <div>
                                        <strong>@item.Title</strong>
                                        <br />
                                        <small class="text-muted">@FormatDuration(item.DurationSeconds) · @item.Category</small>
                                    </div>
                                    <span class="badge bg-info">@item.UseCount uses</span>
                                </div>
                            </button>
                        }
                    </div>
                </div>
            </div>
        </div>
    </div>
}
```

---

## Migration Plan

### Step 1: Database Migration
```bash
dotnet ef migrations add AddMediaLibrary --project BoxieHub
dotnet ef database update --project BoxieHub
```

### Step 2: Create Models & DTOs
- MediaLibraryItem.cs
- MediaLibraryUsage.cs
- MediaLibraryItemDto.cs
- LibraryStatsDto.cs

### Step 3: Implement Service Layer
- IMediaLibraryService interface
- MediaLibraryService implementation
- Register in ServiceRegistrationExtensions

### Step 4: Build UI Components
- Library/Index.razor
- Library/Upload.razor
- Library/Details.razor
- Components/LibraryItemCard.razor
- Components/LibraryBrowserModal.razor

### Step 5: Integrate with Upload Flow
- Modify Upload.razor to include "Use from Library" option
- Track usage when library items are uploaded to Tonies

### Step 6: Add Navigation
- Add "Library" link to main navigation
- Add breadcrumbs

### Step 7: Testing
- Unit tests for MediaLibraryService
- Integration tests for library CRUD
- Component tests for library pages

---

## Success Metrics

- [ ] Users can upload audio files to library
- [ ] Users can search and filter library
- [ ] Users can reuse library items on multiple Tonies
- [ ] Usage tracking shows which Tonies use which library items
- [ ] Statistics dashboard shows library insights
- [ ] 90%+ test coverage for library features

---

## Estimated Timeline

- **Day 1-2**: Database schema, models, DTOs, service layer
- **Day 3-4**: Library index and upload pages
- **Day 5**: Library details and browser modal
- **Day 6**: Integration with Tonie upload
- **Day 7**: Testing and polish

**Total: ~7 days for MVP**

---

## Future Enhancements

- Audio waveform visualization
- Bulk import from folder
- Export library as ZIP
- Share library items with other users
- AI-powered tagging and categorization
- Automatic audio normalization
- Cloud storage integration (S3, Azure Blob)
