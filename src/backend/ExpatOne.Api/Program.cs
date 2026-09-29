using System.Net;
using System.Threading.RateLimiting;
using Amazon.S3;
using ExpatOne.Api.Auth;
using ExpatOne.Api.Extensions;
using ExpatOne.Api.Services;
using ExpatOne.Api.Middleware;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Forwarded headers ────────────────────────────────────────────────────────
// Production: TLS terminates at the AWS ALB, which forwards over HTTP to Kestrel
// on a private VPC subnet. ForwardedHeadersMiddleware reads X-Forwarded-Proto and
// X-Forwarded-For so that UseHttpsRedirection and logging see the original
// client scheme and IP.
//
// Trust model:
//   Development  — ForwardedHeaders have no effect (HSTS/HTTPS-redirection are
//                  disabled in Development), so we leave KnownNetworks/Proxies
//                  empty (the ASP.NET Core default), which means forwarded headers
//                  are only accepted from localhost/loopback. This is fine for dev.
//
//   Production   — AWS ALB always originates from an RFC 1918 private IP inside
//                  the VPC. We therefore trust the three standard private ranges
//                  by default. If your VPC uses a specific CIDR and you want to
//                  restrict further, set ForwardedHeaders:TrustedCidrs in
//                  appsettings.Production.json or as an environment variable.
//                  Each entry must be a valid CIDR string, e.g. "10.0.0.0/16".
//                  If TrustedCidrs is present it replaces the RFC 1918 defaults.
//
// ForwardLimit=1 means only the immediately-adjacent proxy hop is trusted.
// A client cannot prepend extra X-Forwarded-For values to spoof an IP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = builder.Configuration.GetValue("ForwardedHeaders:ForwardLimit", 1);

    if (builder.Environment.IsDevelopment())
    {
        // Development: forwarded headers only accepted from loopback (ASP.NET Core default).
        // HSTS and HTTPS-redirection are disabled in Development so these headers carry
        // no security consequence — but we leave the default in place as good practice.
        // Nothing to clear or add here; the framework default (loopback-only) is correct.
    }
    else
    {
        // Production: the ALB lives on a private VPC address.  Accept forwarded headers
        // from any RFC 1918 range unless the operator has specified exact CIDRs.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();

        var trustedCidrs = builder.Configuration
            .GetSection("ForwardedHeaders:TrustedCidrs")
            .Get<string[]>();

        if (trustedCidrs is { Length: > 0 })
        {
            // Operator has specified explicit CIDRs — use those only.
            foreach (var cidr in trustedCidrs)
            {
                var parts = cidr.Split('/');
                if (parts.Length == 2
                    && IPAddress.TryParse(parts[0], out var address)
                    && int.TryParse(parts[1], out var prefixLength))
                {
                    options.KnownNetworks.Add(
                        new Microsoft.AspNetCore.HttpOverrides.IPNetwork(address, prefixLength));
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Invalid CIDR in ForwardedHeaders:TrustedCidrs: '{cidr}'");
                }
            }
        }
        else
        {
            // Default: trust all three RFC 1918 private ranges.
            // AWS ALBs always use private IPs; any public IP reaching Kestrel directly
            // has bypassed the ALB and its forwarded headers will be ignored because
            // public IPs do not match these networks.
            options.KnownNetworks.Add(
                new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
            options.KnownNetworks.Add(
                new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
            options.KnownNetworks.Add(
                new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
        }
    }
});

// ── HSTS ─────────────────────────────────────────────────────────────────────
// Enabled only for non-Development environments to avoid breaking local HTTP dev.
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromDays(365);
        options.IncludeSubDomains = true;
    });
}

