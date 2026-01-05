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

### ?? User Story 2: List Creative Tonies
**As a** BoxieHub user  
**I want to** see all my Creative Tonies in a list  
**So that** I can quickly find and access them

**Acceptance Criteria:**
- [ ] User sees a dashboard with all Creative Tonies
- [ ] Each Tonie displays:
  - Name
  - Image/icon
  - Number of chapters (current/max)
  - Storage used/available (in minutes)
  - Last synced date
- [ ] List shows Tonies from the default Tonie Cloud account
- [ ] User can switch between multiple linked accounts
- [ ] Empty state shown if no Tonies exist
- [ ] Loading state while fetching data

**Technical Tasks:**
- [ ] Create `/tonies` index page
- [ ] Create TonieService for business logic
- [ ] Implement household/tonie caching
- [ ] Add Tonie card component
- [ ] Style with CSS/Bootstrap
- [ ] Add error handling for API failures

**API Dependencies:**
- `IBoxieCloudClient.GetHouseholdsAsync()`
- `IBoxieCloudClient.GetCreativeToniesByHouseholdAsync()`

**Branch:** `feature/user-story-2-list-tonies`

---

### ?? User Story 3: View Tonie Details & Tracks
**As a** BoxieHub user  
**I want to** view details of a specific Creative Tonie  
**So that** I can see all tracks and manage them

**Acceptance Criteria:**
- [ ] User can click on a Tonie to see details
- [ ] Details page shows:
  - Tonie name (editable)
  - Storage bar (visual indicator of used/free space)
  - List of all chapters/tracks
  - Each track shows: name, duration, file size
  - Track order (numbered)
- [ ] Page loads efficiently with progress indicator
- [ ] User can navigate back to Tonie list

**Technical Tasks:**
- [ ] Create `/tonies/{tonieId}` details page
- [ ] Create TrackListComponent
- [ ] Implement storage visualization component
- [ ] Add loading states and error handling
- [ ] Cache tonie details for performance

**API Dependencies:**
- `IBoxieCloudClient.GetCreativeTonieDetailsAsync()`

**Branch:** `feature/user-story-3-tonie-details`

---

## Epic 3: Track Management

### ?? User Story 4: Rearrange Tracks
**As a** BoxieHub user  
**I want to** reorder tracks on my Creative Tonie  
**So that** I can customize the playback order

**Acceptance Criteria:**
- [ ] User can drag and drop tracks to reorder
- [ ] Visual feedback during drag operation
- [ ] "Save" button appears when order changes
- [ ] User can cancel changes before saving
- [ ] System validates new order before saving
- [ ] Changes sync to Tonie Cloud API
- [ ] Success/error feedback after save
- [ ] Track numbers update after reorder

**Technical Tasks:**
- [ ] Implement drag-and-drop with JS Interop
- [ ] Track unsaved changes state
- [ ] Implement chapter reordering API call
- [ ] Add optimistic UI updates
- [ ] Handle concurrent edit conflicts
- [ ] Add confirmation before discarding changes

**API Dependencies:**
- [ ] New: `IBoxieCloudClient.UpdateChapterOrderAsync()` (to be implemented)
- Tonie Cloud API: `PATCH /v2/households/{householdId}/creativeTonies/{tonieId}`

**Branch:** `feature/user-story-4-reorder-tracks`

---

### ?? User Story 5: Upload Tracks (Stub Implementation)
**As a** BoxieHub user  
**I want to** upload audio files to my Creative Tonie  
**So that** I can add new content

**Acceptance Criteria:**
- [ ] User sees "Upload Track" button on Tonie details page
- [ ] Clicking button opens file picker dialog
- [ ] User can select MP3, M4A, or WAV files
- [ ] System validates:
  - File format (audio only)
  - File size (max per track)
  - Total storage available on Tonie
- [ ] Upload progress shown (percentage)
- [ ] **Stub:** File upload simulated (not actually sent to Tonie Cloud)
- [ ] Success message after "upload" completes
- [ ] Track appears in track list after upload
- [ ] Error message if validation fails

**Technical Tasks:**
- [ ] Create upload dialog component
- [ ] Implement file picker with InputFile
- [ ] Add file validation logic
- [ ] Create progress bar component
- [ ] **Stub:** Simulate upload with delay
- [ ] Store uploaded file metadata (not actual file)
- [ ] Update UI after successful "upload"
- [ ] Add comprehensive error messages

**API Dependencies:**
- `IBoxieCloudClient.GetUploadTokenAsync()` - for S3 upload token (stubbed)
- [ ] Future: S3 upload implementation (not in this story)

**Branch:** `feature/user-story-5-upload-tracks-stub`

---

## Epic 4: Track Operations (Future)

### ?? User Story 6: Delete Tracks (Future)
**As a** BoxieHub user  
**I want to** delete tracks from my Creative Tonie  
**So that** I can free up storage space

**Status:** ?? Planned

---

### ?? User Story 7: Edit Track Metadata (Future)
**As a** BoxieHub user  
**I want to** edit track names and details  
**So that** I can organize my content better

**Status:** ?? Planned

---

## Epic 5: Content Library (Future)

### ?? User Story 8: Media Library Management (Future)
**As a** BoxieHub user  
**I want to** maintain a library of audio files  
**So that** I can reuse content across multiple Tonies

**Status:** ?? Planned

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

### Sprint 1 (Current)
- ? User Story 0: User Registration
- ?? User Story 1: Add Tonie Cloud Account

### Sprint 2 (Next)
- User Story 2: List Creative Tonies
- User Story 3: View Tonie Details & Tracks

### Sprint 3 (Planned)
- User Story 4: Rearrange Tracks
- User Story 5: Upload Tracks (Stub)

### Future Sprints
- User Stories 6-9
- Infrastructure & Security Tasks

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
