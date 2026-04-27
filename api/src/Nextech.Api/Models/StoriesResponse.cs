namespace Nextech.Api.Models;

public sealed record StoriesResponse(
    IReadOnlyList<Story> Items,
    int Total,
    int Page,
    int PageSize);
