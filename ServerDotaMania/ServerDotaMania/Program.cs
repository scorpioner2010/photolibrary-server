using CloudinaryDotNet;
using Microsoft.Extensions.Logging;
using ServerDotaMania.Settings;
using ServerDotaMania.Logging; // Підключаємо простір імен із MyInMemoryLogger

var builder = WebApplication.CreateBuilder(args);

// --- Налаштування логування ---
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new MyInMemoryLoggerProvider());
builder.Logging.AddConsole();

// --- Налаштування CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// --- Налаштування Cloudinary ---
builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("CloudinarySettings"));

// --- Додаємо HttpClient та контролери ---
builder.Services.AddHttpClient();
builder.Services.AddControllers();

// Побудова додатку
var app = builder.Build();

// Використовуємо CORS та підключаємо контролери
app.UseCors("AllowAll");
app.MapControllers();

// Endpoint для отримання логів
app.MapGet("/logs", () => MyInMemoryLogger.GetLogs());

app.Run();