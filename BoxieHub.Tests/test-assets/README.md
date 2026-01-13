# Test Assets for Audio Verification

This directory should contain real audio files for testing audio analysis and duration verification.

## Required Test Files

### 1. **Short Audio (< 1 minute)**
- **File:** `short-30s.mp3`
- **Duration:** 30 seconds
- **Format:** MP3, 128 kbps
- **Purpose:** Test basic duration detection
- **Where to get:** Create using Audacity or download from Freesound.org

### 2. **Medium Audio (5-10 minutes)**
- **File:** `medium-5m.mp3`
- **Duration:** ~5 minutes
- **Format:** MP3, 128 kbps
- **Purpose:** Test typical podcast/music length
- **Where to get:** Creative Commons podcasts or music

### 3. **Long Audio (85-89 minutes)**
- **File:** `long-89m.mp3`
- **Duration:** 89 minutes (just under Tonie limit)
- **Format:** MP3, 128 kbps
- **Purpose:** Test boundary condition for Tonie 90-minute limit
- **Where to get:** Create by concatenating multiple shorter files

### 4. **Too Long Audio (>90 minutes)**
- **File:** `too-long-95m.mp3`
- **Duration:** 95 minutes (exceeds Tonie limit)
- **Format:** MP3, 128 kbps
- **Purpose:** Test validation rejection
- **Where to get:** Create by concatenating multiple files

### 5. **M4A File (YouTube format)**
- **File:** `test-video.m4a`
- **Duration:** ~3 minutes
- **Format:** M4A (AAC audio)
- **Purpose:** Test YouTube import format
- **Where to get:** Extract audio from Creative Commons YouTube video

### 6. **OGG File**
- **File:** `test-audio.ogg`
- **Duration:** ~2 minutes
- **Format:** OGG Vorbis
- **Purpose:** Test alternative format support
- **Where to get:** Convert MP3 using ffmpeg

### 7. **WAV File**
- **File:** `test-audio.wav`
- **Duration:** ~1 minute
- **Format:** WAV (uncompressed)
- **Purpose:** Test lossless format
- **Where to get:** Convert MP3 using Audacity

### 8. **Corrupted File**
- **File:** `corrupted.mp3`
- **Duration:** N/A
- **Format:** Invalid MP3 (truncated/corrupted header)
- **Purpose:** Test error handling
- **How to create:** Truncate a valid MP3 file or write random bytes

### 9. **Zero Duration File**
- **File:** `zero-duration.mp3`
- **Duration:** 0 seconds
- **Format:** MP3 with valid header but no audio data
- **Purpose:** Test edge case handling
- **How to create:** Create minimal MP3 header only

### 10. **Wrong Metadata**
- **File:** `wrong-metadata.mp3`
- **Duration:** 60 seconds (actual)
- **Metadata:** Says 3600 seconds (1 hour) - WRONG!
- **Purpose:** Test that actual duration is used, not metadata
- **How to create:** Edit MP3 tags with wrong duration using MP3Tag

## Creating Test Files

### Using FFmpeg

```bash
# Create 30-second silence
ffmpeg -f lavfi -i anullsrc=r=44100:cl=stereo -t 30 -q:a 2 -acodec libmp3lame short-30s.mp3

# Create 5-minute audio (concatenate short files)
ffmpeg -i short-30s.mp3 -filter_complex "[0:a]aloop=9" -t 300 medium-5m.mp3

# Convert MP3 to M4A
ffmpeg -i short-30s.mp3 -c:a aac -b:a 128k test-video.m4a

# Convert MP3 to OGG
ffmpeg -i short-30s.mp3 -c:a libvorbis -q:a 4 test-audio.ogg

# Convert MP3 to WAV
ffmpeg -i short-30s.mp3 test-audio.wav

# Create corrupted file
dd if=short-30s.mp3 of=corrupted.mp3 bs=1 count=100
```

### Using Audacity

1. Generate ? Tone ? Set duration ? Export as MP3
2. Repeat for different durations
3. Use Edit ? Metadata to set wrong duration for testing

## License Requirements

?? **Important:** Only use audio files that are:
- Public domain
- Creative Commons (CC0, CC-BY)
- Created by you
- Licensed for testing purposes

**Do NOT commit copyrighted audio files to the repository!**

## Recommended Sources

- **Freesound.org** - CC-licensed sound effects
- **Free Music Archive** - CC-licensed music
- **YouTube Audio Library** - Royalty-free music
- **Archive.org** - Public domain recordings

## .gitignore

Make sure `test-assets/` is in `.gitignore` to avoid committing large audio files:

```gitignore
# Test audio files (not committed due to size/licensing)
BoxieHub.Tests/test-assets/*.mp3
BoxieHub.Tests/test-assets/*.m4a
BoxieHub.Tests/test-assets/*.ogg
BoxieHub.Tests/test-assets/*.wav
BoxieHub.Tests/test-assets/*.flac

# Keep README
!BoxieHub.Tests/test-assets/README.md
```

## Using in Tests

```csharp
[Test]
public async Task GetActualDurationAsync_With30SecondMP3_ReturnsCorrectDuration()
{
    // Arrange
    var testFile = Path.Combine(TestContext.CurrentContext.TestDirectory, 
        "test-assets", "short-30s.mp3");
    
    if (!File.Exists(testFile))
    {
        Assert.Ignore("Test file not found. See test-assets/README.md");
    }
    
    using var stream = File.OpenRead(testFile);
    
    // Act
    var duration = await _audioAnalysisService.GetActualDurationAsync(stream, "audio/mpeg");
    
    // Assert
    Assert.That(duration, Is.Not.Null);
    Assert.That(duration.Value, Is.EqualTo(30.0f).Within(0.1f)); // 30 seconds ±0.1s
}
```

## Current Status

- ? No test assets committed yet
- ? Tests written (will be skipped if assets missing)
- ? Manual testing required with real audio files

## Setup Instructions

1. Create `BoxieHub.Tests/test-assets/` directory
2. Download/create audio files as listed above
3. Run tests - they should pass if files are valid

---

**Note:** These files are for local testing only. CI/CD pipelines should use mocked audio analysis or small generated files.
