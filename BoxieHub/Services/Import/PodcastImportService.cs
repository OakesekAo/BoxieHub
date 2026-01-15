using System.ServiceModel.Syndication;
using System.Xml;
using BoxieHub.Models;

namespace BoxieHub.Services.Import;

/// <summary>
/// Service for importing podcast episodes from RSS feeds
/// Supports RSS 2.0 and Atom formats
/// </summary>
public class PodcastImportService : IPodcastImportService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PodcastImportService> _logger;

    public PodcastImportService(
        IHttpClientFactory httpClientFactory,
        ILogger<PodcastImportService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        _logger = logger;
    }

    public async Task<PodcastFeedInfo> GetFeedInfoAsync(string rssUrl, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Fetching podcast RSS feed from: {Url}", rssUrl);

            // Download RSS feed
            using var response = await _httpClient.GetAsync(rssUrl, ct);
            response.EnsureSuccessStatusCode();

            // Parse RSS/Atom feed
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Ignore,
                MaxCharactersFromEntities = 1024 * 1024 // 1MB limit for safety
            });

            var feed = SyndicationFeed.Load(xmlReader);

            if (feed == null)
            {
                return new PodcastFeedInfo
                {
                    IsValid = false,
                    ErrorMessage = "Failed to parse RSS feed. Please check the URL."
                };
            }

            // Extract podcast metadata
            var podcastInfo = new PodcastFeedInfo
            {
                Title = feed.Title?.Text ?? "Unknown Podcast",
                Description = feed.Description?.Text ?? string.Empty,
                Author = feed.Authors.FirstOrDefault()?.Name ?? "Unknown",
                ImageUrl = ExtractImageUrl(feed),
                FeedUrl = rssUrl,
                Episodes = new List<PodcastEpisode>()
            };

            // Parse episodes
            foreach (var item in feed.Items.Take(50)) // Limit to 50 most recent episodes
            {
                try
                {
                    var episode = ParseEpisode(item, podcastInfo.ImageUrl);
                    if (episode != null && !string.IsNullOrEmpty(episode.AudioUrl))
                    {
                        podcastInfo.Episodes.Add(episode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse episode: {Title}", item.Title?.Text);
                }
            }

            if (!podcastInfo.Episodes.Any())
            {
                return new PodcastFeedInfo
                {
                    IsValid = false,
                    ErrorMessage = "No valid audio episodes found in this feed."
                };
            }

            _logger.LogInformation("Successfully parsed podcast: {Title} with {Count} episodes",
                podcastInfo.Title, podcastInfo.Episodes.Count);

            podcastInfo.IsValid = true;
            return podcastInfo;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching podcast feed: {Url}", rssUrl);
            return new PodcastFeedInfo
            {
                IsValid = false,
                ErrorMessage = $"Failed to download feed: {ex.Message}"
            };
        }
        catch (XmlException ex)
        {
            _logger.LogWarning(ex, "XML parsing error for feed: {Url}", rssUrl);
            return new PodcastFeedInfo
            {
                IsValid = false,
                ErrorMessage = "Invalid RSS feed format. Please check the URL."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing podcast feed: {Url}", rssUrl);
            return new PodcastFeedInfo
            {
                IsValid = false,
                ErrorMessage = $"Error parsing feed: {ex.Message}"
            };
        }
    }

    public async Task<Stream> DownloadEpisodeAsync(
        string audioUrl,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Downloading podcast episode from: {Url}", audioUrl);

            using var response = await _httpClient.GetAsync(audioUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength ?? 0;
            var buffer = new byte[8192];
            var totalBytesRead = 0L;

            var memoryStream = new MemoryStream();

            using var downloadStream = await response.Content.ReadAsStreamAsync(ct);

            while (true)
            {
                var bytesRead = await downloadStream.ReadAsync(buffer, ct);
                if (bytesRead == 0) break;

                await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalBytesRead += bytesRead;

                // Report progress
                if (progress != null && contentLength > 0)
                {
                    var progressPercentage = (double)totalBytesRead / contentLength;
                    progress.Report(progressPercentage);
                }
            }

            memoryStream.Position = 0;

            _logger.LogInformation("Successfully downloaded episode ({Size} bytes)", memoryStream.Length);

            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download podcast episode from: {Url}", audioUrl);
            throw;
        }
    }

    /// <summary>
    /// Parse a single episode from RSS item
    /// </summary>
    private PodcastEpisode? ParseEpisode(SyndicationItem item, string fallbackImageUrl)
    {
        // Find audio enclosure
        var audioEnclosure = item.Links
            .FirstOrDefault(l => l.RelationshipType == "enclosure" &&
                                 l.MediaType?.StartsWith("audio/") == true);

        if (audioEnclosure == null)
        {
            // Try alternative approach: check all links for audio extensions
            audioEnclosure = item.Links
                .FirstOrDefault(l => l.Uri.AbsoluteUri.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                                     l.Uri.AbsoluteUri.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) ||
                                     l.Uri.AbsoluteUri.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase));
        }

        if (audioEnclosure == null)
            return null;

        var episode = new PodcastEpisode
        {
            Id = item.Id ?? audioEnclosure.Uri.AbsoluteUri,
            Title = item.Title?.Text ?? "Untitled Episode",
            Description = StripHtml(item.Summary?.Text ?? string.Empty),
            AudioUrl = audioEnclosure.Uri.AbsoluteUri,
            ContentType = audioEnclosure.MediaType ?? GuessContentType(audioEnclosure.Uri.AbsoluteUri),
            FileSizeBytes = audioEnclosure.Length,
            PublishDate = item.PublishDate,
            ImageUrl = ExtractEpisodeImageUrl(item) ?? fallbackImageUrl
        };

        // Try to extract duration from iTunes extensions
        episode.Duration = ExtractDuration(item);

        return episode;
    }

    /// <summary>
    /// Extract podcast image URL from feed
    /// </summary>
    private string ExtractImageUrl(SyndicationFeed feed)
    {
        // Try iTunes image first
        var itunesImage = feed.ElementExtensions
            .FirstOrDefault(e => e.OuterName == "image" &&
                                 e.OuterNamespace == "http://www.itunes.com/dtds/podcast-1.0.dtd");

        if (itunesImage != null)
        {
            try
            {
                var imageElement = itunesImage.GetObject<XmlElement>();
                var href = imageElement?.GetAttribute("href");
                if (!string.IsNullOrEmpty(href))
                    return href;
            }
            catch { }
        }

        // Try standard image
        if (feed.ImageUrl != null)
            return feed.ImageUrl.ToString();

        // Fallback to default
        return "/images/podcast-placeholder.png";
    }

    /// <summary>
    /// Extract episode-specific image URL
    /// </summary>
    private string? ExtractEpisodeImageUrl(SyndicationItem item)
    {
        // Try iTunes episode image
        var itunesImage = item.ElementExtensions
            .FirstOrDefault(e => e.OuterName == "image" &&
                                 e.OuterNamespace == "http://www.itunes.com/dtds/podcast-1.0.dtd");

        if (itunesImage != null)
        {
            try
            {
                var imageElement = itunesImage.GetObject<XmlElement>();
                var href = imageElement?.GetAttribute("href");
                if (!string.IsNullOrEmpty(href))
                    return href;
            }
            catch { }
        }

        return null;
    }

    /// <summary>
    /// Extract duration from iTunes extension or other sources
    /// </summary>
    private TimeSpan ExtractDuration(SyndicationItem item)
    {
        // Try iTunes duration
        var itunesDuration = item.ElementExtensions
            .FirstOrDefault(e => e.OuterName == "duration" &&
                                 e.OuterNamespace == "http://www.itunes.com/dtds/podcast-1.0.dtd");

        if (itunesDuration != null)
        {
            try
            {
                var durationStr = itunesDuration.GetObject<XmlElement>()?.InnerText?.Trim();
                if (!string.IsNullOrEmpty(durationStr))
                {
                    // Format can be: seconds (e.g., "1234"), HH:MM:SS (e.g., "1:30:45"), or MM:SS (e.g., "16:59")
                    
                    // Try parsing as plain seconds first
                    if (int.TryParse(durationStr, out var seconds))
                        return TimeSpan.FromSeconds(seconds);

                    // Try parsing as time format (HH:MM:SS or MM:SS or H:MM:SS)
                    var parts = durationStr.Split(':');
                    if (parts.Length == 2)
                    {
                        // MM:SS format
                        if (int.TryParse(parts[0], out var minutes) && 
                            int.TryParse(parts[1], out var secs))
                        {
                            return new TimeSpan(0, minutes, secs);
                        }
                    }
                    else if (parts.Length == 3)
                    {
                        // HH:MM:SS format
                        if (int.TryParse(parts[0], out var hours) && 
                            int.TryParse(parts[1], out var mins) && 
                            int.TryParse(parts[2], out var secs))
                        {
                            return new TimeSpan(hours, mins, secs);
                        }
                    }
                }
            }
            catch { }
        }

        return TimeSpan.Zero; // Unknown duration
    }

    /// <summary>
    /// Guess content type from file extension
    /// </summary>
    private string GuessContentType(string url)
    {
        var extension = Path.GetExtension(url).ToLowerInvariant();
        return extension switch
        {
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".ogg" => "audio/ogg",
            ".wav" => "audio/wav",
            _ => "audio/mpeg" // Default to MP3
        };
    }

    /// <summary>
    /// Strip HTML tags from description
    /// </summary>
    private string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // Simple HTML stripping (you could use HtmlAgilityPack for better parsing)
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(text).Trim();
    }
}
