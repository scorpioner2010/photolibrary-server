using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ServerDotaMania.Controllers;

[Route("api")]
[ApiController]
public class HelloController : ControllerBase
{
    private readonly ILogger<HelloController> _logger;

    public HelloController(ILogger<HelloController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Get()
    {
        _logger.LogInformation("Received GET request on /api");
        return Ok(new { message = "Server work OK!" });
    }

    [HttpPost]
    public IActionResult Post([FromBody] MessageRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.message))
        {
            _logger.LogWarning("Received POST request with empty message");
            return BadRequest(new { message = "Message is required!" });
        }

        string responseMessage;
        
        if (request.message == "1")
            responseMessage = "111!";
        else if (request.message == "2")
            responseMessage = "222!";
        else if (request.message == "3")
            responseMessage = "333";
        else
            responseMessage = "Error!";

        _logger.LogInformation("Received POST request with message: {Message}, responding with: {Response}", request.message, responseMessage);
        return Ok(new { message = responseMessage });
    }
    
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("Received upload request with no file");
            return BadRequest("No file uploaded.");
        }

        var uploadsFolder = Path.Combine("wwwroot", "uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
            _logger.LogInformation("Created uploads folder at {UploadsFolder}", uploadsFolder);
        }
        
        var filePath = Path.Combine(uploadsFolder, file.FileName);
        _logger.LogInformation("Saving file {FileName} to {FilePath}", file.FileName, filePath);
    
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
    
        _logger.LogInformation("File {FileName} uploaded successfully", file.FileName);
        return Ok(new { message = "File uploaded successfully" });
    }
}

public class MessageRequest
{
    public string message { get; set; }
}
