using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace ServerDotaMania.Controllers;

[Route("api")]
[ApiController]
public class HelloController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        // http://localhost:51754/api для локальної перевірки
        // https://dotamania.bsite.net/api для глобальної перевірки
        return Ok(new { message = "Server work OK!" });
    }

    [HttpPost]
    public IActionResult Post([FromBody] MessageRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.message))
        {
            return BadRequest(new { message = "Message is required!" });
        }

        // Формування відповіді залежно від отриманого повідомлення
        string responseMessage;
        
        if (request.message == "1")
            responseMessage = "111!";
        else if (request.message == "2")
            responseMessage = "222!";
        else if (request.message == "3")
            responseMessage = "333";
        else
            responseMessage = "Error!";

        return Ok(new { message = responseMessage });
    }
    
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        // Формуємо шлях до папки uploads у wwwroot
        var uploadsFolder = Path.Combine("wwwroot", "uploads");
        // Перевірка чи існує папка, якщо ні – створюємо її
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }
        
        var filePath = Path.Combine(uploadsFolder, file.FileName);
    
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
    
        return Ok(new { message = "File uploaded successfully" });
    }
}

public class MessageRequest
{
    public string message { get; set; }
}