# ?? Media Library Roadmap - User Story 8

**Goal:** Build a reusable media library system with external storage support (S3, Dropbox, Google Drive)

---

## ?? **Vision**

Users can:
- Upload audio files to their choice of storage (BoxieHub S3, Dropbox, Google Drive)
- Browse and organize their media library
- Reuse library items across multiple Creative Tonies
- Import audio from external sources (YouTube, Podcasts)
- Share library items within their household
- Track where each item is used

**Key Principle:** 
- ? **NO large files in PostgreSQL** (only metadata + small images)
- ? **All audio files in object storage** (S3/Dropbox/GDrive)

---

## ?? **Feature Breakdown**

### **Phase 1: Storage Infrastructure** ???
**Goal:** Move from database storage to external storage (S3-compatible)

**Estimated Time:** 8-10 hours

#### **1.1 Storage Abstraction Layer**
- [x] `StorageProvider` enum (S3Railway, Dropbox, GoogleDrive)
- [ ] `IFileStorageService` interface
- [ ] `S3FileStorageService` implementation (MinIO/Railway)
- [ ] `DatabaseFileStorageService` (fallback for small images)

#### **1.2 Database Schema Changes**
- [ ] Update `FileUpload` model:
  - `StorageProvider Provider`
  - `string? StoragePath`
  - `int? UserStorageAccountId`
  - `byte[]? Data` (nullable, only for images)
- [ ] Create `UserStorageAccount` model:
  - Track connected external storage accounts
  - OAuth tokens (encrypted)
  - Quota tracking
- [ ] Database migrations

#### **1.3 MinIO Dev Environment**
- [ ] `docker-compose.yml` with MinIO
- [ ] Configuration setup (appsettings.json)
- [ ] S3 client library (AWSSDK.S3 or MinIO SDK)
- [ ] Dev/prod environment parity

**Deliverables:**
- ? MinIO running locally
- ? Files uploaded to S3, not database
- ? Configuration for Railway S3 in production
- ? Migration path for existing files

---

### **Phase 2: Media Library Core** ??
**Goal:** Basic library with upload, view, and search

**Estimated Time:** 10-12 hours

#### **2.1 Upload to Library**
**User Story 8.1**

- [ ] `/library/upload` page
- [ ] File picker with drag & drop
- [ ] Storage provider selection dropdown:
  - BoxieHub S3 (if subscribed)
  - My Dropbox (if connected)
  - My Google Drive (if connected)
- [ ] Form fields:
  - Title (required)
  - Description (optional)
  - Category (Music, Story, Educational, etc.)
  - Tags (comma-separated)
- [ ] Audio duration detection (HTML5 Audio API)
- [ ] File validation (format, size)
- [ ] Upload progress indicator
- [ ] Save to selected storage provider

**UI:**
```
???????????????????????????????????????
? Upload to Library                   ?
???????????????????????????????????????
? [?? Select File] chapter-1.mp3      ?
?                                     ?
? Store in: [My Dropbox ?]           ?
?   ? BoxieHub Storage ?? Pro Only    ?
?   ? My Dropbox (2GB free)           ?
?   ? My Google Drive (12GB free)     ?
?                                     ?
? Title: [Chapter 1]                  ?
? Category: [Music ?]                 ?
? Tags: [kids, bedtime]               ?
?                                     ?
? [Cancel]  [Upload to Library]       ?
???????????????????????????????????????
```

#### **2.2 Library Index Page**
**User Story 8.3**

- [ ] `/library` page
- [ ] Grid/list view toggle
- [ ] Display library items with:
  - Thumbnail/icon
  - Title
  - Duration
  - File size
  - Storage provider badge (S3/Dropbox/GDrive icon)
  - Category badge
  - Tags
  - Use count ("Used 3×")
- [ ] Search bar (filter by title)
- [ ] Category filter dropdown
- [ ] Sort options:
  - Recently Added
  - Name (A-Z)
  - Duration
  - Most Used
