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

/*builder.Services.AddControllers().AddJsonOptions(options =>
{
    // Зберігаємо регістр властивостей, як у C# (Name, Description, ImageBase64)
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
});*/

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