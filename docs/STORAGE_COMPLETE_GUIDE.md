# BoxieHub Storage Setup Guide

## Overview

BoxieHub supports multiple storage backends for audio files:
- **S3-Compatible Storage** (Default) - MinIO (dev) / Railway S3 (prod)
- **Dropbox** (Coming in Phase 4)
- **Google Drive** (Coming in Phase 4)
- **Database** (Fallback only)

---

## ?? Quick Start (Local Development)

### Prerequisites
- Docker Desktop installed
- .NET 8 SDK
- PostgreSQL (local or Docker)

### Step 1: Start MinIO with Docker Compose

```bash
# From the root of the BoxieHub project
docker-compose up -d
```

This starts MinIO on:
- **S3 API**: http://localhost:9000
- **Web Console**: http://localhost:9001

### Step 2: Create the S3 Bucket

1. Open MinIO Console: http://localhost:9001
2. Login with:
   - **Username**: `boxiehub`
   - **Password**: `boxiehub-dev-password-123`
3. Click **"Buckets"** ? **"Create Bucket"**
4. Bucket name: `boxiehub-media`
5. Click **"Create Bucket"**

### Step 3: Verify Configuration

Your `appsettings.Development.json` should have:

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

### Step 4: Run the Application

```bash
dotnet run --project BoxieHub
```

Navigate to `https://localhost:5001` and test file uploads!

---

## ??? Production Setup (Railway)

### Option 1: Railway S3 Plugin (Recommended)

Railway provides a built-in S3-compatible storage plugin:

1. **Add S3 Plugin to Railway Project**
   - Go to your Railway project
   - Click "New" ? "Database" ? "Add S3"
   - Railway will provision an S3 bucket

2. **Get Credentials**
   Railway will provide:
   - `S3_BUCKET_NAME`
   - `S3_ENDPOINT` (Service URL)
   - `S3_ACCESS_KEY_ID`
   - `S3_SECRET_ACCESS_KEY`

3. **Set Environment Variables in Railway**
   ```bash
   S3Storage__BucketName=your-bucket-name
   S3Storage__Region=us-east-1
   S3Storage__ServiceUrl=https://your-railway-s3-url
   S3Storage__AccessKey=your-access-key
   S3Storage__SecretKey=your-secret-key
   S3Storage__ForcePathStyle=false
   ```

### Option 2: AWS S3 (Alternative)

If you prefer to use AWS S3 directly:

1. **Create S3 Bucket**
   - Login to AWS Console
   - Go to S3
   - Create bucket: `boxiehub-media-prod`
   - Region: `us-east-1` (or your preferred region)
   - Keep bucket private (block all public access)

2. **Create IAM User**
   - Go to IAM ? Users ? Create User
   - User name: `boxiehub-s3-user`
   - Attach policy: `AmazonS3FullAccess` (or create custom policy)

3. **Get Access Keys**
   - Go to user ? Security Credentials
   - Create Access Key
   - Save Access Key ID and Secret Access Key

4. **Set Environment Variables in Railway**
   ```bash
   S3Storage__BucketName=boxiehub-media-prod
   S3Storage__Region=us-east-1
   # ServiceUrl not needed for AWS S3 (uses default)
   S3Storage__AccessKey=AKIA...
   S3Storage__SecretKey=...
   S3Storage__ForcePathStyle=false
   ```

### Option 3: Other S3-Compatible Services

BoxieHub works with any S3-compatible service:
- **DigitalOcean Spaces**
- **Backblaze B2**
- **Wasabi**
- **Cloudflare R2**

Just provide the appropriate `ServiceUrl` and credentials.

---

## ?? Future: Dropbox Integration (Phase 4)

### What You'll Need

1. **Create Dropbox App**
   - Go to https://www.dropbox.com/developers/apps
   - Create new app
   - Choose "Scoped access"
   - Choose "Full Dropbox" access
   - Name: "BoxieHub"

2. **Get OAuth Credentials**
   - App Key (Client ID)
   - App Secret (Client Secret)
   - Redirect URI: `https://your-domain.com/auth/dropbox/callback`

