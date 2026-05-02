using AgenticPipeline.Core.Interfaces;
using AgenticPipeline.Core.Models;
using AgenticPipeline.Infrastructure.Data;
using AgenticPipeline.Infrastructure.Plugins;
using AgenticPipeline.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Octokit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Handle circular references in Entity Framework navigation properties
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Claude/Bedrock settings from appsettings.json
builder.Services.Configure<ClaudeSettings>(
    builder.Configuration.GetSection(ClaudeSettings.SectionName));

// Configure GitHub settings from appsettings.json
builder.Services.Configure<GitHubSettings>(
    builder.Configuration.GetSection("GitHub"));

// Configure Azure DevOps settings from appsettings.json
builder.Services.Configure<AzureDevOpsSettings>(
    builder.Configuration.GetSection(AzureDevOpsSettings.SectionName));

// Configure Embedding settings from appsettings.json
builder.Services.Configure<EmbeddingSettings>(
    builder.Configuration.GetSection(EmbeddingSettings.SectionName));

// Configure Notification settings from appsettings.json
builder.Services.Configure<NotificationSettings>(
    builder.Configuration.GetSection(NotificationSettings.SectionName));

// Configure AutoFix settings from appsettings.json
builder.Services.Configure<AutoFixSettings>(
    builder.Configuration.GetSection("AutoFix"));

// Configure Database (SQLite with EF Core)
builder.Services.AddDbContext<AgenticPipelineDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register repositories
builder.Services.AddScoped<IFailureRepository, AgenticPipeline.Infrastructure.Repositories.FailureRepository>();

// Register services
builder.Services.AddSingleton<IClaudeService, ClaudeService>();
builder.Services.AddSingleton<ICommitAnalyzer, GitHubService>();
builder.Services.AddSingleton<IEmbeddingService, TitanEmbeddingService>();
builder.Services.AddSingleton<ISimilarityService, CosineSimilarityService>();
builder.Services.AddSingleton<IPatternRecognitionService, KMeansPatternRecognitionService>();
builder.Services.AddSingleton<RcaPlugin>();
builder.Services.AddSingleton<CommitAnalyzerPlugin>();

// Register orchestration services
builder.Services.AddScoped<IPipelineOrchestrator, PipelineOrchestrator>();
builder.Services.AddSingleton<WebhookVerificationService>();

// Register GitHub client with authentication
builder.Services.AddSingleton<GitHubClient>(sp =>
{
    var gitHubSettings = sp.GetRequiredService<IOptions<GitHubSettings>>().Value;
    var client = new GitHubClient(new ProductHeaderValue("AgenticPipeline"));

    // Configure authentication if token is provided
    if (!string.IsNullOrEmpty(gitHubSettings.PersonalAccessToken))
    {
        client.Credentials = new Credentials(gitHubSettings.PersonalAccessToken);
    }

    return client;
});

// Register GitHub Actions log service
builder.Services.AddSingleton<GitHubActionsLogService>();

// Register Azure DevOps log service with HttpClient
builder.Services.AddHttpClient<AzureDevOpsLogService>();

// Register notification services
builder.Services.AddSingleton<GitHubCommentService>();
builder.Services.AddHttpClient<TeamsNotificationService>();

// Register auto-fix services
builder.Services.AddSingleton<ErrorExtractionService>();
builder.Services.AddSingleton<FixValidationService>();
builder.Services.AddScoped<IAutoFixService, AutoFixService>();

// Register iterative retry services
builder.Services.AddSingleton<AutoFixSessionManager>();
builder.Services.AddScoped<IterativeAutoFixOrchestrator>();

// Add CORS (if needed for frontend)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Initialize Database
InitializeDatabase(app);

// Configure the HTTP request pipeline

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Agentic Pipeline API v1");
        options.RoutePrefix = string.Empty; // Swagger UI at root
    });
}

app.UseHttpsRedirection();
app.UseCors();

// Enable request buffering for webhook endpoints to allow reading body multiple times
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/webhook"))
    {
        context.Request.EnableBuffering();
    }
    await next();
});

app.UseAuthorization();
app.MapControllers();

app.Run();

// Local function for database initialization
static void InitializeDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<AgenticPipelineDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        // Ensure database is created (for development)
        context.Database.EnsureCreated();

        logger.LogInformation("Database initialized successfully. Location: {DbPath}",
            Path.Combine(Directory.GetCurrentDirectory(), "pipeline.db"));
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database");
        throw;
    }
}
