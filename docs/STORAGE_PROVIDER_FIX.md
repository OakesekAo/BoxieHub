# Storage Provider Selection & Multi-Source Loading Fix

## Problem Summary

1. **MinIO appears empty** - Files ARE being uploaded to S3/MinIO, but `FileUpload.Data` is NULL (by design). Library loads metadata but can't access actual file data when reusing from library.

2. **No storage provider selection** - All uploads hardcoded to S3Railway with no user choice

3. **No default storage setting** - Users can't set preferred storage provider

4. **Library doesn't fetch from multiple sources** - Only loads metadata from database, doesn't retrieve actual files

## Solution Architecture

### 1. Storage Provider Selection UI
- Add storage provider dropdown to upload forms
- Show available providers (Database, S3Railway, Dropbox, GoogleDrive)
- Remember user's last selection as preference
- Add storage settings page to manage default provider

### 2. User Storage Preferences
Add new model:
```csharp
public class UserStoragePreference
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public StorageProvider DefaultProvider { get; set; }
    public int? DefaultStorageAccountId { get; set; }
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
}
```

### 3. Fix Library File Loading
When loading from library:
- MediaLibraryItem has `FileUpload.StoragePath` not `FileUpload.Data`
- Need to fetch actual file from storage before uploading to Tonie
- Use `IFileStorageService.DownloadFileAsync(storagePath, storageAccountId)`

### 4. Multi-Source Library Loading
- Library Index shows all items regardless of storage provider
- Each item displays its storage provider (badge/icon)
- Details page shows where file is stored
- Files can be moved between storage providers

## Implementation Plan

### Phase 1: Fix Current Broken Flow
1. Fix `Upload.razor` to download file from storage when using library item
2. Add proper stream handling for S3-stored files

### Phase 2: Add Storage Provider Selection
1. Add `UserStoragePreference` model and migration
2. Create storage preference service
3. Add provider dropdown to Library Upload page
4. Add provider dropdown to Tonie Upload page (new files only)
5. Remember last used provider as preference

### Phase 3: Storage Settings Page
1. Create `/storage/settings` page
2. Allow users to:
   - Set default storage provider
   - View storage accounts (Dropbox, GDrive connections)
   - See storage usage per provider
   - Manage connected accounts

### Phase 4: Multi-Provider Library Display
1. Add storage provider badge to library items
2. Show which provider each file uses
3. Add filter by storage provider
4. Add bulk move between providers (future)

## Database Changes

```sql
CREATE TABLE UserStoragePreferences (
    Id SERIAL PRIMARY KEY,
    UserId VARCHAR(450) NOT NULL,
    DefaultProvider INT NOT NULL,
    DefaultStorageAccountId INT NULL,
    Created TIMESTAMPTZ NOT NULL,
    Modified TIMESTAMPTZ NULL,
    CONSTRAINT FK_UserStoragePreferences_Users FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id),
    CONSTRAINT FK_UserStoragePreferences_UserStorageAccounts FOREIGN KEY (DefaultStorageAccountId) 
        REFERENCES UserStorageAccounts(Id)
);

CREATE UNIQUE INDEX IX_UserStoragePreferences_UserId ON UserStoragePreferences(UserId);
```

## Industry Standards Reference

### S3 Object Organization
Standard patterns:
- **User-scoped**: `users/{userId}/{category}/{guid}/{filename}`
- **Date-based**: `uploads/{year}/{month}/{day}/{guid}/{filename}`
- **Type-based**: `media/{type}/{userId}/{guid}/{filename}`

BoxieHub uses: `users/{userId}/{guid}/{filename}` ? GOOD

### Storage Path Reference
- **FileUpload.StoragePath** stores the full S3 key
- **FileUpload.Data** is NULL for external storage (saves database space)
- **FileUpload.Provider** indicates which service hosts the file
- **FileUpload.UserStorageAccountId** links to user's connected account (for Dropbox/GDrive)

### Multi-Provider Support
Industry standard approach:
1. Store provider type enum
2. Store provider-specific path/key/id
3. Route download requests based on provider
4. Implement provider-specific clients (S3, Dropbox SDK, GDrive SDK)

## File Retrieval Flow

```
User selects library item for Tonie upload
  ?
Check FileUpload.Provider
  ?
Switch on Provider:
  - Database: Use FileUpload.Data directly
  - S3Railway: IFileStorageService.DownloadFileAsync(storagePath, null)
  - Dropbox: IFileStorageService.DownloadFileAsync(storagePath, storageAccountId)
  - GoogleDrive: IFileStorageService.DownloadFileAsync(storagePath, storageAccountId)
  ?
Return Stream for Tonie Cloud upload
```

## Configuration

### appsettings.json
```json
{
  "S3Storage": {
    "ServiceUrl": "http://localhost:9000",
    "BucketName": "boxiehub-media",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "Region": "us-east-1",
    "ForcePathStyle": true
  },
  "StorageSettings": {
    "DefaultProvider": "S3Railway",
    "MaxFileSizeBytes": 209715200,
    "AllowedProviders": ["Database", "S3Railway", "Dropbox", "GoogleDrive"]
  }
}
```

## Testing Checklist

- [ ] Upload file to library ? verify appears in MinIO
- [ ] Upload file to library ? verify FileUpload.Data is NULL
- [ ] Upload file to library ? verify FileUpload.StoragePath is set
- [ ] Use library item on Tonie ? verify file downloads from S3
- [ ] Use library item on Tonie ? verify upload to Tonie Cloud succeeds
- [ ] Select different storage provider ? verify saved to correct location
- [ ] Set default provider ? verify remembered on next upload
- [ ] View library items ? verify shows storage provider badge
- [ ] Delete library item ? verify deletes from S3

## Next Steps

1. **IMMEDIATE**: Fix broken library reuse (can't upload library items to Tonies)
2. Add storage provider selection to upload forms
3. Create storage preferences model and service
4. Add storage settings page
5. Enhance library UI with provider badges
