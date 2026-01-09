# BoxieHub Storage Infrastructure

## Overview

BoxieHub uses S3-compatible storage for media files (audio, images, etc.):
- **Development**: MinIO (local Docker container)
- **Production**: Railway S3 or AWS S3

## Quick Start (Development)

### 1. Start MinIO with Docker Compose

```bash
docker-compose up -d
```

This starts MinIO on:
- **S3 API**: http://localhost:9000
- **Web Console**: http://localhost:9001

### 2. Access MinIO Console

1. Open http://localhost:9001 in your browser
2. Login with:
   - Username: `boxiehub`
   - Password: `boxiehub-dev-password-123`

### 3. Create the Bucket

1. In MinIO Console, click **"Create Bucket"**
2. Bucket name: `boxiehub-media`
3. Click **"Create"**

### 4. Run Migrations

```bash
cd BoxieHub
dotnet ef migrations add AddStorageInfrastructure
dotnet ef database update
```

### 5. Run the Application

```bash
dotnet run --project BoxieHub
```

## Configuration

### Development (appsettings.Development.json)

```json
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

### Production (Environment Variables)

Set these environment variables in Railway:

```bash
S3Storage__BucketName=boxiehub-media
S3Storage__Region=us-east-1
S3Storage__ServiceUrl=https://your-railway-s3.railway.app
S3Storage__AccessKey=your-access-key
S3Storage__SecretKey=your-secret-key
S3Storage__ForcePathStyle=false
```

## Storage Providers

BoxieHub supports multiple storage backends:

### 1. Database (Legacy)
- Small images only
- Stored in PostgreSQL
- **Not recommended** for audio files

### 2. S3 Railway (Recommended)
- BoxieHub-managed storage
- S3-compatible (MinIO dev, Railway prod)
- Best for most users

### 3. Dropbox (Coming Soon - Phase 4)
- User's personal Dropbox account
- OAuth 2.0 authentication
- Free 2GB storage

### 4. Google Drive (Coming Soon - Phase 4)
- User's personal Google Drive
- OAuth 2.0 authentication
- Free 15GB storage

## File Organization

### S3 Storage Structure

```
boxiehub-media/
??? users/
?   ??? user-id-1/
?   ?   ??? guid-1/
?   ?   ?   ??? audio-file.mp3
?   ?   ??? guid-2/
?   ?   ?   ??? another-file.mp3
?   ??? user-id-2/
?       ??? guid-3/
?           ??? music.mp3
```

### Dropbox Structure (Phase 4)

```
/Apps/BoxieHub/
??? user-id/
    ??? audio-file.mp3
    ??? another-file.mp3
```

## Docker Commands

### Start MinIO

```bash
docker-compose up -d
```

### Stop MinIO

```bash
docker-compose down
```

### View Logs

```bash
docker-compose logs -f minio
```

### Reset MinIO Data

```bash
docker-compose down -v
docker-compose up -d
```

## Troubleshooting

### MinIO Not Starting

1. Check if port 9000 or 9001 is already in use:
   ```bash
   netstat -ano | findstr :9000
   netstat -ano | findstr :9001
   ```

2. Stop conflicting processes or change ports in `docker-compose.yml`

### Bucket Not Found Error

1. Open MinIO Console: http://localhost:9001
2. Verify `boxiehub-media` bucket exists
3. If not, create it manually

### Connection Refused

1. Ensure Docker is running
2. Check MinIO container status:
   ```bash
   docker ps
   ```
3. Restart if needed:
   ```bash
   docker-compose restart minio
   ```

## Testing Storage

### Upload a Test File

```csharp
var storageService = serviceProvider.GetRequiredService<IFileStorageService>();

using var stream = File.OpenRead("test-audio.mp3");
var storagePath = await storageService.UploadFileAsync(
    stream,
    "test-audio.mp3",
    "audio/mpeg",
    "user-id-123"
);

Console.WriteLine($"Uploaded to: {storagePath}");
```

### Download a File

```csharp
var fileStream = await storageService.DownloadFileAsync(storagePath);
using var outputFile = File.Create("downloaded.mp3");
await fileStream.CopyToAsync(outputFile);
```

### Check if File Exists

```csharp
var exists = await storageService.FileExistsAsync(storagePath);
Console.WriteLine($"File exists: {exists}");
```

## Migration Path

### From Database Storage to S3

BoxieHub automatically uses S3 storage for new uploads when configured. Existing database-stored files remain in the database until you migrate them:

```csharp
// Future migration script (Phase 1 cleanup)
var databaseFiles = await dbContext.FileUploads
    .Where(f => f.Provider == StorageProvider.Database && f.FileCategory == "Audio")
    .ToListAsync();

foreach (var file in databaseFiles)
{
    if (file.Data != null)
    {
        using var stream = new MemoryStream(file.Data);
        var storagePath = await s3Storage.UploadFileAsync(
            stream, file.FileName, file.ContentType, file.UserId
        );
        
        file.Provider = StorageProvider.S3Railway;
        file.StoragePath = storagePath;
        file.Data = null; // Clear database blob
    }
}

await dbContext.SaveChangesAsync();
```

## Security

### Access Control

- S3 buckets are **private** by default
- Files accessed through BoxieHub API only
- Signed URLs for temporary access

### Encryption

- Files encrypted at rest (S3/MinIO feature)
- OAuth tokens encrypted in database
- HTTPS in production

## Monitoring

### MinIO Metrics

Access metrics at: http://localhost:9000/minio/v2/metrics/cluster

### Storage Usage

Query from database:

```sql
SELECT 
    Provider,
    COUNT(*) as FileCount,
    SUM(FileSizeBytes) / (1024*1024*1024.0) as TotalGB
FROM FileUploads
GROUP BY Provider;
```

## Next Steps

- ? Phase 1: Storage Infrastructure (Complete)
- ? Phase 2: Media Library Upload/Browse
- ? Phase 3: Add Library Item to Tonie
- ? Phase 4: Dropbox & Google Drive OAuth

---

**Need Help?** Check the main [Media Library Roadmap](docs/MEDIA_LIBRARY_ROADMAP.md)
