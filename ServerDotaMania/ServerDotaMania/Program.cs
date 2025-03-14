using CloudinaryDotNet;
using ServerDotaMania.Configuration; // Простір імен, де лежить ваш CloudinarySettings

Console.WriteLine("=== Starting Program ===");

var builder = WebApplication.CreateBuilder(args);

// =================== НАЛАШТУВАННЯ ЛОГУВАННЯ ===================
Console.WriteLine("=== Configuring Logging ===");
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new MyInMemoryLoggerProvider());
builder.Logging.AddConsole();

// =================== НАЛАШТУВАННЯ CORS ===================
Console.WriteLine("=== Configuring CORS ===");
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// =================== ДОДАЄМО HttpClient ===================
Console.WriteLine("=== Adding HttpClient ===");
builder.Services.AddHttpClient();

// =================== НАЛАШТУВАННЯ CLOUDINARY ===================
Console.WriteLine("=== Configuring Cloudinary ===");
// 1. Зчитуємо секцію "CloudinarySettings" із файлу appsettings.json
builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("CloudinarySettings"));

Console.WriteLine("Reading 'CloudinarySettings' from configuration...");
var cloudinarySettings = builder.Configuration
    .GetSection("CloudinarySettings")
    .Get<CloudinarySettings>();

if (cloudinarySettings == null)
{
    Console.WriteLine("CloudinarySettings is NULL! Check your appsettings.json or environment variables.");
}
else
{
    // Тут відкрито виводимо всі поля, оскільки це тестові дані
    Console.WriteLine($"CloudName = {cloudinarySettings.CloudName}");
    Console.WriteLine($"ApiKey    = {cloudinarySettings.ApiKey}");
    Console.WriteLine($"ApiSecret = {cloudinarySettings.ApiSecret}");
}

// 2. Створюємо обліковий запис (Account) для Cloudinary
Console.WriteLine("Creating Cloudinary Account...");
Account account = new Account(
    cloudinarySettings?.CloudName,
    cloudinarySettings?.ApiKey,
    cloudinarySettings?.ApiSecret
);

// 3. Створюємо екземпляр Cloudinary
Console.WriteLine("Creating Cloudinary instance...");
Cloudinary cloudinary = new Cloudinary(account);

// 4. Реєструємо Cloudinary як Singleton у контейнері залежностей
Console.WriteLine("Registering Cloudinary as singleton...");
builder.Services.AddSingleton(cloudinary);

// ================================================================

// Додаємо контролери
Console.WriteLine("=== Adding Controllers ===");
builder.Services.AddControllers();

// Створюємо додаток
Console.WriteLine("=== Building WebApplication ===");
var app = builder.Build();

Console.WriteLine("=== Configuring Middleware ===");
// Включаємо CORS
app.UseCors("AllowAll");

// Додаємо авторизацію (якщо потрібна)
app.UseAuthorization();

// Додаємо маршрутизацію для API
Console.WriteLine("Mapping controllers...");
app.MapControllers();

// Додаємо endpoint для отримання логів
Console.WriteLine("Mapping /logs endpoint...");
app.MapGet("/logs", () =>
{
    return MyInMemoryLogger.GetLogs();
});

// Запускаємо додаток
Console.WriteLine("=== Running the app ===");
app.Run();