- [ ] Pagination or infinite scroll

**UI:**
```
?????????????????????????????????????????????????
? My Media Library        [+ Add to Library]    ?
?????????????????????????????????????????????????
? [?? Search...] [Category: All ?] [Sort ?]    ?
?                                               ?
? ???????????? ???????????? ????????????      ?
? ???        ? ???        ? ???        ?      ?
? ?Chapter 1 ? ?Lullaby   ? ?Story     ?      ?
? ?2:45      ? ?3:12      ? ?8:30      ?      ?
? ?5.2 MB    ? ?6.8 MB    ? ?15 MB     ?      ?
? ??? Dropbox? ??? GDrive ? ??? S3     ?      ?
? ?Music     ? ?Sleep     ? ?Story     ?      ?
? ?Used 3×   ? ?Used 1×   ? ?Used 0×   ?      ?
? ?[+ Add]   ? ?[+ Add]   ? ?[+ Add]   ?      ?
? ???????????? ???????????? ????????????      ?
?????????????????????????????????????????????????
```

#### **2.3 Library Item Details**
**User Story 8.6**

- [ ] `/library/{id}` page or modal
- [ ] Display all metadata
- [ ] Edit metadata button
- [ ] "Used on Tonies" section:
  - List all Tonies using this item
  - Chapter position
  - Date added
  - Navigate to Tonie
- [ ] Delete button with confirmation
- [ ] "Add to Tonie" button

**Deliverables:**
- ? Users can upload audio to library
- ? Files stored in S3/Dropbox/GDrive (not database)
- ? Browse and search library
- ? View usage tracking

---

### **Phase 3: Add Library Item to Tonie** ??
**Goal:** Core feature - Reuse library items on Tonies

**Estimated Time:** 6-8 hours

#### **3.1 Add to Tonie Modal**
**User Story 8.4**

- [ ] Click [+ Add] button on library item
- [ ] Modal: "Add to Creative Tonie"
- [ ] List all user's Creative Tonies
- [ ] Show storage remaining per Tonie
- [ ] Validation:
  - Check if enough space
  - Warn if near capacity
- [ ] Select Tonie ? Click "Add"
- [ ] Backend workflow:
  1. Download file from storage (Dropbox/GDrive/S3)
  2. Upload to Tonie Cloud S3
  3. Update Tonie chapters via API
  4. Track usage in `MediaLibraryUsages`
  5. Increment `UseCount`
- [ ] Success toast notification
- [ ] Navigate to Tonie details

**UI:**
```
???????????????????????????????????????
? Add "Chapter 1" to Creative Tonie   ?
???????????????????????????????????????
? Select a Creative Tonie:            ?
?                                     ?
? ? Pirate Stories                    ?
?   45 min used / 90 min total        ?
?                                     ?
? ? Princess Tales                    ?
?   20 min used / 90 min total        ?
?                                     ?
? ? My Tonie                          ?
?   88 min used / 90 min total ??     ?
?   (Not enough space)                ?
?                                     ?
? [Cancel]  [Add to Tonie]            ?
???????????????????????????????????????
```

#### **3.2 Integration with Tonie Upload Page**
- [ ] Update `/tonies/{householdId}/{tonieId}/upload`
- [ ] Add "Use from Library" button
- [ ] Opens library browser modal
- [ ] Search library items
- [ ] Select item ? Auto-fill chapter title
- [ ] Upload to Tonie (same as 3.1)

**Deliverables:**
- ? Users can add library items to Tonies
- ? Files downloaded from storage and uploaded to Tonie Cloud
- ? Usage tracking functional

---

### **Phase 4: External Storage Connections** ??
**Goal:** Connect Dropbox and Google Drive accounts

**Estimated Time:** 12-16 hours

#### **4.1 Storage Accounts Management**
**User Story 8.2**

