# Changelog

All notable changes to BoxieHub will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- YouTube single video import with preview and metadata extraction
- YouTube playlist batch import (up to 50 videos)
- Background job processing for imports
- Progress tracking for active imports
- Media library with tags and categories
- S3-compatible storage support (MinIO, Railway S3, AWS S3)
- Custom Tonie image upload
- Chapter editing and reordering
- Multi-account Toniebox support
- Encrypted credential storage
- Real-time Tonie sync with caching
- Smart audio bitrate selection (mono for Toniebox)
- Emoji sanitization in filenames
- Description truncation for long YouTube descriptions

### Changed
- Improved error messages with full exception chains
- Optimized polling (stops when no active jobs)
- Switched to lowest bitrate audio for Toniebox imports

### Fixed
- Fixed varchar constraint violations for long descriptions
- Fixed HTTP header errors with emoji characters in filenames
- Fixed MediaLibraryItem description length issues
- Fixed cascading deletes when removing Tonie accounts

---

## [0.1.0] - 2026-01-09

### Added
- Initial release
- User authentication and authorization
- PostgreSQL database integration
- Basic Tonie management
- Audio file upload
- Tonie Cloud API integration

[Unreleased]: https://github.com/OakesekAo/BoxieHub/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/OakesekAo/BoxieHub/releases/tag/v0.1.0
