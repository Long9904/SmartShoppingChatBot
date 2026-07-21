using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CloudinaryDotNet;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using SmartShoppingChatBot.API.Extensions;
using SmartShoppingChatBot.API.Middlewares;
using SmartShoppingChatBot.Application;
using SmartShoppingChatBot.Application.Commons.Behaviors;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Application.Plugins;
using SmartShoppingChatBot.Infrastructure;
using SmartShoppingChatBot.Infrastructure.Seeders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("gemini", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
    c.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddHttpClient("qwen", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
    c.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});

// Configure Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new VietnamDateTimeOffsetConverter());
});

builder.Services.AddMongoDbConfig(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/keys"))
    .SetApplicationName("SmartShoppingChatBot");

// Settings configuration
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<EmailTokenSettings>(builder.Configuration.GetSection("EmailTokenSettings"));
builder.Services.Configure<ApiConfigs>(builder.Configuration.GetSection("ApiConfigs"));
builder.Services.Configure<GoogleConfigs>(builder.Configuration.GetSection("Google"));
builder.Services.Configure<QwenConfigs>(builder.Configuration.GetSection("Qwen"));

builder.Services.AddEndpointsApiExplorer();
//Cloudinary
var cloudName = builder.Configuration["Cloudinary:CloudName"];
var apiKey = builder.Configuration["Cloudinary:ApiKey"];
var apiSecret = builder.Configuration["Cloudinary:ApiSecret"];

var account = new Account(cloudName, apiKey, apiSecret);
var cloudinary = new Cloudinary(account);
builder.Services.AddSingleton<ICloudinary>(sp => cloudinary);
// Swagger configuration with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityDefinition("ApiKey",
    new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        Name = "x-api-key",
        In = ParameterLocation.Header
    });

    c.OperationFilter<SwaggerSecurityOperationFilter>();

    c.SwaggerDoc("external", new OpenApiInfo
    {
        Title = "Smart Shopping ChatBot API",
        Version = "v1",
        Description = "API documentation for Smart Shopping ChatBot\n\n\n" +
                       "Definition and Acronyms: \n\n" +
                       "- BO: Business Owner\n\n" +
                       "- CT: Catalog Team",
    });

    c.SwaggerDoc("internal", new OpenApiInfo
    {
        Title = "Smart Shopping ChatBot Internal API",
        Version = "v1",
        Description = "Internal API documentation for Smart Shopping ChatBot\n\n\n" +
                       "Definition and Acronyms: \n\n" +
                       "- BO: Business Owner\n\n" +
                       "- CT: Catalog Team",
    });

});

// JWT configuration
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("JwtSettings"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwt = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey));


builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        // Cookies
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Cookies["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };

        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role
        };
    })

    .AddScheme<AuthenticationSchemeOptions,
    ApiKeyAuthenticationHandler>(
    "ApiKey",
    options => { });




builder.Services.AddAuthorization();

// MassTransit
builder.Services.AddMassTransit(x =>
{
    // Auto-discover and register all consumers in the MeetingService.Consumers assembly
    x.AddConsumers(typeof(ApplicationDI).Assembly);

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]!);
            h.Password(builder.Configuration["RabbitMQ:Password"]!);
        });

        cfg.UseMessageRetry(r => r.Exponential(
            retryLimit: 5,
            minInterval: TimeSpan.FromSeconds(1),
            maxInterval: TimeSpan.FromSeconds(30),
            intervalDelta: TimeSpan.FromSeconds(5)
        ));

        // Auto-configure endpoints for all discovered consumers
        cfg.ConfigureEndpoints(context);
    });
});


// Qdrant configuration
builder.Services.AddSingleton(_ =>
    new QdrantClient(
        host: builder.Configuration["Qdrant:Host"] ?? "localhost",
        port: int.Parse(builder.Configuration["Qdrant:Port"] ?? "6334")
    ));

// Semantic Kernel
builder.Services.AddScoped<ProductPlugin>();
builder.Services.AddScoped<DocumentPlugin>();
builder.Services.AddScoped<Kernel>(sp =>
{
    var kb = Kernel.CreateBuilder();

    kb.AddOpenAIChatCompletion(
        modelId: builder.Configuration["OpenAI:ModelId"]!,
        apiKey: builder.Configuration["OpenAI:ApiKey"]!
    );

    // Kernel plugin register
    kb.Plugins.AddFromObject(sp.GetRequiredService<ProductPlugin>(), "Product");
    kb.Plugins.AddFromObject(sp.GetRequiredService<DocumentPlugin>(), "DocumentPlugin");

    return kb.Build();
});

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").GetChildren()
    .Select(x => x.Value!)
    .Where(v => !string.IsNullOrEmpty(v))
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
          policy.WithOrigins(allowedOrigins)
          .AllowAnyMethod()
          .AllowAnyHeader()
          .AllowCredentials());
});

// Razor page
builder.Services.AddRazorPages();

var app = builder.Build();

// Set the environment variable for Google Application Credentials
Environment.SetEnvironmentVariable(
    "GOOGLE_APPLICATION_CREDENTIALS",
    builder.Configuration["Google:CredentialsPath"]);

using var scope = app.Services.CreateScope();

var userSeeder = scope.ServiceProvider.GetRequiredService<UserSeeder>();
await userSeeder.SeedUsersAsync();

var subscriptionSeeder = scope.ServiceProvider.GetRequiredService<SubscriptionSeeder>();
await subscriptionSeeder.SeedSubscriptionsAsync();

var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

await db.Database.EnsureCreatedAsync();

var qdrantInitializer = scope.ServiceProvider
    .GetRequiredService<QdrantCollectionInitializer>();

await qdrantInitializer.EnsureAsync();

app.UseSwagger(c =>
{
    c.RouteTemplate = "swagger/{documentName}/swagger.json";
});

app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "swagger/internal";

    c.SwaggerEndpoint("/swagger/internal/swagger.json", "Internal API");
});

app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "swagger/external";

    c.SwaggerEndpoint("/swagger/external/swagger.json", "External API");
});

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors("AllowSpecificOrigins");
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();