- [ ] `/library/storage-accounts` page
- [ ] List connected accounts:
  - Provider icon
  - Account email
  - Storage used / total
  - "Disconnect" button
- [ ] "Connect Dropbox" button ? OAuth flow
- [ ] "Connect Google Drive" button ? OAuth flow
- [ ] Default storage selection

**UI:**
```
???????????????????????????????????????
? Storage Accounts                    ?
???????????????????????????????????????
? ? BoxieHub Storage (S3)             ?
?   500 MB / 10 GB used               ?
?   [Upgrade Plan]                    ?
?                                     ?
? ? Dropbox (oakes@email.com)        ?
?   1.2 GB / 2 GB used                ?
?   [Disconnect]                      ?
?                                     ?
? ? Connect More Storage              ?
?   [Connect Google Drive]            ?
???????????????????????????????????????
```

#### **4.2 Dropbox OAuth Integration**
- [ ] Register Dropbox app (get client ID/secret)
- [ ] `DropboxOAuthController`:
  - `/api/storage/connect/dropbox` ? Redirect to Dropbox
  - `/api/storage/callback/dropbox` ? Handle callback
- [ ] `DropboxStorageService`:
  - Upload file to user's Dropbox
  - Download file from Dropbox
  - Get account info (quota)
- [ ] Store tokens in `UserStorageAccounts` (encrypted)
- [ ] Token refresh logic

#### **4.3 Google Drive OAuth Integration**
- [ ] Register Google Cloud project (get client ID/secret)
- [ ] `GoogleDriveOAuthController`:
  - `/api/storage/connect/googledrive` ? Redirect to Google
  - `/api/storage/callback/googledrive` ? Handle callback
- [ ] `GoogleDriveStorageService`:
  - Upload file to user's Google Drive
  - Download file from Google Drive
  - Get account info (quota)
- [ ] Store tokens in `UserStorageAccounts` (encrypted)
- [ ] Token refresh logic

**NuGet Packages:**
- `Dropbox.Api` - Official Dropbox SDK
- `Google.Apis.Drive.v3` - Official Google Drive API

**Deliverables:**
- ? Users can connect Dropbox accounts
- ? Users can connect Google Drive accounts
- ? Files uploaded to user's personal cloud
- ? OAuth tokens securely stored

---

### **Phase 5: Media Download API** ??
**Goal:** Secure file downloads through BoxieHub API

**Estimated Time:** 3-4 hours

#### **5.1 Media Download Controller**
- [ ] `/api/media/download/{fileId}` endpoint
- [ ] [Authorize] attribute
- [ ] Check user permissions:
  - User owns file, OR
  - File shared with user's household
- [ ] Download from storage provider:
  - S3FileStorageService
  - DropboxStorageService
  - GoogleDriveStorageService
- [ ] Stream file to client
- [ ] Set correct Content-Type header
- [ ] Set Content-Disposition (filename)

**Deliverables:**
- ? Secure file downloads
- ? Permission checking
- ? Works with all storage providers

---

### **Phase 6: Import from External URLs** ??
**Goal:** Rip audio from YouTube, podcasts, etc.

**Estimated Time:** 8-12 hours

#### **6.1 External Media Service**
**User Story 8.5**

- [ ] `/library/import` page
- [ ] URL input field
- [ ] Detect source type:
  - YouTube video
  - Podcast RSS
  - SoundCloud
  - Direct MP3 URL
- [ ] Show preview:
  - Title
  - Duration
  - Thumbnail
- [ ] User edits metadata
- [ ] Select storage destination
- [ ] Backend:
  1. Download/rip audio from source
  2. Convert to MP3 if needed
  3. Upload to chosen storage
  4. Add to library with `SourceUrl` tracking

