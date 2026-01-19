using BoxieHub.Data;
using BoxieHub.Models;
using Microsoft.EntityFrameworkCore;

namespace BoxieHub.Services.Import;

/// <summary>
/// Implementation of import job service
/// Handles CRUD operations for import jobs
/// </summary>
public class ImportJobService : IImportJobService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IYouTubeImportService _youtubeService;
    private readonly IPodcastImportService _podcastService;
    private readonly ILogger<ImportJobService> _logger;

    public ImportJobService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IYouTubeImportService youtubeService,
        IPodcastImportService podcastService,
        ILogger<ImportJobService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _youtubeService = youtubeService;
        _podcastService = podcastService;
        _logger = logger;
    }

    public async Task<ImportJob> CreateYouTubeImportJobAsync(
        string userId,
        string youtubeUrl,
        string? customTitle = null,
        string? customDescription = null,
        string? category = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating YouTube import job for user {UserId}: {Url}", 
            userId, youtubeUrl);

        // Validate URL and fetch metadata
        var videoInfo = await _youtubeService.GetVideoInfoAsync(youtubeUrl, ct);
        
        if (!videoInfo.IsValid)
        {
            throw new InvalidOperationException(
                videoInfo.ErrorMessage ?? "Invalid YouTube URL");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        // Truncate fields to fit database constraints
        var title = customTitle ?? videoInfo.Title;
        if (title?.Length > 500)
        {
            title = title.Substring(0, 497) + "...";
            _logger.LogWarning("Truncated title for video {VideoId} from {OriginalLength} to 500 characters", 
                videoInfo.Id, videoInfo.Title?.Length);
        }
        
        var description = customDescription ?? videoInfo.Description;
        if (description?.Length > 2000)
        {
            description = description.Substring(0, 1997) + "...";
            _logger.LogWarning("Truncated description for video {VideoId} from {OriginalLength} to 2000 characters", 
                videoInfo.Id, videoInfo.Description?.Length);
        }
        
        var thumbnailUrl = videoInfo.ThumbnailUrl;
        if (thumbnailUrl?.Length > 1024)
        {
            thumbnailUrl = thumbnailUrl.Substring(0, 1024);
            _logger.LogWarning("Truncated thumbnail URL for video {VideoId} from {OriginalLength} to 1024 characters", 
                videoInfo.Id, videoInfo.ThumbnailUrl?.Length);
        }

        var job = new ImportJob
        {
            UserId = userId,
            Source = ImportSource.YouTube,
            SourceUrl = youtubeUrl,
            SourceTitle = title,
            SourceDescription = description,
            SourceThumbnailUrl = thumbnailUrl,
            SourceDurationSeconds = (float)videoInfo.Duration.TotalSeconds,
            Category = category ?? "Other",
            StatusEnum = ImportJobStatus.Pending,
            StatusMessage = "Waiting to start...",
            Created = DateTimeOffset.UtcNow
        };

        dbContext.ImportJobs.Add(job);
        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created import job {JobId} for video: {Title} (Category: {Category})", 
            job.Id, videoInfo.Title, job.Category);

        return job;
    }
    
    public async Task<List<ImportJob>> CreateYouTubeBatchImportAsync(
        string userId,
        List<string> youtubeUrls,
        string? category = null,
        string? tags = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating batch YouTube import for user {UserId}: {Count} videos (Category: {Category}, Tags: {Tags})", 
            userId, youtubeUrls.Count, category ?? "Other", tags ?? "none");

        // Fetch all video info in parallel for much faster processing
        _logger.LogInformation("Fetching video info for {Count} videos in parallel", youtubeUrls.Count);
        var videoInfoTasks = youtubeUrls.Select(url => 
            _youtubeService.GetVideoInfoAsync(url, ct)).ToList();

        var videoInfoResults = await Task.WhenAll(videoInfoTasks);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        var jobs = new List<ImportJob>();

        for (int i = 0; i < youtubeUrls.Count; i++)
        {
            var url = youtubeUrls[i];
            var videoInfo = videoInfoResults[i];

            if (!videoInfo.IsValid)
            {
                _logger.LogWarning("Skipping invalid video {Url}: {Error}", url, videoInfo.ErrorMessage);
                continue;
            }

            // Truncate fields to fit database constraints
            var title = videoInfo.Title;
            if (title?.Length > 500)
            {
                title = title.Substring(0, 497) + "...";
                _logger.LogWarning("Truncated title for video {VideoId} from {OriginalLength} to 500 characters", 
                    videoInfo.Id, videoInfo.Title?.Length);
            }

            var description = videoInfo.Description;
            if (description?.Length > 2000)
            {
                description = description.Substring(0, 1997) + "...";
                _logger.LogWarning("Truncated description for video {VideoId} from {OriginalLength} to 2000 characters", 
                    videoInfo.Id, videoInfo.Description?.Length);
            }

            var thumbnailUrl = videoInfo.ThumbnailUrl;
            if (thumbnailUrl?.Length > 1024)
            {
                thumbnailUrl = thumbnailUrl.Substring(0, 1024);
                _logger.LogWarning("Truncated thumbnail URL for video {VideoId} from {OriginalLength} to 1024 characters", 
                    videoInfo.Id, videoInfo.ThumbnailUrl?.Length);
            }

            var job = new ImportJob
            {
                UserId = userId,
                Source = ImportSource.YouTube,
                SourceUrl = url,
                SourceTitle = title,
                SourceDescription = description,
                SourceThumbnailUrl = thumbnailUrl,
                SourceDurationSeconds = (float)videoInfo.Duration.TotalSeconds,
                Category = category ?? "Other", // Use provided category or default to "Other"
                Tags = tags, // Apply tags to all videos in the batch
                StatusEnum = ImportJobStatus.Pending,
                StatusMessage = "Waiting to start...",
                Created = DateTimeOffset.UtcNow
            };

            jobs.Add(job);
        }

        // Save all jobs in one transaction - much faster than individual saves
        _logger.LogInformation("Saving {Count} import jobs to database in bulk", jobs.Count);
        dbContext.ImportJobs.AddRange(jobs);
        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Successfully created {SuccessCount}/{TotalCount} import jobs with category '{Category}'", 
            jobs.Count, youtubeUrls.Count, category ?? "Other");

        return jobs;
    }

    public async Task<ImportJob?> GetJobAsync(int jobId, string userId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        
        return await dbContext.ImportJobs
            .Where(j => j.Id == jobId && j.UserId == userId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<ImportJob>> GetUserJobsAsync(
        string userId, 
        int take = 20, 
        CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        
        return await dbContext.ImportJobs
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.Created)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<bool> CancelJobAsync(int jobId, string userId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var job = await dbContext.ImportJobs
            .Where(j => j.Id == jobId && j.UserId == userId)
            .FirstOrDefaultAsync(ct);

        if (job == null)
            return false;

        // Can only cancel pending or running jobs
        if (job.StatusEnum is ImportJobStatus.Completed or ImportJobStatus.Failed or ImportJobStatus.Cancelled)
            return false;

        job.StatusEnum = ImportJobStatus.Cancelled;
        job.StatusMessage = "Cancelled by user";
        job.CompletedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(ct);
        
        _logger.LogInformation("Cancelled import job {JobId}", jobId);
        
        return true;
    }
    
    public async Task<ImportJob> CreatePodcastImportJobAsync(
        string userId,
        string feedUrl,
        PodcastEpisode episode,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating podcast import job for user {UserId}: {Title}",
            userId, episode.Title);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        // Truncate fields to fit database constraints
        var title = episode.Title;
        if (title?.Length > 500)
        {
            title = title.Substring(0, 497) + "...";
        }

        var description = episode.Description;
        if (description?.Length > 2000)
        {
            description = description.Substring(0, 1997) + "...";
        }

        var imageUrl = episode.ImageUrl;
        if (imageUrl?.Length > 1024)
        {
            imageUrl = imageUrl.Substring(0, 1024);
        }

        var job = new ImportJob
        {
            UserId = userId,
            Source = ImportSource.Podcast,
            SourceUrl = episode.AudioUrl,
            SourceTitle = title,
            SourceDescription = description,
            SourceThumbnailUrl = imageUrl,
            SourceDurationSeconds = (float)episode.Duration.TotalSeconds,
            StatusEnum = ImportJobStatus.Pending,
            StatusMessage = "Waiting to start...",
            Created = DateTimeOffset.UtcNow
        };

        dbContext.ImportJobs.Add(job);
        await dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created podcast import job {JobId} for episode: {Title}",
            job.Id, episode.Title);

        return job;
    }
}
