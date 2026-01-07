# User Story 2: Creative Tonies Management

## Story
**As a** BoxieHub user  
**I want to** view and manage my Creative Tonies from the Tonie Cloud  
**So that** I can see my Tonies, their content, and manage chapters without leaving BoxieHub

---

## Acceptance Criteria

### 1. View Creative Tonies Dashboard ?
- [ ] Load all Creative Tonies from linked Tonie Cloud account(s)
- [ ] Display Tonies in a visual card grid with:
  - Tonie image
  - Tonie name
  - Content stats (chapters, duration, storage)
  - Last updated timestamp
- [ ] Show real statistics on the dashboard:
  - Total number of Tonies
  - Total number of chapters across all Tonies
  - Total content duration in hours/minutes
- [ ] Handle loading states and errors gracefully
- [ ] Support multiple households (if user has more than one)

### 2. View Tonie Details ?
- [ ] Click on a Tonie to view detailed information
- [ ] Display all chapters with:
  - Chapter title
  - Duration
  - Upload date
  - Transcoding status
- [ ] Show storage usage (seconds used/remaining)
- [ ] Display Tonie metadata (household, last sync, etc.)

### 3. Upload Audio to Tonie ??
- [ ] Upload audio files to a Creative Tonie
- [ ] Support multiple audio formats:
  - MP3
  - M4A/AAC
  - OGG/Vorbis
  - WAV
- [ ] Validate file before upload:
  - Check file size (max 90 minutes per chapter)
  - Check available storage on Tonie
- [ ] Show upload progress with:
  - Progress bar
  - Upload speed
  - Time remaining estimate
- [ ] Handle upload errors:
  - Network failures
  - Storage full
  - Invalid file format
  - Transcoding failures

### 4. Manage Chapters ??
- [ ] Delete chapters from a Tonie (with confirmation)
- [ ] View chapter details:
  - File size
  - Duration
  - Transcoding status
- [ ] Handle transcoding states:
  - Show "Processing..." for active transcoding
  - Show error if transcoding failed
  - Display successful transcoding

### 5. Refresh Data ??
- [ ] Manual refresh button to reload Tonie data
- [ ] Automatic refresh after upload/delete operations
- [ ] Optimistic UI updates (show changes immediately)

---

## Technical Implementation Plan

### Phase 1: Data Loading & Display (Core)

#### 1.1 Service Layer
```csharp
// BoxieHub/Services/TonieService.cs
public interface ITonieService
{
    Task<List<HouseholdDto>> GetHouseholdsAsync(string userId);
    Task<List<CreativeTonieDto>> GetCreativeToniesByHouseholdAsync(string userId, string householdId);
    Task<CreativeTonieDto> GetCreativeTonieDetailsAsync(string userId, string householdId, string tonieId);
    Task<TonieStats> GetUserStatsAsync(string userId);
}
```

#### 1.2 View Models
```csharp
// BoxieHub/Models/ViewModels/TonieStatsViewModel.cs
public class TonieStatsViewModel
{
    public int TotalTonies { get; set; }
    public int TotalChapters { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public float TotalSecondsUsed { get; set; }
    public float TotalSecondsAvailable { get; set; }
}
```

#### 1.3 Blazor Components
- `Index.razor` - Update with real stats
- `TonieCard.razor` - Individual Tonie display card
- `TonieGrid.razor` - Grid layout of Tonie cards
- `TonieDetails.razor` - Full Tonie detail page with chapters

### Phase 2: Chapter Management

#### 2.1 Upload Component
```razor
// BoxieHub/Components/Pages/Tonies/UploadAudio.razor
- Drag & drop zone
- File picker
- Progress indicator
- Success/error feedback
```

#### 2.2 Chapter List Component
```razor
// BoxieHub/Components/Pages/Tonies/Components/ChapterList.razor
- List of chapters
- Delete button with confirmation
- Chapter metadata display
- Transcoding status indicators
```

### Phase 3: Error Handling & Polish

#### 3.1 Error States
- Network errors
- Authentication failures
- Storage full errors
- Transcoding failures
- Invalid file formats

