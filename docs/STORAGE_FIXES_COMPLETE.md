# Storage System Fixes - Complete

## Problem Summary

The S3 storage bucket was empty despite user uploads, indicating files were either:
1. Not being uploaded to S3 at all
2. Failing silently and falling back to database storage
3. Tonie custom images were hardcoded to database storage

## Root Causes Identified

### 1. Tonie Image Uploads (FIXED ?)
**Issue:** Custom Tonie images were hardcoded to use Database storage only
- `TonieService.UploadCustomTonieImageAsync()` always read the entire image into a byte array
- Stored directly in `FileUpload.Data` column
- Did NOT use `IFileStorageService`
- Did NOT respect user storage preferences

**Location:** `BoxieHub/Services/TonieService.cs` lines 433-493 (old code)

### 2. Media Library Storage (NEEDS VERIFICATION ??)
**Issue:** Media Library should be using S3Railway by default
- Default storage provider is set to `StorageProvider.S3Railway` (line 48 in `StoragePreferenceService.cs`)
- BUT S3 bucket is empty, suggesting:
  - S3 uploads are failing silently
  - Falling back to Database storage
  - OR no media has been uploaded yet

**Location:** `BoxieHub/Services/MediaLibraryService.cs` lines 100-126

---

## Fixes Implemented

### Fix 1: Refactor Tonie Image Uploads ? COMPLETE

**Changes Made:**
1. **Injected `IStoragePreferenceService`** into `TonieService` constructor
2. **Refactored `UploadCustomTonieImageAsync()`** to match Media Library pattern:
   - Get user's default storage provider
   - If `Database`: Store in `FileUpload.Data` (legacy)
   - If `S3Railway/Dropbox/GDrive`: Use `IFileStorageService.UploadFileAsync()`
   - Store `StoragePath` and `Provider` in `FileUpload` table
   - Update user's last used provider

3. **Updated `DeleteCustomTonieImageAsync()`**:
   - Delete from external storage if `StoragePath` exists
   - Clean up database records
   - Handle storage deletion failures gracefully

**Code Changes:**
```csharp
// OLD CODE (Database only)
using var ms = new MemoryStream();
await imageStream.CopyToAsync(ms, ct);
var imageData = ms.ToArray();

var fileUpload = new FileUpload
{
    Id = Guid.NewGuid(),
    Data = imageData, // Always in database
    ContentType = contentType,
    ...
};

// NEW CODE (Respects storage preferences)
var provider = await _storagePreferenceService.GetDefaultProviderAsync(userId, ct);

string? storagePath = null;
byte[]? imageData = null;

if (provider == StorageProvider.Database)
{
    // Store in DB (legacy)
    using var ms = new MemoryStream();
    await imageStream.CopyToAsync(ms, ct);
    imageData = ms.ToArray();
}
else
{
    // Upload to external storage
    storagePath = await _fileStorageService.UploadFileAsync(
        imageStream, fileName, contentType, userId, null, ct);
}

var fileUpload = new FileUpload
{
    Id = Guid.NewGuid(),
    Data = imageData, // NULL for external storage
    Provider = provider,
    StoragePath = storagePath,
    ...
};
```

**Files Modified:**
- `BoxieHub/Services/TonieService.cs`
- `BoxieHub.Tests/Unit/Services/TonieServiceTests.cs`

---

## System Architecture (Current State)

### Storage Provider Hierarchy

```
User Upload Request
    ?
    ?
IStoragePreferenceService.GetDefaultProviderAsync(userId)
    ?
    ?
?????????????????????????????????????????????????????????????????
?                                  ?
?   StorageProvider.Database       ?   StorageProvider.S3Railway
?   (Legacy, small files)          ?   (Default, recommended)
?                                  ?
?   ? Store in FileUpload.Data     ?   ? IFileStorageService
?   ? Data != null                 ?   ?   ? S3FileStorageService
?   ? StoragePath = null            ?   ?      ? MinIO (dev)
?                                  ?   ?      ? Railway S3 (prod)
?                                  ?   ? Store in S3 bucket
?                                  ?   ? Data = null
?                                  ?   ? StoragePath = "users/..."
?????????????????????????????????????????????????????????????????
```

### Storage Provider Support Matrix

| Provider | Status | Used For | Storage Account Required |
|----------|--------|----------|--------------------------|
| `Database` | ? Legacy | Small images (<1MB) | No |
| `S3Railway` | ? **DEFAULT** | All media, Tonie images | No (configured in appsettings) |
| `Dropbox` | ? Planned | User-connected storage | Yes (OAuth) |
| `GoogleDrive` | ? Planned | User-connected storage | Yes (OAuth) |

---

## Configuration

### Development (MinIO)
```json
// appsettings.Development.json
{
  "S3Storage": {
    "BucketName": "boxiehub-media",
    "Region": "us-east-1",
    "ServiceUrl": "http://localhost:9000",
    "AccessKey": "boxiehub",
    "SecretKey": "boxiehub-dev-password-123",
    "ForcePathStyle": true
  }
}
```

### Production (Railway S3)
```json
// appsettings.Production.json
{
  "S3Storage": {
    "BucketName": "${RAILWAY_S3_BUCKET}",
    "Region": "us-east-1",
    "ServiceUrl": "${RAILWAY_S3_ENDPOINT}",
    "AccessKey": "${RAILWAY_S3_ACCESS_KEY}",
    "SecretKey": "${RAILWAY_S3_SECRET_KEY}",
    "ForcePathStyle": true
  }
}
```

---

## File Path Structure

