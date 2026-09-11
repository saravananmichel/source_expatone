using Amazon.S3;
using ExpatOne.Api.Auth;
using ExpatOne.Api.Middleware;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ExpatOneDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUserService, UserService>();

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
    builder.Services.AddScoped<IDocumentService, DocumentService>();
}

var firebaseCredentialPath = builder.Configuration["Firebase:CredentialPath"];
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

builder.Services.AddAuthentication(FirebaseAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, FirebaseAuthenticationHandler>(
        FirebaseAuthenticationHandler.SchemeName, null);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("Development");
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