#### 3.2 Loading States
- Skeleton loaders for cards
- Progress indicators
- Optimistic UI updates

---

## API Integration Points

### Existing API Client (`BoxieCloudClient.cs`)
? Already implemented:
- `GetHouseholdsAsync()` - Load user's households
- `GetCreativeToniesByHouseholdAsync()` - Load Tonies for household
- `GetCreativeTonieDetailsAsync()` - Get Tonie with chapters
- `GetUploadTokenAsync()` - Get S3 upload credentials
- `PatchCreativeTonieAsync()` - Update Tonie with new chapters
- `SyncAudioAsync()` - Full upload workflow

### S3 Storage Service (`S3StorageService.cs`)
? Already implemented:
- `UploadFileAsync()` - Upload to S3 with presigned URL

---

## Database Changes

### New Tables/Models (if needed)

#### TonieCache (Optional - for performance)
```csharp
public class TonieCache
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public string TonieId { get; set; }
    public string HouseholdId { get; set; }
    public string CachedData { get; set; } // JSON
    public DateTimeOffset CachedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
```

#### UploadHistory (Optional - for tracking)
```csharp
public class UploadHistory
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public string TonieId { get; set; }
    public string FileName { get; set; }
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } // Success, Failed, InProgress
    public DateTimeOffset UploadedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
```

---

## UI/UX Design

### Dashboard Layout
```
???????????????????????????????????????????????????????????
?  My Creative Tonies            [Add Tonie Account] [?] ?
???????????????????????????????????????????????????????????
? ???????????????????  ????????????????????????????????? ?
? ? Linked Accounts ?  ?      Quick Stats              ? ?
? ? [1] oakes.a@..  ?  ?  ?? 5 Tonies  ?? 23 Chapters ? ?
? ???????????????????  ?  ?? 2h 15m content           ? ?
?                      ????????????????????????????????? ?
???????????????????????????????????????????????????????????
?                                                         ?
?  [Search Tonies...]                    [Grid] [List]   ?
?                                                         ?
?  ????????????  ????????????  ????????????            ?
?  ? ??       ?  ? ??       ?  ? ??       ?            ?
?  ? Pirate   ?  ? Princess ?  ? Creative ?            ?
?  ? Stories  ?  ? Tales    ?  ? Tonie 1  ?            ?
?  ?          ?  ?          ?  ?          ?            ?
?  ? 5 chap.  ?  ? 8 chap.  ?  ? 10 chap. ?            ?
?  ? 45min    ?  ? 1h 20m   ?  ? 2h 10m   ?            ?
?  ????????????  ????????????  ????????????            ?
?                                                         ?
???????????????????????????????????????????????????????????
```

### Tonie Detail Page
```
???????????????????????????????????????????????????????????
?  ? Back to Tonies                                       ?
???????????????????????????????????????????????????????????
?  ??????????  Pirate Stories                            ?
?  ?  ??    ?  Last updated: Jan 5, 2026                 ?
?  ?        ?  Storage: 45/90 min used (50%)             ?
?  ??????????                                             ?
?                                                         ?
?  [Upload Audio]  [Refresh]                             ?
?                                                         ?
?  Chapters (5)                                           ?
?  ???????????????????????????????????????????????????  ?
?  ? 1. Introduction        [2:30]  [??? Delete]     ?  ?
?  ? 2. Treasure Hunt       [8:45]  [??? Delete]     ?  ?
?  ? 3. Storm at Sea        [12:15] [??? Delete]     ?  ?
?  ? 4. Island Discovery    [10:30] [??? Delete]     ?  ?
?  ? 5. Final Battle        [11:00] [??? Delete]     ?  ?
?  ???????????????????????????????????????????????????  ?
???????????????????????????????????????????????????????????
```

---

## Testing Plan

### Unit Tests
- [ ] `TonieService` - All methods
- [ ] View model transformations
- [ ] Stats calculations
- [ ] File validation logic

### Integration Tests
- [ ] Load households from API
- [ ] Load Tonies from API
- [ ] Upload workflow (mocked S3)
- [ ] Delete chapter workflow