### S3 Storage Paths
```
boxiehub-media/
  users/
    {userId}/
      {guid}/
        audio.mp3          (Media Library audio)
        custom-image.jpg   (Tonie custom image)
        
Example:
users/abc123/550e8400-e29b-41d4-a716-446655440000/pirate-story.mp3
users/abc123/7a3b9c4d-f2e1-4b8a-9d6f-123456789abc/tonie-custom.png
```

### Database Serving (UploadsController)
```
GET /uploads/{fileId}
  ? Check FileUpload.Provider
  ? If Database: Return FileUpload.Data
  ? If S3Railway: Download from IFileStorageService
  ? If Dropbox/GDrive: Download via OAuth tokens
```

---

## Testing Checklist

### Test 1: Verify S3 Upload (Media Library)
- [ ] 1. Upload a new audio file to Media Library
- [ ] 2. Check PostgreSQL `FileUploads` table:
      ```sql
      SELECT Id, Provider, StoragePath, FileSizeBytes 
      FROM "FileUploads" 
      ORDER BY Created DESC LIMIT 1;
      ```
      - `Provider` should be `1` (S3Railway)
      - `StoragePath` should start with `users/`
      - `Data` should be `NULL`
- [ ] 3. Check MinIO bucket at http://localhost:9000
      - Should see file in `boxiehub-media` bucket
      - Path: `users/{userId}/{guid}/{filename}`
- [ ] 4. Access file via `/uploads/{fileId}` endpoint
      - Should download successfully
      - Should not error

### Test 2: Verify S3 Upload (Tonie Images)
- [ ] 1. Upload a custom Tonie image
- [ ] 2. Check PostgreSQL `FileUploads` table (same query as above)
      - `Provider` should be `1` (S3Railway)
      - `FileCategory` should be `"Image"`
      - `Data` should be `NULL`
- [ ] 3. Check MinIO bucket
      - Image file should exist in `boxiehub-media` bucket
- [ ] 4. Verify image displays on Tonie Details page
      - Should show custom image immediately
      - URL should be `/uploads/{fileId}`

### Test 3: Verify Database Fallback
- [ ] 1. Stop MinIO container: `docker-compose down`
- [ ] 2. Upload should fail with error message
      - OR fall back to Database storage (depending on implementation)
- [ ] 3. Check logs for error details

### Test 4: Verify Delete Operations
- [ ] 1. Delete a Media Library item with S3 file
- [ ] 2. Check MinIO bucket - file should be deleted
- [ ] 3. Delete a custom Tonie image
- [ ] 4. Check MinIO bucket - image should be deleted
- [ ] 5. Check PostgreSQL - `FileUpload` record should be gone

---

## Common Issues & Troubleshooting

### Issue 1: "S3 bucket is empty"
**Possible Causes:**
1. MinIO not running: `docker ps | grep minio`
2. S3 configuration incorrect in `appsettings.Development.json`
3. Uploads failing silently, falling back to Database
4. No media has been uploaded yet (test with a new upload)

**Solution:**
```bash
# Check MinIO is running
docker ps

# Restart MinIO
docker-compose up -d minio

# Check MinIO logs
docker logs boxiehub-minio-1

# Access MinIO UI
open http://localhost:9000
# Login: boxiehub / boxiehub-dev-password-123
```

### Issue 2: "Image shows immediately but reverts after refresh"
**Cause:** Cache busting is working, but new image not uploaded to S3

**Solution:** Check logs for upload errors, verify S3 connection

### Issue 3: "404 when accessing /uploads/{fileId}"
**Cause:** `UploadsController` can't download from S3

**Solution:** 
- Check `IFileStorageService` implementation
- Verify S3 credentials
- Check `FileUpload.StoragePath` is correct

---

## Next Steps ??

### Immediate (This Session)
1. ?? **Test Media Library upload** - Verify files go to S3
2. ?? **Test Tonie image upload** - Verify images go to S3
3. ?? **Check MinIO bucket** - Confirm files exist
4. ?? **Verify UploadsController** - Confirm files can be downloaded

### Future Enhancements
1. **Add storage provider selection UI** on upload pages
2. **Add Dropbox OAuth integration** (User Story 8 Phase 4)
3. **Add Google Drive OAuth integration** (User Story 8 Phase 4)
4. **Add storage usage dashboard** - Show how much space used per provider
5. **Add file migration tool** - Move files from Database to S3
6. **Add storage health checks** - Alert if S3 is down

---

## Success Criteria ?

This fix is complete when:
- [x] Tonie images respect user storage preferences
- [x] Tonie images can use S3Railway storage
- [x] Delete operations clean up external storage
- [ ] All unit tests pass (113 tests)
- [ ] S3 bucket contains uploaded files
- [ ] Files can be downloaded via `/uploads/{fileId}`
- [ ] No regression in existing functionality

---

## Related Documentation

- [User Story 8: Media Library](USER_STORY_8_COMPLETE.md)
- [Storage Implementation Guide](STORAGE_IMPLEMENTATION_COMPLETE.md)
- [Storage Setup Guide](STORAGE_SETUP.md)
- [Storage Quick Reference](STORAGE_QUICK_REFERENCE.md)

---

## Commit Message

```
fix: Refactor Tonie image uploads to use IFileStorageService

- Inject IStoragePreferenceService into TonieService
- Respect user's default storage provider (Database/S3/Dropbox/GDrive)
- Match Media Library storage pattern for consistency
- Update delete operations to clean up external storage
- Add comprehensive logging for troubleshooting
- Update unit tests with IStoragePreferenceService mock

Fixes #XX - Tonie images not respecting storage preferences
Related to User Story 8 - Media Library Storage
```

---

**Status:** ? **FIX IMPLEMENTED - READY FOR TESTING**

**Date:** January 9, 2026  
**Developer:** GitHub Copilot  
**Branch:** `feature/user-story-7-edit-metadata`
