using System.Globalization;
using System.Security.Claims;
using CloudNativeImageProcessing.Application.Images;
using CloudNativeImageProcessing.Infrastructure;
using CloudNativeImageProcessing.Infrastructure.Options;
using CloudNativeImageProcessing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.EnableAdaptiveSampling = false;
    });
}

var bearerTokenExpirationHours = builder.Configuration.GetValue<long?>("Identity:BearerTokenExpirationHours") ?? 8;
var uploadMaxRequestBytes = builder.Configuration.GetValue<long>("Upload:MaxRequestBodyBytes", 10L * 1024 * 1024);
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = uploadMaxRequestBytes;
});

builder.Services.AddEndpointsApiExplorer();
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 } origins
    ? origins
    : new[] { "http://localhost:5173", "http://127.0.0.1:5173" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ImageService>();

builder.Services.AddIdentityApiEndpoints<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ImageDbContext>();

builder.Services.Configure<BearerTokenOptions>(IdentityConstants.BearerScheme, options =>
{
    options.BearerTokenExpiration = TimeSpan.FromHours(bearerTokenExpirationHours);
});

builder.Services.AddAuthorization();

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
});

var app = builder.Build();

var imageProcessingEh = app.Configuration[$"{EventHubOptions.SectionName}:ImageProcessingConnectionString"];
if (string.IsNullOrWhiteSpace(imageProcessingEh))
{
    app.Logger.LogWarning(
        "{Key} is not set. Image processing events will not be published (no-op publisher). For local dev, run with ASPNETCORE_ENVIRONMENT=Development so appsettings.Development.json is loaded, or set the variable.",
        $"{EventHubOptions.SectionName}:ImageProcessingConnectionString");
}

const int maxMigrationAttempts = 10;
for (var attempt = 1; attempt <= maxMigrationAttempts; attempt++)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ImageDbContext>();
        await dbContext.Database.MigrateAsync();
        app.Logger.LogInformation("Database migrations applied.");
        break;
    }
    catch when (attempt < maxMigrationAttempts)
    {
        await Task.Delay(TimeSpan.FromSeconds(3));
    }
}

// HTTP-only (e.g. Docker :8080) must not redirect browsers to HTTPS or fetch/CORS breaks.
var urls = builder.Configuration["ASPNETCORE_URLS"] ?? "";
if (urls.Contains("https:", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/health"),
    branch => branch.UseHttpLogging());

app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGroup("/api/auth").MapIdentityApi<IdentityUser>();

app.MapGet("/api/images", [Authorize] async (
    HttpContext http,
    ClaimsPrincipal principal,
    ImageService service,
    CancellationToken cancellationToken) =>
{
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.Unauthorized();
    }

    var userEmail = principal.FindFirstValue(ClaimTypes.Email);
    if (string.IsNullOrWhiteSpace(userEmail))
    {
        return Results.BadRequest(new { message = "User email claim is required to publish upload events." });
    }

    // Explicit query parsing — minimal API binding for `page` / `pageSize` can fail to bind from the query string in some setups.
    static int ReadQueryInt(IQueryCollection query, string key, int fallback)
    {
        var raw = query[key].FirstOrDefault();
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    var page = Math.Max(1, ReadQueryInt(http.Request.Query, "page", 1));
    var pageSize = ReadQueryInt(http.Request.Query, "pageSize", 10);

    var images = await service.ListAsync(userId, page, pageSize, cancellationToken);
    return Results.Ok(images);
});

app.MapGet("/api/images/{id:guid}", [Authorize] async (
    Guid id,
    ClaimsPrincipal principal,
    ImageService service,
    CancellationToken cancellationToken) =>
{
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.Unauthorized();
    }

    var image = await service.GetAsync(id, userId, cancellationToken);
    return image is null ? Results.NotFound() : Results.Ok(image);
});

app.MapGet("/api/images/{id:guid}/preview", [Authorize] async (
    Guid id,
    ClaimsPrincipal principal,
    ImageService service,
    CancellationToken cancellationToken) =>
{
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.Unauthorized();
    }

    var blob = await service.GetImageBlobAsync(id, userId, cancellationToken);
    if (blob is null)
    {
        return Results.NotFound();
    }

    return Results.File(blob.Value.Stream, blob.Value.ContentType, enableRangeProcessing: true);
});

app.MapPost("/api/images", [Authorize] async (
    IFormFile? file,
    [FromForm] string? name,
    [FromForm] string? description,
    [FromForm] string? useAiDescription,
    [FromForm] string? operation,
    ClaimsPrincipal principal,
    ImageService service,
    CancellationToken cancellationToken) =>
{
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "A file is required." });
    }

    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest(new { message = "Name is required." });
    }

    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.Unauthorized();
    }

    var userEmail = principal.FindFirstValue(ClaimTypes.Email);
    if (string.IsNullOrWhiteSpace(userEmail))
    {
        return Results.BadRequest(new { message = "User email claim is required to publish upload events." });
    }

    var useAi = string.Equals(useAiDescription, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(useAiDescription, "on", StringComparison.OrdinalIgnoreCase);
    var op = string.IsNullOrWhiteSpace(operation) ? "none" : operation.Trim();

    try
    {
        var stream = file.OpenReadStream();
        var command = new ImageUploadCommand(
            name.Trim(),
            description,
            useAi,
            op,
            stream,
            file.FileName,
            file.ContentType);

        var created = await service.CreateAsync(command, userId, userEmail, cancellationToken);
        return Results.Created($"/api/images/{created.Id}", created);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).DisableAntiforgery().AddEndpointFilter((context, next) =>
{
    var limitFeature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
    if (limitFeature is not null && !limitFeature.IsReadOnly)
    {
        limitFeature.MaxRequestBodySize = uploadMaxRequestBytes;
    }

    return next(context);
});

app.MapDelete("/api/images/{id:guid}", [Authorize] async (
    Guid id,
    ClaimsPrincipal principal,
    ImageService service,
    CancellationToken cancellationToken) =>
{
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.Unauthorized();
    }

    var deleted = await service.DeleteAsync(id, userId, cancellationToken);
    return deleted ? Results.NoContent() : Results.NotFound();
});

app.Run();
