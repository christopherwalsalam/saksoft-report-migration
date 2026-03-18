using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ReportConversion.Application;
using ReportConversion.Application.Settings;
using ReportConversion.Infrastructure;
using Serilog;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ───────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, lc) =>
    lc.ReadFrom.Configuration(ctx.Configuration));

// ─── Configuration / IOptions ──────────────────────────────────────────────
builder.Services.Configure<SapBoSettings>(builder.Configuration.GetSection("SapBo"));
builder.Services.Configure<AzureOpenAiSettings>(builder.Configuration.GetSection("AzureOpenAi"));
builder.Services.Configure<AzureBlobSettings>(builder.Configuration.GetSection("AzureBlob"));
builder.Services.Configure<PowerBiSettings>(builder.Configuration.GetSection("PowerBi"));
builder.Services.Configure<StaleReportSettings>(builder.Configuration.GetSection("StaleReport"));
builder.Services.Configure<DuplicateDetectionSettings>(builder.Configuration.GetSection("DuplicateDetection"));
builder.Services.Configure<MigrationSettings>(builder.Configuration.GetSection("Migration"));

// ─── Application & Infrastructure ──────────────────────────────────────────
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices();

// ─── JWT Auth ───────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// ─── Hangfire ───────────────────────────────────────────────────────────────
var hangfireConn = builder.Configuration["Hangfire:SqlServerConnectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddHangfire(config =>
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UseSqlServerStorage(hangfireConn, new SqlServerStorageOptions
          {
              CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
              SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
              QueuePollInterval = TimeSpan.Zero,
              UseRecommendedIsolationLevel = true,
              DisableGlobalLocks = true
          }));
builder.Services.AddHangfireServer();

// ─── Controllers & Swagger ─────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ─── CORS ───────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactUI", policy =>
        policy.WithOrigins(
                "http://localhost:3000",
                "https://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ReportConversion API",
        Version = "v1",
        Description = "SAP BusinessObjects to Power BI Migration API — automates end-to-end migration of up to 10,000 reports with AI-assisted analysis."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter JWT token: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// ─── Middleware Pipeline ────────────────────────────────────────────────────
app.UseMiddleware<ReportConversion.API.Middleware.GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ReportConversion API v1"));
}

app.UseHttpsRedirection();
app.UseCors("ReactUI");
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire");
app.MapControllers();

app.Run();
