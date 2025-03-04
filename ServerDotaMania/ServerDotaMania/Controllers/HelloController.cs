using Microsoft.AspNetCore.Mvc;

[Route("api/hello")]
[ApiController]
public class HelloController : ControllerBase
{
    [HttpGet]
    public string Get()
    {
        return "Hello from the server!";
    }
}