// ── Rate limiting ─────────────────────────────────────────────────────────────
// Per-authenticated-user sliding-window limits on AI-backed endpoints.
// Partitioned by firebase_uid claim (server-side, not client-supplied).
// Falls back to IP-based partitioning for unauthenticated requests.
// Policy names are applied via [EnableRateLimiting] on individual actions.
builder.Services.AddRateLimiter(rl =>
{
    rl.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // General AI endpoints: translate, document-analyze, document-ask, assistant-messages
    // Limit read from config; default 20 requests per 60-second sliding window per user.
    rl.AddPolicy<string, AiRateLimitPartitioner>("ai-per-user");

    // Emergency AI endpoint: separate, more generous policy (10/60s per user).
    // CALL 999 is deterministic Flutter-native and is never behind this policy.
    rl.AddPolicy<string, EmergencyAiRateLimitPartitioner>("emergency-ai-per-user");

    rl.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        // SlidingWindowRateLimiter does not populate RetryAfter metadata automatically.
        // Use the window configured for the active policy, or default to 60 seconds.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }
        else
        {
            // Fallback: advise client to retry after the default window (60 s).
            context.HttpContext.Response.Headers.RetryAfter = "60";
        }
        await context.HttpContext.Response.WriteAsync(
            """{"message":"Too many requests. Please wait before trying again."}""", token);
    };
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ExpatOneDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.UseVector()));

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProfileService, ProfileService>();

var awsRegion = builder.Configuration["Aws:Region"];
if (!string.IsNullOrEmpty(awsRegion))
{
    builder.Services.AddSingleton<IAmazonS3>(sp =>
    {
        var config = new AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsRegion) };
        return new AmazonS3Client(config);
    });
    builder.Services.AddScoped<S3StorageService>();
    builder.Services.AddScoped<IStorageService>(sp => sp.GetRequiredService<S3StorageService>());
    builder.Services.AddScoped<IDocumentAuditService, DocumentAuditService>();
    builder.Services.AddScoped<IDocumentService, DocumentService>();
    builder.Services.AddScoped<IDocumentVersionService, DocumentVersionService>();
    builder.Services.AddScoped<IDocumentShareService, DocumentShareService>();
}

var geminiApiKey = builder.Configuration["Gemini:ApiKey"];
if (!string.IsNullOrEmpty(geminiApiKey))
{
    builder.Services.AddHttpClient<IAIService, GeminiAIService>();
    builder.Services.AddScoped<IDocumentAnalysisService, DocumentAnalysisService>();
    builder.Services.AddScoped<IKnowledgeIngestionService, KnowledgeIngestionService>();
    builder.Services.AddScoped<IKnowledgeSearchService, KnowledgeSearchService>();
    builder.Services.AddScoped<IAssistantService, AssistantService>();
    builder.Services.AddScoped<ITranslationService, TranslationService>();
    builder.Services.AddScoped<IEmergencyAssistService, EmergencyAssistService>();
}

var firebaseCredentialPath = builder.Configuration["Firebase:CredentialPath"];
if (FirebaseApp.DefaultInstance is null)
{
    if (!string.IsNullOrEmpty(firebaseCredentialPath) && File.Exists(firebaseCredentialPath))
    {
        FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromFile(firebaseCredentialPath),
            ProjectId = builder.Configuration["Firebase:ProjectId"]
        });
    }
    else if (!string.IsNullOrEmpty(builder.Configuration["Firebase:ProjectId"]))
    {
        FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.GetApplicationDefault(),
            ProjectId = builder.Configuration["Firebase:ProjectId"]
        });
    }
}

builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddHostedService<ReminderProcessorService>();

