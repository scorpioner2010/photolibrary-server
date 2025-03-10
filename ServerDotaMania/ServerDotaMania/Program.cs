using CloudinaryDotNet;
using ServerDotaMania.Configuration; // Простір імен, де лежить ваш CloudinarySettings
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Налаштування логування: очищення стандартних провайдерів і додавання нашого логера та консолі
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new MyInMemoryLoggerProvider());
builder.Logging.AddConsole();

// Додаємо CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// =================== НАЛАШТУВАННЯ CLOUDINARY ===================

// 1. Зчитуємо секцію "CloudinarySettings" із файлу appsettings.json
builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("CloudinarySettings"));

// 2. Створюємо об'єкт CloudinarySettings
var cloudinarySettings = builder.Configuration
    .GetSection("CloudinarySettings")
    .Get<CloudinarySettings>();

// 3. Створюємо обліковий запис (Account) для Cloudinary
Account account = new Account(
    cloudinarySettings.CloudName,
    cloudinarySettings.ApiKey,
    cloudinarySettings.ApiSecret
);

// 4. Створюємо екземпляр Cloudinary
Cloudinary cloudinary = new Cloudinary(account);

// 5. Реєструємо Cloudinary як Singleton у контейнері залежностей
builder.Services.AddSingleton(cloudinary);

// ================================================================

// Додаємо контролери
builder.Services.AddControllers();

var app = builder.Build();

// Включаємо CORS
app.UseCors("AllowAll");

// Додаємо авторизацію (якщо потрібна)
app.UseAuthorization();

// Додаємо маршрутизацію для API
app.MapControllers();

// Додаємо endpoint для отримання логів
app.MapGet("/logs", () =>
{
    return MyInMemoryLogger.GetLogs();
});

app.Run();