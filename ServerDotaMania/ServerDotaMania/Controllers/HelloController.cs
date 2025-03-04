using Microsoft.AspNetCore.Mvc;

[Route("api")]
[ApiController]
public class HelloController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { message = "11111122222!" });
    }

    [HttpPost]
    public IActionResult Post([FromBody] MessageRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.message))
        {
            return BadRequest(new { message = "Message is required!" });
        }

        // Формуємо відповідь залежно від отриманого меседжа
        string responseMessage = request.message switch
        {
            "1" => "111!",
            "2" => "222!",
            "3" => "333",
            _ => "I don't understand this message."
        };

        return Ok(new { message = responseMessage });
    }
}

public class MessageRequest
{
    public string message { get; set; }
}