**UI:**
```
???????????????????????????????????????
? Import from URL                     ?
???????????????????????????????????????
? Source URL:                         ?
? [https://youtube.com/watch?v=...]   ?
?                                     ?
? ? Detected: YouTube Video           ?
?   Title: "Bedtime Story"            ?
?   Duration: 12:34                   ?
?                                     ?
? Store in: [My Dropbox ?]           ?
? Title: [Bedtime Story]              ?
? Category: [Story ?]                 ?
?                                     ?
? [Cancel]  [Import to Library]       ?
???????????????????????????????????????
```

#### **6.2 YouTube Downloader**
- [ ] Install `YoutubeExplode` NuGet package
- [ ] `IYouTubeDownloader` interface
- [ ] `YouTubeDownloader` implementation:
  - Extract video metadata
  - Download audio-only stream
  - Return MP3 stream
- [ ] Error handling (copyright, age-restricted, etc.)

#### **6.3 Podcast Downloader**
- [ ] `IPodcastDownloader` interface
- [ ] `PodcastDownloader` implementation:
  - Parse RSS feed
  - Extract episode metadata
  - Download MP3
- [ ] Support major podcast platforms:
  - Spotify (via RSS)
  - Apple Podcasts
  - Generic RSS feeds

**Deliverables:**
- ? Users can import from YouTube
- ? Users can import from Podcasts
- ? Audio converted to MP3
- ? Uploaded to chosen storage

---

### **Phase 7: Household Sharing** ???????????
**Goal:** Share library items within household

**Estimated Time:** 4-6 hours

#### **7.1 Household Library Sharing**
**User Story 8.11 (Modified)**

- [ ] Add `IsSharedWithHousehold` flag to `MediaLibraryItem`
- [ ] Add `HouseholdId` to `MediaLibraryItem`
- [ ] Library index shows:
  - "My Library" tab
  - "Household Library" tab (if in household)
- [ ] Toggle "Share with household" on item details
- [ ] Household members can:
  - ? View shared items
  - ? Add shared items to their Tonies
  - ? Delete items (only owner can)

**Note:** ? **No public/community sharing** (as requested)

**Deliverables:**
- ? Library items can be shared within household
- ? Household members see shared items
- ? Permission system (owner vs member)

---

### **Phase 8: Library Statistics & Analytics** ??
**Goal:** Insights into library usage

**Estimated Time:** 4-6 hours

#### **8.1 Library Statistics Dashboard**
**User Story 8.7**

- [ ] `/library/stats` page
- [ ] Display:
  - Total items
  - Total storage used (across all providers)
  - Total duration
  - Items by category (pie chart)
  - Most used items
  - Recently added
  - Least used items (candidates for deletion)
- [ ] Storage breakdown by provider:
  - BoxieHub S3: 500 MB / 10 GB
  - Dropbox: 1.2 GB / 2 GB
  - Google Drive: 3.5 GB / 15 GB
- [ ] Upgrade CTA if approaching limits

**UI:**
```
???????????????????????????????????????
? Library Statistics                  ?
???????????????????????????????????????
? Total Items: 24                     ?
? Total Storage: 1.8 GB               ?
? Total Duration: 2h 45m              ?
?                                     ?
? Storage by Provider:                ?
? ?????????? S3: 500 MB / 10 GB      ?
? ?????????? Dropbox: 1.2 GB / 2 GB  ?
? ?????????? GDrive: 3.5 GB / 15 GB  ?
?                                     ?
? Items by Category:                  ?
?   Music: 12 (50%)                   ?
?   Story: 8 (33%)                    ?
?   Educational: 4 (17%)              ?
?                                     ?
? Most Used:                          ?
?   1. Lullaby - Used 8×              ?
?   2. Chapter 1 - Used 5×            ?
?   3. Story Time - Used 3×           ?
???????????????????????????????????????
```

**Deliverables:**
- ? Visual statistics dashboard
- ? Storage quota tracking
- ? Usage insights

---

### **Phase 9: Advanced Features** ??
**Goal:** Polish and enhancements

