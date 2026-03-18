using MemStack.Data;
using MemStack.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var configuredConnection = builder.Configuration.GetConnectionString("Default");
var databasePath = Environment.GetEnvironmentVariable("DATABASE_PATH");
if (!string.IsNullOrWhiteSpace(databasePath))
{
    var directory = Path.GetDirectoryName(databasePath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    configuredConnection = $"Data Source={databasePath}";
}
else if (string.IsNullOrWhiteSpace(configuredConnection))
{
    configuredConnection = "Data Source=memstack.db";
}

// Controllers + OpenAPI
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// EF Core with SQLite (guide Step 5)
builder.Services.AddDbContext<MemStackDbContext>(options =>
    options.UseSqlite(configuredConnection));

// Repository: EF Core is primary, InMemory kept for reference/testing
builder.Services.AddScoped<IFeatureMemoryRepository, EfFeatureMemoryRepository>();

// Git persistence (guide Step 10-11)
builder.Services.AddSingleton<IGitRepository, GitRepository>();
builder.Services.AddScoped<IFeatureMemoryService, FeatureMemoryService>();

var app = builder.Build();

// Auto-apply migrations on startup so the DB is always up to date
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MemStackDbContext>();
    db.Database.Migrate();

    // Initialize git repo if enabled
    var git = scope.ServiceProvider.GetRequiredService<IGitRepository>();
    git.Initialize();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.MapGet("/", () => Results.Ok(new
{
    service = "MemStack",
    status = "ok",
    environment = app.Environment.EnvironmentName
}));
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));
app.MapControllers();
app.Run();
