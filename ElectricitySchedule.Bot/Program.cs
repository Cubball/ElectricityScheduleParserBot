using Microsoft.EntityFrameworkCore;
using ElectricitySchedule.Bot.Persistence;
using ElectricitySchedule.Bot.Services;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddNewtonsoftJson();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection should be provided");
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connectionString));

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