**Estimated Time:** 8-12 hours

#### **9.1 Audio Preview Player**
**User Story 8.9**

- [ ] In-browser audio player on item details
- [ ] Play/pause controls
- [ ] Seek bar
- [ ] Waveform visualization (optional)
- [ ] Volume control

#### **9.2 Batch Operations**
**User Story 8.8**

- [ ] Select multiple items (checkbox)
- [ ] Bulk actions:
  - Delete selected
  - Add to Tonie (select Tonie)
  - Change category
  - Add/remove tags
  - Share with household

#### **9.3 Smart Recommendations**
**User Story 8.10**

- [ ] "Items you haven't used yet" widget
- [ ] "Popular in your library" (most used)
- [ ] "Similar items" (based on tags/category)
- [ ] "Frequently used together" (co-occurrence analysis)

#### **9.4 Backup & Export**
**User Story 8.12**

- [ ] "Export Library" button
- [ ] Generate ZIP with:
  - All audio files
  - metadata.json
  - README
- [ ] "Import Library" feature:
  - Upload ZIP
  - Parse metadata
  - Upload files to storage
  - Recreate library

**Deliverables:**
- ? Audio preview in browser
- ? Batch operations
- ? Smart recommendations
- ? Backup/restore capability

---

## ??? **Sprint Schedule**

### **Sprint 4: Storage Foundation** (Week 1)
- ? Phase 1: Storage Infrastructure (8-10 hrs)
- ? Phase 2.1: Upload to Library (4-5 hrs)
- ? Phase 2.2: Library Index (3-4 hrs)

**Total:** ~15-19 hours  
**Deliverable:** Users can upload to library and browse, files in S3

---

### **Sprint 5: Core Reusability** (Week 2)
- ? Phase 2.3: Library Item Details (2-3 hrs)
- ? Phase 3: Add Library Item to Tonie (6-8 hrs)
- ? Phase 5: Media Download API (3-4 hrs)

**Total:** ~11-15 hours  
**Deliverable:** Users can reuse library items on Tonies

---

### **Sprint 6: External Storage** (Week 3)
- ? Phase 4.1: Storage Accounts UI (3-4 hrs)
- ? Phase 4.2: Dropbox OAuth (4-6 hrs)
- ? Phase 4.3: Google Drive OAuth (4-6 hrs)

**Total:** ~11-16 hours  
**Deliverable:** Users can connect Dropbox and Google Drive

---

### **Sprint 7: External Media** (Week 4)
- ? Phase 6.1: Import UI (3-4 hrs)
- ? Phase 6.2: YouTube Downloader (3-5 hrs)
- ? Phase 6.3: Podcast Downloader (2-3 hrs)

**Total:** ~8-12 hours  
**Deliverable:** Users can import from YouTube and Podcasts

---

### **Sprint 8: Sharing & Analytics** (Week 5)
- ? Phase 7: Household Sharing (4-6 hrs)
- ? Phase 8: Statistics Dashboard (4-6 hrs)

**Total:** ~8-12 hours  
**Deliverable:** Household sharing and analytics

---

### **Sprint 9: Polish** (Week 6)
- ? Phase 9.1: Audio Preview (3-4 hrs)
- ? Phase 9.2: Batch Operations (3-4 hrs)
- ? Phase 9.3: Recommendations (2-3 hrs)
- ? Phase 9.4: Backup/Export (3-4 hrs)

**Total:** ~11-15 hours  
**Deliverable:** Feature-complete media library

---

## ?? **Tech Stack**

### **Backend**
- ASP.NET Core 8.0
- Entity Framework Core 8.0
- PostgreSQL
- MinIO (dev) / Railway S3 (prod)

### **Storage SDKs**
- AWSSDK.S3 (for S3-compatible storage)
- Minio (optional, alternative to AWS SDK)
- Dropbox.Api
- Google.Apis.Drive.v3

