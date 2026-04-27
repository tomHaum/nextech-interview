using Nextech.Api.HackerNews;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicyName = "AllowedFrontends";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

builder.Services.Configure<HackerNewsOptions>(
    builder.Configuration.GetSection(HackerNewsOptions.SectionName));

builder.Services.AddHttpClient<IHackerNewsClient, HackerNewsClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HackerNewsOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddSingleton<IStoryCache, StoryCache>();
builder.Services.AddHostedService<StoryRefreshService>();

builder.Services.AddControllers();
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

app.UseCors(CorsPolicyName);
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
