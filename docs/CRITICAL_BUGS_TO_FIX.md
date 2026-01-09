# Critical Bugs - Fix Required Before Next User Story

## Summary

4 critical bugs identified. **2 FIXED** ??, 1 needs testing ??, 1 needs implementation ?.

---

## ? Issue 1: UploadsController Not Serving S3 Files - **FIXED** ?

**Problem:** Images uploaded to S3 show as broken links (`/uploads/{guid}`)

**Root Cause:** `UploadsController.GetImage()` only checked for `Data` (Database storage) and returned `NotFound()` for S3-stored files

**Fix Applied:**
- Added `IFileStorageService` injection to controller
- Check if file is in Database (legacy) vs External Storage  
- Download from S3 using `_fileStorageService.DownloadFileAsync()` if `StoragePath` exists
- Return file stream with proper content type

**Files Modified:**
- `BoxieHub/Controllers/UploadsController.cs`

**Status:** ? **COMPLETE** - Build successful

---

## ? Issue 2: Library Card Delete Button Non-Functional - **FIXED** ?

**Problem:** Delete from dropdown menu (3-dot menu) doesn't work on Library Index page, but delete from Details page works fine

**Root Cause:** `LibraryItemCard` component has `OnDelete` EventCallback defined, but `Library/Index.razor` doesn't wire it up!

**Fix Applied:**
1. Added delete confirmation modal to `Index.razor`
2. Wired up `OnDelete="@ShowDeleteConfirmation"` to `LibraryItemCard`
3. Added `ShowDeleteConfirmation()`, `CancelDelete()`, and `ConfirmDelete()` handlers
4. Added toast notifications for success/error feedback
5. Includes usage count warning if file was used on Tonies

**Files Modified:**
- `BoxieHub/Components/Pages/Library/Index.razor`

**Status:** ? **COMPLETE** - Build successful

---

## ? Issue 3: Library Upload from Tonie Page - **INVESTIGATION NEEDED** ??

**Problem:** "Use from Library" flow from Tonie upload page supposedly doesn't work
**Expected Flow:** Tonie ? Upload ? Use from Library ? Select Media ? Upload
**User Report:** Getting "no file selected" error

**Investigation Results:**
Looking at `BoxieHub/Components/Pages/Tonies/Upload.razor`, the `StartUpload()` method DOES handle `selectedLibraryItem`:

```csharp
// Lines 351-382 handle library item properly
if (selectedLibraryItem != null && selectedLibraryItem.FileUpload != null)
{
    // Download from S3 or Database
    stream = await FileStorageService.DownloadFileAsync(...);
}
```

**Possible Root Causes:**
1. **User Error** - Maybe they didn't actually select a file?
2. **Navigation Issue** - `LoadLibraryItemFromQuery()` might not be triggering
3. **FileUpload Navigation Property** - `.Include(m => m.FileUpload)` might be missing
4. **Modal State** - `LibraryBrowserModal` might not be passing the item correctly

**Testing Needed:**
- Test the exact flow: Tonie ? Upload ? "Use from Library" button
- Check browser console for JavaScript errors
- Check server logs during the flow
- Verify `MediaLibraryService.GetLibraryItemAsync()` includes `FileUpload`

**Status:** ?? **NEEDS TESTING** - Code looks correct, may already work

---

## ? Issue 4: Media Duration Not Showing - **ROOT CAUSE FOUND** ??

**Problem:** Duration shows as placeholder ("0s" or nothing) everywhere

**Root Cause:** ? **CONFIRMED**
Line 288 in `BoxieHub/Components/Pages/Library/Upload.razor`:

```csharp
DurationSeconds = 0 // TODO: Extract from audio metadata if possible
```

**Duration is hardcoded to 0!** This means:
- All library items get saved with `DurationSeconds = 0`
- `FormattedDuration` shows "0.0s"  
- No actual duration extraction is happening

**What's Missing:**
1. **Client-side JavaScript** to detect audio duration (HTML5 Audio API)
2. **Pass duration** from Razor component to DTO
3. **Save actual duration** to database

**Tonie Upload DOES Have Duration Detection!**
`BoxieHub/Components/Pages/Tonies/Upload.razor` lines 331-348 shows the correct implementation:

```csharp
// Create blob and detect duration using JavaScript
audioDurationSeconds = await JS.InvokeAsync<double>("getAudioDuration", 
    fileBytes, 
    selectedFile.ContentType);
```

**Fix Strategy:**
1. Copy the JavaScript `getAudioDuration()` function (already exists)
2. Add duration detection to Library Upload page
3. Pass `audioDurationSeconds` to `MediaLibraryItemDto`
4. Update database for existing items (migration or manual fix)

**Files to Modify:**
- `BoxieHub/Components/Pages/Library/Upload.razor` - Add duration detection
- Test with new uploads to verify duration is saved

**Status:** ? **TODO** - Clear fix path identified

---

## Fix Priority Order

1. ? **Issue 1** - UploadsController S3 Support (DONE)
2. **Issue 2** - Library Index Delete Button (High Priority - UX breaking)
3. **Issue 4** - Media Duration Display (High Priority - Data quality)
4. **Issue 3** - Upload from Library Flow (Medium Priority - Alternate workflow exists)

---

## Testing Checklist

### After Fixing Issue 2:
- [ ] Go to `/library`
- [ ] Click 3-dot menu on any media card
- [ ] Click "Delete"
- [ ] Confirmation modal should appear
- [ ] Click "Delete" in modal
- [ ] Item should be deleted and list refreshed

### After Fixing Issue 3:
- [ ] Go to `/tonies/{householdId}/{tonieId}/upload`
- [ ] Click "Use from Library" button
- [ ] Select a media item from library browser
- [ ] Click "Use Selected File"
- [ ] Media should populate in form
- [ ] Fill in chapter title
- [ ] Click "Upload Audio"
- [ ] Upload should succeed (not "no file selected")

### After Fixing Issue 4:
- [ ] Upload a new audio file to library
- [ ] Check that duration is calculated and shown during upload
- [ ] Check that duration appears on library index card
- [ ] Check that duration appears on library details page
- [ ] Verify duration is saved in database

---

## Next Steps

1. **Fix Issue 2** (Library Delete) - Should take ~10 minutes
2. **Investigate Issue 4** (Duration) - Need to trace through upload flow
3. **Fix Issue 3** (Library Upload Flow) - May require refactoring upload logic

**Estimated Time:** 30-45 minutes total

**Branch:** Current branch (`feature/user-story-7-edit-metadata`)

**After Fixes:** Run full test suite, commit all fixes together with a comprehensive commit message.

---

**Date:** January 9, 2026  
**Status:** 1/4 Fixed, 3 Remaining  
**Priority:** Critical - Block next user story until resolved