3. **Configuration** (Future)
   ```json
   {
     "Dropbox": {
       "ClientId": "your-app-key",
       "ClientSecret": "your-app-secret",
       "RedirectUri": "https://your-domain.com/auth/dropbox/callback"
     }
   }
   ```

### User Flow (Future Implementation)

1. User goes to Storage Settings
2. Clicks "Connect Dropbox"
3. Redirected to Dropbox OAuth
4. User authorizes BoxieHub
5. Access token stored encrypted in `UserStorageAccounts` table
6. Files uploaded to `/Apps/BoxieHub/` in user's Dropbox

---

## ?? Future: Google Drive Integration (Phase 4)

### What You'll Need

1. **Create Google Cloud Project**
   - Go to https://console.cloud.google.com
   - Create new project: "BoxieHub"
   - Enable Google Drive API

2. **Create OAuth 2.0 Credentials**
   - Go to APIs & Services ? Credentials
   - Create OAuth 2.0 Client ID
   - Application type: Web application
   - Authorized redirect URIs: `https://your-domain.com/auth/google/callback`

3. **Get Credentials**
   - Client ID
   - Client Secret

4. **Configuration** (Future)
   ```json
   {
     "GoogleDrive": {
       "ClientId": "your-client-id.apps.googleusercontent.com",
       "ClientSecret": "your-client-secret",
       "RedirectUri": "https://your-domain.com/auth/google/callback",
       "Scopes": ["https://www.googleapis.com/auth/drive.file"]
     }
   }
   ```

### User Flow (Future Implementation)

1. User goes to Storage Settings
2. Clicks "Connect Google Drive"
3. Redirected to Google OAuth
4. User authorizes BoxieHub
5. Access token stored encrypted in `UserStorageAccounts` table
6. Files uploaded to "BoxieHub" folder in user's Drive

---

## ?? Storage Architecture

### Current Implementation

```
BoxieHub
??? IFileStorageService (Interface)
??? S3FileStorageService (S3-compatible)
??? DatabaseFileStorageService (Fallback)
??? ServiceRegistrationExtensions (Factory pattern)
```

### File Storage Path Structure

**S3 Storage:**
```
boxiehub-media/
??? users/
    ??? {userId}/
        ??? {guid}/
            ??? {filename}
```

**Dropbox (Future):**
```
/Apps/BoxieHub/
??? {userId}/
    ??? {filename}
```

**Google Drive (Future):**
```
BoxieHub/
??? {filename}
```

### Database Schema

**UserStorageAccounts** (For OAuth providers)
```sql
CREATE TABLE "UserStorageAccounts" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" VARCHAR(450) NOT NULL,
    "Provider" VARCHAR(50) NOT NULL, -- "Dropbox", "GoogleDrive"
    "DisplayName" VARCHAR(200),
    "AccessToken" TEXT NOT NULL, -- Encrypted
    "RefreshToken" TEXT, -- Encrypted
    "ExpiresAt" TIMESTAMPTZ,
    "IsActive" BOOLEAN NOT NULL DEFAULT true,
    "Created" TIMESTAMPTZ NOT NULL,
    "Modified" TIMESTAMPTZ NOT NULL
);
```

**FileUpload** (References storage)
```sql
CREATE TABLE "FileUploads" (
    "Id" UUID PRIMARY KEY,
    "Provider" VARCHAR(50) NOT NULL, -- "S3Railway", "Dropbox", "GoogleDrive", "Database"
    "StoragePath" TEXT, -- Path in storage provider
    "StorageAccountId" INT NULL, -- FK to UserStorageAccounts (for OAuth providers)
    "Data" BYTEA NULL, -- Only for Database provider
    "ContentType" VARCHAR(100),
    "FileName" VARCHAR(256),
    "FileCategory" VARCHAR(50),
    "FileSizeBytes" BIGINT,
    "Created" TIMESTAMPTZ NOT NULL
);
```

---

## ?? Testing Storage

### Test File Upload (Development)

```bash
# Start MinIO
docker-compose up -d

# Check MinIO is running
curl http://localhost:9000/minio/health/live

# Run the app
dotnet run --project BoxieHub

# Test upload via UI
# 1. Navigate to http://localhost:5001
# 2. Login
# 3. Go to Media Library
# 4. Upload a file
```

### Verify Files in MinIO Console

