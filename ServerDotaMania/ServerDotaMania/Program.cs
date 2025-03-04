var builder = WebApplication.CreateBuilder(args);

// Додаємо CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// Додаємо контролери
builder.Services.AddControllers();

var app = builder.Build();

// Включаємо CORS
app.UseCors("AllowAll");

// Додаємо авторизацію (якщо потрібна)
app.UseAuthorization();

// Додаємо маршрутизацію для API
app.MapControllers();

app.Run();