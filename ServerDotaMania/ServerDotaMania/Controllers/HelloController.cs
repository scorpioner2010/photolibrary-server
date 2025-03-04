using Microsoft.AspNetCore.Mvc;

[Route("api")]
[ApiController]
public class HelloController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        //http://localhost:51754/api //for check server
        return Ok(new { message = "Server work OK!" });
    }

    [HttpPost]
    public IActionResult Post([FromBody] MessageRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.message))
        {
            return BadRequest(new { message = "Message is required!" });
        }

        // Формуємо відповідь залежно від отриманого меседжа
        string responseMessage;
        
        if (request.message == "1")
        {
            responseMessage = "111!";
        }
        else if (request.message == "2")
        {
            responseMessage = "222!";
        }
        else if (request.message == "3")
        {
            responseMessage = "333";
        }
        else
        {
            responseMessage = "Error!";
        }

        return Ok(new { message = responseMessage });
    }
}

public class MessageRequest
{
    public string message { get; set; }
}