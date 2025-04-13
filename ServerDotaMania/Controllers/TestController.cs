using Microsoft.AspNetCore.Mvc;

namespace ServerDotaMania.Controllers
{
    [ApiController]
    [Route("api")]
    public class TestController : ControllerBase
    {
        private readonly ILogger<TestController> _logger;
        
        public TestController(ILogger<TestController> logger)
        {
            _logger = logger;
        }
        
        [HttpGet("testCloudinary")]
        public IActionResult TestCloudinary()
        {
            _logger.LogInformation("TestCloudinary endpoint hit.");
            return Ok("Cloudinary is working properly.");
        }
    }
}