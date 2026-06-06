using System.Text.Json;
using System.Text.Json.Serialization;
using SmartShoppingChatBot.API.Extensions;
using SmartShoppingChatBot.API.Middlewares;
using SmartShoppingChatBot.Application;
using SmartShoppingChatBot.Application.Commons.Options;
using SmartShoppingChatBot.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

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
});

builder.Services.AddMongoDbConfig(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices();

// Email settings configuration
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();
