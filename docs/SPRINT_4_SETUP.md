# Sprint 4 - Phase 1: Storage Infrastructure Setup

## Step 1: Install AWS SDK NuGet Package

```powershell
cd BoxieHub
dotnet add package AWSSDK.S3
dotnet restore
```

## Step 2: Start MinIO

```powershell
docker-compose up -d
```

Wait ~10 seconds for MinIO to start, then verify:
```powershell
docker ps
```

You should see `boxiehub-minio` running.

## Step 3: Create MinIO Bucket

1. Open MinIO Console: http://localhost:9001
2. Login:
   - Username: `boxiehub`
   - Password: `boxiehub-dev-password-123`
3. Click **"Create Bucket"**
4. Bucket name: `boxiehub-media`
5. Click **"Create"**

## Step 4: Create Database Migration

```powershell
cd BoxieHub
dotnet ef migrations add AddStorageInfrastructure --context ApplicationDbContext
```

## Step 5: Apply Migration

```powershell
dotnet ef database update --context ApplicationDbContext
```

## Step 6: Verify Setup

Run the application:
```powershell
dotnet run --project BoxieHub
```

Check logs for:
- ? S3 client configured
- ? Database migration applied
- ? No S3 connection errors

## Step 7: Test Storage (Optional)

You can test S3 storage by:
1. Uploading a test file via MinIO Console
2. Verifying it appears in the `boxiehub-media` bucket
3. Checking storage path: `users/{userId}/{guid}/{filename}`

## Troubleshooting

### MinIO Not Starting
```powershell
docker-compose logs minio
```

### Port Already in Use
Edit `docker-compose.yml` to use different ports:
```yaml
ports:
  - "9010:9000"      # Change 9000 to 9010
  - "9011:9001"      # Change 9001 to 9011
```

Then update `appsettings.Development.json`:
```json
"ServiceUrl": "http://localhost:9010"
```

### Migration Fails
Check connection string in User Secrets or appsettings.json

---

## What's Next?

After Phase 1 is complete, you can move to:
- **Phase 2.1**: Upload to Library page
- **Phase 2.2**: Library Index/Browse page

See [Media Library Roadmap](MEDIA_LIBRARY_ROADMAP.md) for details.
