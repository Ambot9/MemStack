using MemStack.Data;
using MemStack.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Controllers + OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// EF Core with SQLite (guide Step 5)
builder.Services.AddDbContext<MemStackDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=memstack.db"));

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