### **External Media**
- YoutubeExplode (YouTube downloader)
- NAudio or FFmpeg (audio conversion)
- System.ServiceModel.Syndication (RSS parsing)

### **Frontend**
- Blazor Server (.NET 8)
- Bootstrap 5
- Bootstrap Icons
- JavaScript (audio player, drag-drop)

---

## ?? **Testing Strategy**

### **Unit Tests**
- [ ] `S3FileStorageService` tests
- [ ] `DropboxStorageService` tests
- [ ] `GoogleDriveStorageService` tests
- [ ] `MediaLibraryService` tests
- [ ] `ExternalMediaService` tests

### **Integration Tests**
- [ ] Upload file to MinIO
- [ ] Download file from MinIO
- [ ] Add library item to Tonie (full workflow)
- [ ] OAuth flows (mocked)

### **E2E Tests**
- [ ] Upload audio to library
- [ ] Browse library
- [ ] Add library item to Tonie
- [ ] Import from YouTube
- [ ] Share with household

**Target:** 80%+ test coverage

---

## ?? **Success Metrics**

### **MVP (Phase 1-3) Success Criteria:**
- ? Users can upload audio to library (S3)
- ? Users can browse and search library
- ? Users can add library items to Tonies
- ? No audio files in PostgreSQL (only metadata)
- ? 50+ unit tests passing

### **Full Feature (Phase 1-9) Success Criteria:**
- ? Users can connect Dropbox and Google Drive
- ? Users can import from YouTube and Podcasts
- ? Users can share library items with household
- ? Library statistics dashboard functional
- ? Audio preview player works
- ? Backup/export feature complete
- ? 100+ unit/integration tests passing
- ? E2E tests covering major workflows

---

## ?? **Deployment Checklist**

### **Dev Environment**
- [ ] MinIO running via Docker Compose
- [ ] appsettings.Development.json configured
- [ ] Test data seeded

### **Production (Railway)**
- [ ] Railway S3 bucket created
- [ ] S3 credentials in environment variables
- [ ] Dropbox OAuth app registered
- [ ] Google Drive API enabled
- [ ] Database migration applied
- [ ] Health checks passing

---

## ?? **Documentation To-Do**

- [ ] API documentation (Swagger)
- [ ] User guide: "How to upload to library"
- [ ] User guide: "How to connect Dropbox/Google Drive"
- [ ] User guide: "How to import from YouTube"
- [ ] Developer guide: "Adding new storage providers"
- [ ] Architecture diagram (storage flow)

---

## ??? **Security Considerations**

### **OAuth Tokens**
- ? Encrypt tokens in database
- ? Use ICredentialEncryptionService
- ? Token refresh logic
- ? Revoke tokens on disconnect

### **File Access**
- ? Permission checking (owner or household member)
- ? Secure download proxy
- ? No direct S3 URLs exposed

### **External Media**
- ? Validate YouTube/Podcast URLs
- ? Sanitize filenames
- ? Virus scanning (optional, future)

---

## ?? **Future Enhancements (Post-MVP)**

### **Phase 10: Advanced Audio Tools**
- Audio trimming/editing
- Fade in/out
- Volume normalization
- Concatenate multiple files
- Generate silence

### **Phase 11: AI Features**
- Auto-tagging (speech-to-text)
- Content categorization
- Duplicate detection
- Audio transcription

### **Phase 12: Monetization**
- Subscription plans (storage tiers)
- Pay-per-upload credits
- Family plan (shared households)

---

## ? **Let's Get Started!**

**Next Step:** Begin Sprint 4, Phase 1 - Storage Infrastructure

**First Tasks:**
1. Create `docker-compose.yml` with MinIO
2. Add AWSSDK.S3 NuGet package
3. Create `IFileStorageService` interface
4. Implement `S3FileStorageService`
5. Update `FileUpload` model
6. Database migration

**Ready to start coding?** ??