1. Open http://localhost:9001
2. Login (boxiehub / boxiehub-dev-password-123)
3. Click "Buckets" ? "boxiehub-media"
4. Browse files under `users/...`

### Test with AWS CLI (Optional)

```bash
# Configure AWS CLI for MinIO
aws configure --profile minio
# Access Key: boxiehub
# Secret Key: boxiehub-dev-password-123
# Region: us-east-1
# Output: json

# List buckets
aws --profile minio --endpoint-url http://localhost:9000 s3 ls

# List files in bucket
aws --profile minio --endpoint-url http://localhost:9000 s3 ls s3://boxiehub-media/

# Download a file
aws --profile minio --endpoint-url http://localhost:9000 s3 cp s3://boxiehub-media/users/.../file.mp3 ./
```

---

## ?? Troubleshooting

### MinIO Won't Start

**Problem:** Port 9000 or 9001 already in use

**Solution:**
```bash
# Check what's using the port
netstat -ano | findstr :9000
netstat -ano | findstr :9001

# Stop the process or change ports in docker-compose.yml
# Example: Change to 9002:9000
```

### Bucket Not Found Error

**Problem:** App can't find the S3 bucket

**Solution:**
1. Verify MinIO is running: `docker ps`
2. Check bucket exists in MinIO Console
3. Verify `appsettings.Development.json` has correct bucket name
4. Restart the app

### Connection Refused

**Problem:** Can't connect to MinIO

**Solution:**
1. Check Docker is running
2. Check MinIO container status: `docker ps -a`
3. Check logs: `docker logs boxiehub-minio`
4. Restart: `docker-compose restart minio`

### Files Not Uploading

**Problem:** Upload fails silently

**Solution:**
1. Check app logs for errors
2. Verify S3 credentials in `appsettings.Development.json`
3. Check MinIO access policy (should be private)
4. Verify bucket exists and is accessible

### Production Deployment Issues

**Problem:** Files work in dev but not production

**Solution:**
1. Verify environment variables in Railway:
   ```bash
   railway variables
   ```
2. Check Railway S3 plugin is provisioned
3. Verify `ForcePathStyle` is `false` for AWS S3, `true` for MinIO
4. Check Railway logs for connection errors

---

## ?? Storage Comparison

| Provider | Free Tier | Pros | Cons | Status |
|----------|-----------|------|------|--------|
| **BoxieHub S3** | Included | Fast, automatic, no setup | Managed by BoxieHub | ? Active |
| **AWS S3** | 5GB/12mo | Reliable, scalable | Costs after free tier | ? Compatible |
| **Railway S3** | Varies | Integrated, easy | Railway-specific | ? Active (Prod) |
| **Dropbox** | 2GB | Familiar, user-owned | Limited free space | ? Phase 4 |
| **Google Drive** | 15GB | Large free tier | OAuth complexity | ? Phase 4 |
| **Database** | Unlimited | Simple | Slow for large files | ? Fallback |

---

## ?? Next Steps

### Current Features (? Complete)
- ? S3-compatible storage (MinIO/Railway)
- ? Automatic provider selection (dev/prod)
- ? File upload/download/delete
- ? Media library with S3 storage

### Phase 4 (? Coming Soon)
- ? Dropbox OAuth integration
- ? Google Drive OAuth integration
- ? User storage account management
- ? Storage provider selection in UI
- ? File migration between providers
- ? Storage usage tracking/quotas

---

## ?? Additional Resources

- [MinIO Documentation](https://min.io/docs/minio/linux/index.html)
- [AWS S3 Documentation](https://docs.aws.amazon.com/s3/)
- [Railway S3 Plugin](https://docs.railway.app/databases/s3)
- [Dropbox API](https://www.dropbox.com/developers/documentation)
- [Google Drive API](https://developers.google.com/drive)

---

## ?? Need Help?

1. Check the [STORAGE_SETUP.md](./STORAGE_SETUP.md) (original guide)
2. Review the [Media Library Roadmap](./MEDIA_LIBRARY_ROADMAP.md)
3. Check application logs: `dotnet run --project BoxieHub`
4. Check Docker logs: `docker logs boxiehub-minio`
5. Open an issue on GitHub

---

**Happy Storing! ??**
