namespace Nextech.Api.HackerNews;

public sealed class HackerNewsOptions
{
    public const string SectionName = "HackerNews";

    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0/";
    public int MaxStories { get; set; } = 500;
    public int RefreshIntervalSeconds { get; set; } = 60;
    public int ItemFetchConcurrency { get; set; } = 10;
}
