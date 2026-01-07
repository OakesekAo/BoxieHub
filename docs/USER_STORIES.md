# BoxieHub User Stories

Based on [TonieBox.CreativeManager](https://github.com/gemse007/TonieBox.CreativeManager) reference implementation.

## Overview

BoxieHub is a web application for managing Creative Tonies, allowing users to:
- Link their Tonie Cloud accounts
- View and manage their Creative Tonies
- Upload, organize, and manage audio content
- Sync content between BoxieHub and Tonie Cloud

---

## Epic 1: Account & Authentication

### ? User Story 0: User Registration (COMPLETE)
**As a** new user  
**I want to** create a BoxieHub account  
**So that** I can manage my Creative Tonies through the web interface

**Acceptance Criteria:**
- ? User can register with email and password
- ? User receives confirmation email
- ? User can log in with credentials
- ? User session is maintained securely

**Status:** ? Complete - ASP.NET Core Identity implemented

---

### ?? User Story 1: Add Tonie Cloud Account (IN PROGRESS)
**As a** BoxieHub user  
**I want to** link my Tonie Cloud account credentials  
**So that** I can access and manage my Creative Tonies

**Acceptance Criteria:**
- [ ] User can add Tonie Cloud credentials (email/password)
- [ ] Credentials are encrypted before storage
- [ ] System validates credentials by testing authentication
- [ ] User can set a display name for the account
- [ ] User can mark one account as default
- [ ] User receives feedback if credentials are invalid
- [ ] Multiple Tonie accounts can be linked to one BoxieHub user

**Technical Tasks:**
- [ ] Create `TonieCredential` model
- [ ] Add `TonieCredentials` DbSet to ApplicationDbContext
- [ ] Create database migration
- [ ] Implement `ICredentialEncryptionService`
- [ ] Create `/tonies/add-account` Blazor page
- [ ] Add "Add Tonie Account" navigation link
- [ ] Test credential validation with BoxieAuthService
- [ ] Add user feedback for success/failure

**Database Schema:**
```sql
CREATE TABLE TonieCredentials (
    Id INT PRIMARY KEY,
    UserId NVARCHAR(450) NOT NULL,
    TonieUsername NVARCHAR(256) NOT NULL,
    EncryptedPassword NVARCHAR(MAX) NOT NULL,
    DisplayName NVARCHAR(256),
    IsDefault BIT NOT NULL DEFAULT 0,
    LastAuthenticated DATETIMEOFFSET,
    Created DATETIMEOFFSET NOT NULL,
    Modified DATETIMEOFFSET NOT NULL,
    FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id)
);
```

**API Dependencies:**
- `IBoxieAuthService.GetAccessTokenAsync()` - for credential validation

**Branch:** `feature/user-story-1-add-tonie-account`

---

## Epic 2: Creative Tonie Management

### ? User Story 2: List Creative Tonies (COMPLETE)
**As a** BoxieHub user  
**I want to** see all my Creative Tonies in a list  
**So that** I can quickly find and access them

**Acceptance Criteria:**
- ? User sees a dashboard with all Creative Tonies
- ? Each Tonie displays:
  - Name
  - Image/icon
  - Number of chapters (current/max)
  - Storage used/available (in minutes)
  - Storage percentage bar
- ? List shows Tonies from the default Tonie Cloud account
- ? User can switch between multiple linked accounts (via Manage Accounts)
- ? Empty state shown if no Tonies exist
- ? Skeleton loading state while fetching data
- ? Smart caching with 1-day TTL
- ? Force refresh capability
- ? Quick stats dashboard (Total Tonies, Chapters, Duration)

**Completed Features:**
- Dashboard at `/tonies` with responsive card grid
- Database-first caching architecture
- Stale data warnings
- Comprehensive error handling
- Toast notifications

**Branch:** `feature/user-story-2-manage-tonies` ? **MERGED**

---

### ? User Story 3: View Tonie Details & Tracks (COMPLETE)
**As a** BoxieHub user  
**I want to** view details of a specific Creative Tonie  
**So that** I can see all tracks and manage them

**Acceptance Criteria:**
- ? User can click on a Tonie to see details
- ? Details page shows:
  - Tonie name with image
  - Color-coded storage bar (visual indicator)
  - List of all chapters/tracks
  - Each track shows: name, duration, transcoding status
  - Track order (numbered)
- ? Page loads efficiently with progress indicator
- ? User can navigate back to Tonie list
- ? Force refresh button
- ? Delete chapter with confirmation modal
- ? Upload audio link

**Completed Features:**
- Details page at `/tonies/{householdId}/{tonieId}`
- Chapter list with status badges (Processing/Ready)
- Storage visualization with percentage
- Delete chapter functionality
- Breadcrumb navigation
- Auto-refresh after operations

**Branch:** `feature/user-story-2-manage-tonies` ? **MERGED**

---

## Epic 3: Track Management

### ? User Story 4: Rearrange Tracks (COMPLETE)
**As a** BoxieHub user  
**I want to** reorder tracks on my Creative Tonie  
**So that** I can customize the playback order

**Acceptance Criteria:**
- ? User can drag and drop tracks to reorder
- ? Visual feedback during drag operation (CSS classes)
- ? "Save Order" button appears when order changes
- ? User can cancel changes before saving
- ? System validates new order before saving
- ? Changes sync to Tonie Cloud API via PATCH
- ? Success/error feedback after save (toast notifications)
- ? Track numbers update after reorder
- ? Unsaved changes warning banner

**Completed Features:**
- HTML5 drag & drop with JavaScript interop
- Pure JavaScript implementation (no Blazor drag event conflicts)
- Save/Cancel controls in chapter header
- Optimistic UI updates
- Toast notifications for feedback
- Delete button disabled during reorder

**Technical Implementation:**
- JavaScript attaches event listeners directly to DOM
- JSInvokable callback receives drop result
- Calls `TonieService.ReorderChaptersAsync()`
- CSS visual feedback (.dragging, .drag-over classes)

**Branch:** `feature/user-story-2-manage-tonies` ? **MERGED**

---

### ? User Story 5: Upload Tracks (COMPLETE)
**As a** BoxieHub user  
**I want to** upload audio files to my Creative Tonie  
**So that** I can add new content

**Acceptance Criteria:**
- ? User sees "Upload Audio" button on Tonie details page
- ? Dedicated upload page at `/tonies/{householdId}/{tonieId}/upload`
- ? User can select MP3, M4A, OGG, or WAV files
- ? System validates:
  - File format (audio only)
  - File size (max 200MB)
  - Audio duration vs available storage
  - Total storage available on Tonie
- ? **Client-side audio duration detection** using HTML5 Audio API
- ? Upload progress shown with spinner
- ? **Real S3 upload** to Tonie Cloud (not stubbed!)
- ? Success message after upload completes
- ? Track appears in track list after transcoding
- ? Error message if validation fails
- ? Transcoding status tracking

**Completed Features:**
- Dedicated upload page with form
- File validation (format, size, duration)
- **Audio duration detection** - Shows duration before upload
- **Duration validation** - Compares against available storage
- S3 presigned URL upload
- Tonie Cloud API integration
- Success/error feedback with toasts
- Auto-populate chapter title from filename
- Storage warning for low space
- Transcoding info message after upload

**Technical Implementation:**
- S3StorageService with Content-Length header
- BoxieCloudClient.SyncAudioAsync workflow
- FileUpload and AudioUploadHistory models
- JavaScript getAudioDuration() function
- Comprehensive error handling
- **11 unit tests passing** (FileUploadHelperTests)

**Branch:** `feature/user-story-2-manage-tonies` ? **MERGED**

---

## Epic 4: Track Operations (Future)

### ? User Story 6: Delete Tracks (COMPLETE)
**As a** BoxieHub user  
**I want to** delete tracks from my Creative Tonie  
**So that** I can free up storage space

**Status:** ? Complete - Implemented as part of User Story 3

**Completed Features:**
- Delete button on each chapter row
- Confirmation modal before deletion
- Shows chapter details in modal (title, duration)
- Disabled during unsaved reorder operations
- Toast notification on success/error
- Auto-refresh Tonie data after deletion
- Calls `TonieService.DeleteChapterAsync()`

**Branch:** `feature/user-story-2-manage-tonies` ? **MERGED**

---

### ? User Story 7: Edit Track Metadata (COMPLETE)
**As a** BoxieHub user  
**I want to** edit track names and custom Tonie images  
**So that** I can organize my content better

**Status:** ? Complete - Sprint 3

**Completed Features:**
- Inline chapter title editing
- Custom Tonie image uploads (local storage)
- Revert to default Tonie Cloud image
- Optimistic UI updates
- Validation and error handling

**Branch:** `feature/user-story-7-edit-metadata` ? **MERGED**

---

## Epic 5: Content Library

### ?? User Story 8: Media Library Management (IN PROGRESS)
**As a** BoxieHub user  
**I want to** maintain a library of audio files with flexible storage options  
**So that** I can reuse content across multiple Tonies without re-uploading

**Status:** ?? In Progress - Sprint 4-9

**Scope:** See [Media Library Roadmap](MEDIA_LIBRARY_ROADMAP.md)

**Key Features:**
1. **Storage Infrastructure** (Phase 1)
   - S3-compatible storage (MinIO dev, Railway prod)
   - Storage abstraction layer
   - Move audio files out of PostgreSQL
   
2. **Library Core** (Phase 2-3)
   - Upload audio to library with metadata
   - Browse, search, and filter library
   - Add library items to Creative Tonies
   - Usage tracking
   
3. **External Storage** (Phase 4)
   - Connect Dropbox accounts (OAuth)
   - Connect Google Drive accounts (OAuth)
   - User chooses storage per upload
   
4. **Import from URLs** (Phase 6)
   - Import from YouTube
   - Import from Podcast RSS feeds
   - Auto-convert to MP3
   
5. **Household Sharing** (Phase 7)
   - Share library items within household
   - Permission system (owner vs member)
   - ? No public/community sharing
   
6. **Analytics & Tools** (Phase 8-9)
   - Library statistics dashboard
   - Audio preview player
   - Batch operations
   - Backup/export library

**Branch:** `feature/user-story-8-media-library`

**Estimated Timeline:** 6-9 weeks (60-80 hours)

**Technical Stack:**
- MinIO (dev) / Railway S3 (prod)
- Dropbox.Api SDK
- Google.Apis.Drive.v3 SDK
- YoutubeExplode (YouTube downloads)
- OAuth 2.0 integrations

---

### ?? User Story 9: Family Sharing (Future)
**As a** BoxieHub user  
**I want to** share my account with family members  
**So that** multiple users can manage the same Tonies

**Status:** ?? Planned

---

## Technical Debt & Infrastructure

### ?? Infrastructure Tasks
- [ ] Implement proper error handling middleware
- [ ] Add application logging (Serilog/NLog)
- [ ] Set up health checks for Tonie Cloud API
- [ ] Implement rate limiting for API calls
- [ ] Add automated integration tests for API calls
- [ ] Set up CI/CD pipeline
- [ ] Add Docker support for deployment

### ?? Security Tasks
- [ ] Implement credential encryption service
- [ ] Add HTTPS enforcement
- [ ] Set up Content Security Policy headers
- [ ] Implement CSRF protection
- [ ] Add rate limiting for login attempts
- [ ] Audit logging for sensitive operations

---

## Definition of Done

A user story is considered complete when:
1. ? All acceptance criteria met
2. ? Unit tests written and passing
3. ? Integration tests passing (where applicable)
4. ? Code reviewed and approved
5. ? Documentation updated
6. ? Merged to main branch
7. ? Deployed to staging environment
8. ? QA testing completed

---

## Sprint Planning

### Sprint 1 (COMPLETE) ?
- ? User Story 0: User Registration
- ? User Story 1: Add Tonie Cloud Account (Database + Encryption implemented)

### Sprint 2 (COMPLETE) ?
- ? **User Story 2**: List Creative Tonies
- ? **User Story 3**: View Tonie Details & Tracks
- ? **User Story 4**: Rearrange Tracks (Drag & Drop)
- ? **User Story 5**: Upload Tracks (Real S3 Upload)
- ? **User Story 6**: Delete Tracks

**Total Story Points Completed:** 21  
**Features Delivered:** 6 complete user stories  
**Test Coverage:** 11 unit tests passing

### Sprint 3 (COMPLETE) ?
- ? **User Story 7**: Edit Track Metadata (Inline chapter editing)
- ? **User Story 7.5**: Upload Custom Tonie Images
- ? **User Story 4 Fix**: Continuous drag & drop reordering

**Total Story Points Completed:** 8  
**Features Delivered:** 3 complete features  
**Test Coverage:** 113 tests passing, 0 failures

### Sprint 4: Storage Foundation (Week 1) ??
- [ ] **US 8 - Phase 1**: Storage Infrastructure (MinIO, S3 abstraction)
- [ ] **US 8 - Phase 2.1**: Upload to Library
- [ ] **US 8 - Phase 2.2**: Library Index Page

**Estimated:** 15-19 hours  
**Deliverable:** Users can upload to library, files in S3

### Sprint 5: Core Reusability (Week 2)
- [ ] **US 8 - Phase 2.3**: Library Item Details
- [ ] **US 8 - Phase 3**: Add Library Item to Tonie
- [ ] **US 8 - Phase 5**: Media Download API

**Estimated:** 11-15 hours  
**Deliverable:** Users can reuse library items on Tonies

### Sprint 6: External Storage (Week 3)
- [ ] **US 8 - Phase 4**: Dropbox & Google Drive OAuth
- [ ] **US 8 - Phase 4**: Storage Accounts UI

**Estimated:** 11-16 hours  
**Deliverable:** Users can connect personal cloud storage

### Sprint 7: External Media (Week 4)
- [ ] **US 8 - Phase 6**: Import from YouTube
- [ ] **US 8 - Phase 6**: Import from Podcasts

**Estimated:** 8-12 hours  
**Deliverable:** Users can import from external sources

### Sprint 8: Sharing & Analytics (Week 5)
- [ ] **US 8 - Phase 7**: Household Library Sharing
- [ ] **US 8 - Phase 8**: Statistics Dashboard

**Estimated:** 8-12 hours  
**Deliverable:** Household sharing and analytics

### Sprint 9: Polish (Week 6)
- [ ] **US 8 - Phase 9**: Audio Preview Player
- [ ] **US 8 - Phase 9**: Batch Operations
- [ ] **US 8 - Phase 9**: Smart Recommendations
- [ ] **US 8 - Phase 9**: Backup/Export

**Estimated:** 11-15 hours  
**Deliverable:** Feature-complete media library

### Future Sprints
- User Story 9: Family Sharing (cross-user permissions)
- Infrastructure & Security Tasks
- CI/CD Pipeline
- AI Features (auto-tagging, transcription)
- Advanced Audio Tools (editing, trimming)

---

## Notes

- All API interactions use the `BoxieCloudClient` service
- Credential storage uses encryption (implementation required)
- Follow existing Blazor patterns in the codebase
- Use Bootstrap 5 for styling consistency
- Implement responsive design for mobile support

---

## References

- [TonieBox.CreativeManager](https://github.com/gemse007/TonieBox.CreativeManager) - Reference implementation
- [Tonie Cloud API](https://meine.tonies.de) - Official Tonie Cloud
- [BoxieHub Repository](https://github.com/OakesekAo/BoxieHub)
