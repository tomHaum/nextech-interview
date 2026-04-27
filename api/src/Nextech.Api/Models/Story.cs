namespace Nextech.Api.Models;

public sealed record Story(
    int Id,
    string Title,
    string? Url,
    string By,
    long Time,
    int Score);
