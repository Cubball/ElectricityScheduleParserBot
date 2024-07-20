using Microsoft.EntityFrameworkCore;
using ElectricitySchedule.Bot.Persistence;
using ElectricitySchedule.Bot.Services;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddNewtonsoftJson();

var connectionString = builder.Configuration["MongoDbSettings:ConnectionString"]
    ?? throw new InvalidOperationException("MongoDbSettings:ConnectionString should be provided");
var databaseName = builder.Configuration["MongoDbSettings:DatabaseName"]
    ?? throw new InvalidOperationException("MongoDbSettings:DatabaseName should be provided");
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseMongoDB(connectionString, databaseName));

var telegramToken = builder.Configuration["TelegramToken"]
    ?? throw new InvalidOperationException("TelegramToken should be provided");
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(telegramToken));

builder.Services.AddScoped<IUpdateService, UpdateService>();
builder.Services.AddScoped<ITelegramService, TelegramService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();

app.MapControllers();

app.Run();