### Component Tests (bUnit)
- [ ] `TonieCard` rendering
- [ ] `TonieGrid` with multiple Tonies
- [ ] `ChapterList` display
- [ ] Upload progress UI
- [ ] Delete confirmation modal

### End-to-End Tests
- [ ] Full user flow: Login ? View Tonies ? Upload Chapter
- [ ] Error scenarios (network failure, storage full)
- [ ] Multi-household support

---

## Performance Considerations

### Optimization Strategies
1. **Caching**
   - Cache Tonie data for 5 minutes
   - Invalidate cache on upload/delete
   - Use in-memory cache (IMemoryCache)

2. **Lazy Loading**
   - Load images lazily as user scrolls
   - Defer loading of detailed chapter data

3. **Pagination**
   - Show first 20 Tonies
   - Load more on scroll (infinite scroll)

4. **Optimistic Updates**
   - Show upload immediately in UI
   - Rollback if API call fails

---

## Security Considerations

### Data Access Control
- ? Only load Tonies for authenticated user
- ? Use default Tonie account credentials
- ? Validate user owns the household before operations
- ? Encrypt cached data if storing sensitive info

### File Upload Security
- ? Validate file types (whitelist: mp3, m4a, ogg, wav)
- ? Validate file size (max 90 minutes)
- ? Use presigned S3 URLs (short expiry)
- ? Sanitize file names
- ? Scan for malware (if applicable)

---

## Success Metrics

### User Story 2 is complete when:
- [ ] User can view all their Creative Tonies
- [ ] Dashboard shows accurate statistics
- [ ] User can upload audio files to a Tonie
- [ ] User can delete chapters from a Tonie
- [ ] Upload progress is visible and accurate
- [ ] All errors are handled gracefully with user feedback
- [ ] **80%+ test coverage** (unit + integration + E2E)
- [ ] **Performance**: Page loads in <2 seconds
- [ ] **Accessibility**: Keyboard navigation works
- [ ] **Responsive**: Works on mobile/tablet/desktop

---

## Dependencies

### From User Story 1 (Already Complete) ?
- `IBoxieAuthService` - OAuth authentication
- `IBoxieCloudClient` - Tonie Cloud API client
- `IS3StorageService` - S3 uploads
- `ICredentialEncryptionService` - Secure credential storage
- `TonieCredential` model - Stored account credentials

### External Dependencies
- **Tonie Cloud API** - `https://api.tonie.cloud/v2/`
- **AWS S3** - Audio file storage
- **Bootstrap 5** - UI components
- **Bootstrap Icons** - Icons

---

## Estimated Effort

| Phase | Effort | Priority |
|-------|--------|----------|
| **Phase 1: Data Loading & Display** | 4-6 hours | ?? High |
| **Phase 2: Chapter Management** | 3-4 hours | ?? High |
| **Phase 3: Error Handling & Polish** | 2-3 hours | ?? Medium |
| **Testing** | 3-4 hours | ?? High |
| **Total** | **12-17 hours** | |

---

## Risks & Mitigation

### Risk 1: API Rate Limiting
**Mitigation:** Implement caching, batch requests, respect rate limits

### Risk 2: Large File Uploads Timing Out
**Mitigation:** Chunk uploads if needed, show clear progress, allow resume

### Risk 3: Transcoding Failures
**Mitigation:** Clear error messages, allow retry, validate files before upload

### Risk 4: Multiple Households Complexity
**Mitigation:** Default to first household, allow switching in settings

---

## Future Enhancements (Post-MVP)

- [ ] Drag & drop chapter reordering
- [ ] Bulk upload (multiple files at once)
- [ ] Audio preview player
- [ ] Edit chapter titles inline
- [ ] Share Tonies between households
- [ ] Export/backup Tonie content
- [ ] Audio editor (trim, normalize, etc.)
- [ ] Integration with Spotify/YouTube for content

---

## Ready to Start! ??

**Current Branch:** `feature/user-story-2-manage-tonies`

**First Task:** Implement Phase 1 - Data Loading & Display
