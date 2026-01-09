# Contributing to BoxieHub ??

Thank you for considering contributing to BoxieHub! We welcome contributions from everyone.

## ?? Ways to Contribute

### ?? Report Bugs
- Use the [Issue Tracker](https://github.com/OakesekAo/BoxieHub/issues)
- Search existing issues first
- Include clear reproduction steps
- Mention your environment (.NET version, OS, browser)

### ?? Suggest Features
- Open a [Feature Request](https://github.com/OakesekAo/BoxieHub/issues/new?labels=enhancement)
- Describe the use case clearly
- Explain expected behavior
- Consider how it fits with existing features

### ?? Improve Documentation
- Fix typos or unclear explanations
- Add examples and screenshots
- Update outdated information
- Translate documentation

### ?? Submit Code
- Fix bugs
- Add new features
- Improve performance
- Write tests

---

## ?? Getting Started

### Prerequisites
- .NET 8 SDK
- PostgreSQL 14+
- Git
- Your favorite IDE (Visual Studio, VS Code, Rider)

### Fork & Clone
```bash
# Fork on GitHub, then clone your fork
git clone https://github.com/YOUR_USERNAME/BoxieHub.git
cd BoxieHub
```

### Set Up Database
```bash
# Create PostgreSQL database
createdb boxiehub_dev

# Configure connection string
cd BoxieHub
dotnet user-secrets set "ConnectionStrings:DBConnection" "Host=localhost;Database=boxiehub_dev;Username=postgres;Password=yourpassword"

# Run migrations
dotnet ef database update
```

### Run the Application
```bash
dotnet run
# Navigate to https://localhost:7120
```

### Run Tests
```bash
cd BoxieHub.Tests
dotnet test
```

---

## ?? Development Workflow

### 1. Create a Branch
```bash
git checkout -b feature/your-feature-name
# or
git checkout -b bugfix/issue-number-description
```

**Branch Naming:**
- `feature/` - New features
- `bugfix/` - Bug fixes
- `docs/` - Documentation updates
- `refactor/` - Code refactoring
- `test/` - Adding tests

### 2. Make Your Changes

**Code Style:**
- Follow existing code conventions
- Use meaningful variable names
- Add comments for complex logic
- Keep methods focused and small

**Commit Messages:**
- Use present tense ("Add feature" not "Added feature")
- First line: brief summary (50 chars max)
- Blank line, then detailed description if needed
- Reference issue numbers

Example:
```
Add YouTube playlist import feature

- Implement GetPlaylistInfoAsync() method
- Create ImportPlaylist.razor component
- Add batch import support
- Closes #42
```

### 3. Write Tests

**Required for:**
- New features (unit tests)
- Bug fixes (regression tests)
- Service layer changes (integration tests)

**Test Guidelines:**
- Follow Arrange-Act-Assert pattern
- One assertion per test (when possible)
- Use descriptive test names
- Mock external dependencies

Example:
```csharp
[Fact]
public async Task CreateYouTubeImportJobAsync_WithValidUrl_CreatesJob()
{
    // Arrange
    var service = CreateService();
    var url = "https://www.youtube.com/watch?v=test";
    
    // Act
    var job = await service.CreateYouTubeImportJobAsync("user123", url);
    
    // Assert
    Assert.NotNull(job);
    Assert.Equal(url, job.SourceUrl);
    Assert.Equal(ImportJobStatus.Pending, job.StatusEnum);
}
```

### 4. Run Tests & Build
```bash
# Run all tests
dotnet test

# Run specific category
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration

# Build
dotnet build

# Check for warnings
dotnet build --warnaserror
```

### 5. Push & Create PR
```bash
git push origin feature/your-feature-name
```

Then create a Pull Request on GitHub:
- Give it a clear title
- Reference related issues
- Describe what changed and why
- Add screenshots for UI changes

---

## ?? Code Standards

### C# Style
- Use C# 12 features
- Follow Microsoft naming conventions
- Use `var` for obvious types
- Prefer `async`/`await` over `.Result`
- Use records for DTOs

### Blazor Components
- One component per file
- Use code-behind for complex logic
- Implement `IDisposable` when needed
- Use `@rendermode InteractiveServer` for interactivity

### Database
- Create migrations for schema changes
- Never edit generated migration code
- Test migrations on fresh database
- Include rollback tests

### Services
- Interface for all services
- Dependency injection
- Async all the way
- Proper exception handling
- Logging at appropriate levels

---

## ?? Testing Standards

### Unit Tests
- Test one thing per test
- Mock external dependencies
- Use `xUnit` framework
- Use `Moq` for mocking
- Fast execution (<100ms)

### Integration Tests
- Test full workflows
- Use real database (in-memory or test DB)
- Clean up after each test
- Test happy path and edge cases

### Test Coverage
- Aim for >80% coverage
- Focus on business logic
- Don't test framework code
- Test error scenarios

---

## ?? Documentation Standards

### Code Comments
```csharp
/// <summary>
/// Brief description of what the method does
/// </summary>
/// <param name="userId">The user's ID</param>
/// <param name="ct">Cancellation token</param>
/// <returns>List of import jobs</returns>
/// <exception cref="ArgumentNullException">If userId is null</exception>
public async Task<List<ImportJob>> GetUserJobsAsync(
    string userId, 
    CancellationToken ct = default)
{
    // Implementation
}
```

### README Updates
- Keep feature list up to date
- Add screenshots for new UI
- Update installation steps if needed
- Document breaking changes

### Changelog
- Add entry to `CHANGELOG.md` (if exists)
- Categorize: Added, Changed, Fixed, Removed
- Include issue/PR references

---

## ?? Review Process

### What We Look For
- ? Code follows style guidelines
- ? Tests pass
- ? No merge conflicts
- ? Documentation updated
- ? Commits are clean
- ? PR description is clear

### Review Timeline
- Small PRs (<100 lines): 1-2 days
- Medium PRs (100-500 lines): 3-5 days
- Large PRs (>500 lines): 5-10 days

### After Review
- Address feedback promptly
- Push additional commits
- Mark conversations as resolved
- Request re-review

---

## ?? Priority Areas

### High Priority (Help Wanted!)
- ?? Bug fixes
- ?? Documentation improvements
- ?? Test coverage
- ? Accessibility improvements
- ?? Internationalization (i18n)

### Medium Priority
- ? New features (see roadmap)
- ?? UI/UX improvements
- ?? Performance optimizations

### Low Priority
- ?? Refactoring
- ?? Dependency updates
- ?? Code cleanup

---

## ?? Communication

### GitHub Issues
- Preferred for bugs and features
- Use templates when available
- Search before creating
- Provide context

### Pull Requests
- Use draft PRs for work-in-progress
- Request reviews from maintainers
- Respond to feedback
- Keep PRs focused

### Discussions
- Ask questions
- Share ideas
- Get help with setup
- General chat

---

## ?? Code of Conduct

### Our Standards
- Be respectful and inclusive
- Welcome newcomers
- Accept constructive criticism
- Focus on what's best for the community

### Unacceptable Behavior
- Harassment or discrimination
- Trolling or insulting comments
- Personal or political attacks
- Publishing private information

### Enforcement
- Violations may result in temporary or permanent ban
- Report violations to maintainers
- All reports are confidential

---

## ?? Recognition

### Contributors Wall
- All contributors listed in README
- Special thanks for major contributions
- Shoutouts in release notes

### Become a Maintainer
- Consistent quality contributions
- Help review other PRs
- Engage with community
- Maintain an area of the codebase

---

## ? Questions?

- ?? [GitHub Discussions](https://github.com/OakesekAo/BoxieHub/discussions)
- ?? Contact maintainers
- ?? Read the [Documentation](docs/)

---

**Thank you for contributing to BoxieHub! ??**