builder.Services.AddAuthentication(FirebaseAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, FirebaseAuthenticationHandler>(
        FirebaseAuthenticationHandler.SchemeName, null);

// ExpatOne's primary client is a Flutter mobile app. Flutter does not run in a browser
// and is therefore not subject to the browser Same-Origin Policy. No production CORS
// policy is required. The permissive Development policy exists solely for local tooling
// (e.g. Swagger UI, curl from a browser-based terminal). It must never be applied in
// production — it is gated to IsDevelopment() below.
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// ── Forwarded headers — must be first so downstream middleware sees correct scheme/IP ──
app.UseForwardedHeaders();

// ── HTTPS enforcement ─────────────────────────────────────────────────────────
// In production the reverse proxy terminates TLS and forwards requests over HTTP.
// UseHttpsRedirection checks the (possibly forwarded) request scheme — any request
// arriving as HTTP is redirected to HTTPS. In Development, requests come in on plain
// HTTP so redirection is skipped to avoid breaking 10.0.2.2:5000 dev workflows.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// DEPLOYMENT REQUIREMENT: set ASPNETCORE_ENVIRONMENT=Production in all production environments.
// Swagger UI and the permissive CORS policy are intentionally restricted to Development only.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("Development");
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

if (args.Contains("--seed-knowledge"))
{
    using var scope = app.Services.CreateScope();
    var ingestion = scope.ServiceProvider.GetService<IKnowledgeIngestionService>();
    if (ingestion is null)
    {
        Console.Error.WriteLine("ERROR: Gemini API key not configured. Cannot seed knowledge.");
        return;
    }
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<KnowledgeSeedService>>();
    var seedService = new KnowledgeSeedService(ingestion, logger);

    Console.WriteLine("Seeding Immigration Batch 1 knowledge...");
    var (sources, chunks, dupes) = await seedService.SeedImmigrationBatch1Async();
    Console.WriteLine($"Immigration — Sources created: {sources}, Chunks created: {chunks}, Duplicates skipped: {dupes}");

    Console.WriteLine("Seeding Tax Batch 2 knowledge...");
    var (taxSources, taxChunks, taxDupes) = await seedService.SeedTaxBatch2Async();
    Console.WriteLine($"Tax — Sources created: {taxSources}, Chunks created: {taxChunks}, Duplicates skipped: {taxDupes}");

    Console.WriteLine("Seeding Driving Batch 3 knowledge...");
    var (drvSources, drvChunks, drvDupes) = await seedService.SeedDrivingBatch3Async();
    Console.WriteLine($"Driving — Sources created: {drvSources}, Chunks created: {drvChunks}, Duplicates skipped: {drvDupes}");

    Console.WriteLine("Seeding Employment Batch 4 knowledge...");
    var (empSources, empChunks, empDupes) = await seedService.SeedEmploymentBatch4Async();
    Console.WriteLine($"Employment — Sources created: {empSources}, Chunks created: {empChunks}, Duplicates skipped: {empDupes}");

    Console.WriteLine("Seeding PERKESO Batch 5 knowledge...");
    var (perkSources, perkChunks, perkDupes) = await seedService.SeedPerkesoBatch5Async();
    Console.WriteLine($"PERKESO — Sources created: {perkSources}, Chunks created: {perkChunks}, Duplicates skipped: {perkDupes}");

    Console.WriteLine("Seeding Healthcare Batch 6 knowledge...");
    var (hcSources, hcChunks, hcDupes) = await seedService.SeedHealthcareBatch6Async();
    Console.WriteLine($"Healthcare — Sources created: {hcSources}, Chunks created: {hcChunks}, Duplicates skipped: {hcDupes}");

    Console.WriteLine("Seeding Education Batch 7 knowledge...");
    var (eduSources, eduChunks, eduDupes) = await seedService.SeedEducationBatch7Async();
    Console.WriteLine($"Education — Sources created: {eduSources}, Chunks created: {eduChunks}, Duplicates skipped: {eduDupes}");

    Console.WriteLine("Seeding Customs Batch 8 knowledge...");
    var (custSources, custChunks, custDupes) = await seedService.SeedCustomsBatch8Async();
    Console.WriteLine($"Customs — Sources created: {custSources}, Chunks created: {custChunks}, Duplicates skipped: {custDupes}");

    Console.WriteLine("Seeding Government Services Batch 9 knowledge...");
    var (govSources, govChunks, govDupes) = await seedService.SeedGovernmentServicesBatch9Async();
    Console.WriteLine($"Government Services — Sources created: {govSources}, Chunks created: {govChunks}, Duplicates skipped: {govDupes}");

    Console.WriteLine("Generating embeddings...");
    var totalEmbeddings = 0;
    int pending;
    do
    {
        var embResult = await ingestion.GenerateEmbeddingsAsync(50);
        totalEmbeddings += embResult.EmbeddingsGenerated;
        pending = embResult.TotalPending;
        if (embResult.EmbeddingsGenerated > 0)
            Console.WriteLine($"  Generated {embResult.EmbeddingsGenerated} embeddings, {pending} pending...");
    } while (pending > 0);
    Console.WriteLine($"Total embeddings generated: {totalEmbeddings}");
    Console.WriteLine("Knowledge seed complete.");
    return;
}

app.Run();

public partial class Program { }
