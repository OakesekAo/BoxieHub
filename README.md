# BoxieHub
# BoxieHub 🎵📦

**Self-hosted media management platform for Toniebox Creative Tonies**

BoxieHub is a modern, self-hosted web application that simplifies managing audio content for your Toniebox Creative Tonies. Import from YouTube, organize your media library, and sync content to your Tonies—all from one beautiful interface.

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-Interactive-512BD4?logo=blazor)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-14+-336791?logo=postgresql)](https://www.postgresql.org/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

---

## ✨ Features

### 🎵 **Media Library**
- **Import from YouTube** - Extract audio from any YouTube video (max 90 minutes)
- **Playlist Import** - Batch import up to 50 videos from YouTube playlists
- **Direct Upload** - Upload your own audio files (MP3, M4A, OGG, WAV)
- **Smart Organization** - Tag, categorize, and search your media
- **Reusable Content** - Use the same audio on multiple Tonies

### 🧸 **Tonie Management**
- **Multi-Account Support** - Connect multiple Toniebox accounts
- **Automatic Sync** - Pulls your Tonies, households, and chapters
- **Custom Images** - Upload custom artwork for your Tonies
- **Chapter Management** - Edit titles, reorder, delete chapters
- **Real-time Status** - See storage usage and transcoding status

### ☁️ **Flexible Storage**
- **Database Storage** - Store small files directly in PostgreSQL
- **S3-Compatible Storage** - MinIO, Railway S3, AWS S3 support
- **External Cloud** - Dropbox and Google Drive integration (coming soon)
- **User Preferences** - Choose default storage per user

### 🔒 **Security & Privacy**
- **Self-Hosted** - Your data stays on your server
- **Encrypted Credentials** - Tonie account passwords encrypted with ASP.NET Data Protection
- **Multi-User Support** - Each user has isolated content and settings
- **Role-Based Access** - Admin and user roles

### 🚀 **Modern Architecture**
- **Blazor Server + WebAssembly** - Fast, interactive UI
- **Background Processing** - YouTube imports run asynchronously
- **Smart Caching** - Database-first with API sync fallback
- **Responsive Design** - Works on desktop, tablet, and mobile

---

## 📸 Screenshots

### Media Library
<details>
<summary>View Screenshot</summary>

*Coming soon - Upload your media, search by tags, and see duration/size at a glance*

</details>

### YouTube Import
<details>
<summary>View Screenshot</summary>

*Coming soon - Paste a URL, preview the video, customize metadata, and import*

</details>

### Tonie Management
<details>
<summary>View Screenshot</summary>

*Coming soon - View all your Tonies with live storage status and chapter counts*

</details>

---

## 🚀 Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 14+](https://www.postgresql.org/download/)
- [MinIO](https://min.io/) or S3-compatible storage (optional)
- A [Toniebox account](https://meine.tonies.com/)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/OakesekAo/BoxieHub.git
   cd BoxieHub
   ```

2. **Set up the database**
   ```bash
   # Install PostgreSQL, then create a database
   createdb boxiehub
   ```

3. **Configure connection string**
   
   Create `BoxieHub/appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "DBConnection": "Host=localhost;Database=boxiehub;Username=postgres;Password=yourpassword"
     },
     "S3Storage": {
       "BucketName": "boxiehub-media",
       "Region": "us-east-1",
       "ServiceUrl": "http://localhost:9000",
       "AccessKey": "minioadmin",
       "SecretKey": "minioadmin",
       "ForcePathStyle": true
     }
   }
   ```

4. **Run database migrations**
   ```bash
   cd BoxieHub
   dotnet ef database update
   ```

5. **Start the application**
   ```bash
   dotnet run
   ```

6. **Open your browser**
   Navigate to `https://localhost:7120` and register an account!

---

## 🔧 Configuration

### Database (Required)

BoxieHub uses PostgreSQL for all data storage:

```json
{
  "ConnectionStrings": {
    "DBConnection": "Host=localhost;Database=boxiehub;Username=postgres;Password=yourpassword"
  }
}
```

### S3 Storage (Optional)

For external media storage, configure S3-compatible settings:

```json
{
  "S3Storage": {
    "BucketName": "boxiehub-media",
    "Region": "us-east-1",
    "ServiceUrl": "http://localhost:9000",  // MinIO or Railway S3
    "AccessKey": "your-access-key",
    "SecretKey": "your-secret-key",
    "ForcePathStyle": true
  }
}
```

**Supported S3 Providers:**
- ✅ MinIO (self-hosted)
- ✅ Railway S3
- ✅ AWS S3
- ✅ Any S3-compatible service

### User Secrets (Development)

For development, use .NET User Secrets to avoid committing credentials:

```bash
dotnet user-secrets set "ConnectionStrings:DBConnection" "Host=localhost;Database=boxiehub;Username=postgres;Password=yourpassword"
dotnet user-secrets set "S3Storage:AccessKey" "your-access-key"
dotnet user-secrets set "S3Storage:SecretKey" "your-secret-key"
```

---

## 📚 Documentation

### User Guides
- [Getting Started](docs/USER_GUIDE.md) *(coming soon)*
- [Importing from YouTube](docs/YOUTUBE_IMPORT.md) *(coming soon)*
- [Managing Tonies](docs/TONIE_MANAGEMENT.md) *(coming soon)*
- [Storage Configuration](docs/STORAGE_SETUP.md) *(coming soon)*

### Developer Guides
- [Architecture Overview](docs/ARCHITECTURE.md) *(coming soon)*
- [Contributing Guidelines](CONTRIBUTING.md) *(coming soon)*
- [API Documentation](docs/API.md) *(coming soon)*

### Technical Docs
- [Feature: Add to Library](docs/FEATURE_ADD_TO_LIBRARY.md)
- [User Story 8: YouTube Import](docs/USER_STORY_8_PHASE_6A_YOUTUBE_ONLY.md)
- [Storage System](docs/STORAGE_FIXES_COMPLETE.md)
- [Bug Fixes Log](docs/YOUTUBE_IMPORT_BUG_FIX.md)

---

## 🗺️ Roadmap

### ✅ Phase 1: Foundation (Complete)
- [x] User authentication & authorization
- [x] PostgreSQL database integration
- [x] Toniebox account management
- [x] Basic Tonie sync from Tonie Cloud API

### ✅ Phase 2: Media Library (Complete)
- [x] Upload audio files
- [x] Media library with tags & categories
- [x] S3-compatible storage integration
- [x] Reusable content system

### ✅ Phase 3: YouTube Import (Complete)
- [x] Single video import
- [x] Playlist batch import
- [x] Background processing
- [x] Progress tracking

### 🚧 Phase 4: Advanced Features (In Progress)
- [x] Custom Tonie images
- [x] Chapter editing & reordering
- [ ] Podcast RSS feed import
- [ ] Direct URL audio import
- [ ] Bulk operations

### 📋 Phase 5: Cloud Storage (Planned)
- [ ] Dropbox integration
- [ ] Google Drive integration
- [ ] OneDrive support
- [ ] Storage quota management

### 🎯 Phase 6: Polish & UX (Planned)
- [ ] Mobile app (MAUI)
- [ ] Dark mode
- [ ] Advanced search & filters
- [ ] Audio editing (trim, normalize)
- [ ] Batch audio processing

### 🌐 Phase 7: Community (Future)

- [ ] Content recommendations
- [ ] User profiles & avatars
- [ ] Activity feed

---

## 🏗️ Architecture

### Tech Stack

**Frontend:**
- Blazor Server (interactive components)
- Blazor WebAssembly (offline support)
- Bootstrap 5.3 (UI framework)
- Bootstrap Icons

**Backend:**
- ASP.NET Core 8.0
- Entity Framework Core 8.0
- PostgreSQL 14+
- ASP.NET Identity (authentication)

**Storage:**
- PostgreSQL (primary database)
- S3-compatible storage (media files)
- ASP.NET Data Protection (encryption)

**External APIs:**

- YoutubeExplode (video/playlist metadata)

### Project Structure

```
BoxieHub/
├── BoxieHub/                    # Main web application
│   ├── Components/              # Blazor components
│   │   ├── Pages/              # Routable pages
│   │   ├── Layout/             # Layout components
│   │   └── Account/            # Auth components
│   ├── Controllers/            # API controllers
│   ├── Data/                   # EF Core context
│   ├── Models/                 # Domain models
│   ├── Services/               # Business logic
│   │   ├── BoxieCloud/        # Tonie API client
│   │   ├── Import/            # YouTube import
│   │   ├── Storage/           # File storage
│   │   └── Sync/              # Background sync
│   └── Migrations/            # EF migrations
├── BoxieHub.Client/           # Blazor WebAssembly
├── BoxieHub.Tests/            # Unit & integration tests
└── docs/                      # Documentation
```

---

## 🧪 Testing

### Run Unit Tests
```bash
cd BoxieHub.Tests
dotnet test --filter Category=Unit
```

### Run Integration Tests
```bash
dotnet test --filter Category=Integration
```

### Test Coverage
- ✅ Unit tests for services
- ✅ Integration tests for workflows
- ✅ Controller tests with mocks
- 🚧 E2E tests (coming soon)

---

## 🤝 Contributing

We welcome contributions! Here's how you can help:

### 🐛 Report Bugs
- Use the [Issue Tracker](https://github.com/OakesekAo/BoxieHub/issues)
- Include steps to reproduce
- Mention your environment (.NET version, OS, browser)

### 💡 Suggest Features
- Open a [Feature Request](https://github.com/OakesekAo/BoxieHub/issues/new?labels=enhancement)
- Describe the use case
- Explain expected behavior

### 🔧 Submit Pull Requests
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### 📝 Development Setup
```bash
# Clone your fork
git clone https://github.com/YOUR_USERNAME/BoxieHub.git
cd BoxieHub

# Install dependencies
dotnet restore

# Run migrations
cd BoxieHub
dotnet ef database update

# Run the app
dotnet run

# Run tests
cd ../BoxieHub.Tests
dotnet test
```

---

## 📜 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

### Built With
- [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet) - Microsoft
- [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) - Microsoft
- [Entity Framework Core](https://docs.microsoft.com/ef/core/) - Microsoft
- [PostgreSQL](https://www.postgresql.org/) - PostgreSQL Global Development Group
- [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) - Tyrrrz
- [Bootstrap](https://getbootstrap.com/) - Twitter
- [Bootstrap Icons](https://icons.getbootstrap.com/) - Bootstrap Team

### Inspired By
- [Tonie Cloud API](https://api.tonie.cloud/) - Official Toniebox API
- [tonies®](https://tonies.com/) - For creating an amazing audio system for kids

### Special Thanks
- To all contributors who help make BoxieHub better
- To the .NET community for amazing tools and libraries
- To parents who want better control over their kids' media

---

## ⚠️ Disclaimer

**BoxieHub is an independent project and is not affiliated with, endorsed by, or sponsored by Boxine GmbH or tonies®.**

- This application uses the official Tonie Cloud API
- You must have a valid Toniebox account to use this application
- Your Tonie credentials are encrypted and stored securely
- Use of YouTube import features is subject to [YouTube's Terms of Service](https://www.youtube.com/t/terms)
- Respect copyright laws when importing content

---

## 📞 Contact & Support

### Need Help?
- 📖 [Documentation](docs/) - Read the guides
- 💬 [Discussions](https://github.com/OakesekAo/BoxieHub/discussions) - Ask questions
- 🐛 [Issues](https://github.com/OakesekAo/BoxieHub/issues) - Report bugs

### Stay Updated
- ⭐ Star this repository to show support
- 👀 Watch for updates and new releases
- 🔔 Subscribe to release notifications

---

## 📊 Project Stats

![GitHub stars](https://img.shields.io/github/stars/OakesekAo/BoxieHub?style=social)
![GitHub forks](https://img.shields.io/github/forks/OakesekAo/BoxieHub?style=social)
![GitHub watchers](https://img.shields.io/github/watchers/OakesekAo/BoxieHub?style=social)
![GitHub issues](https://img.shields.io/github/issues/OakesekAo/BoxieHub)
![GitHub pull requests](https://img.shields.io/github/issues-pr/OakesekAo/BoxieHub)
![GitHub last commit](https://img.shields.io/github/last-commit/OakesekAo/BoxieHub)

---

<div align="center">

**Made with ❤️ for parents and their little ones**

[⬆ Back to Top](#boxiehub-)

</div>
