using Microsoft.AspNetCore.Mvc;

namespace ServerDotaMania.Controllers
{
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
        
        [HttpPost("uploadContainer")]
        public async Task<IActionResult> UploadContainer(
            [FromForm] string containerName,
            [FromForm] string description,
            [FromForm] IFormFile file)
        {
            if (string.IsNullOrEmpty(containerName))
            {
                _logger.LogWarning("Container name is missing.");
                return BadRequest("Container name is required.");
            }
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("Received uploadContainer request with no file");
                return BadRequest("No file uploaded.");
            }

            var baseFolder = Path.Combine("wwwroot", "containers");
            var containerFolder = Path.Combine(baseFolder, containerName);
            _logger.LogInformation("Processing container: {ContainerName}", containerName);

            // Якщо контейнер вже існує – очищуємо його (видаляємо всі файли)
            if (Directory.Exists(containerFolder))
            {
                _logger.LogInformation("Container {ContainerName} already exists. Clearing existing files.", containerName);
                var existingFiles = Directory.GetFiles(containerFolder);
                foreach (var existingFile in existingFiles)
                {
                    _logger.LogInformation("Deleting existing file: {File}", existingFile);
                    System.IO.File.Delete(existingFile);
                }
            }
            else
            {
                Directory.CreateDirectory(containerFolder);
                _logger.LogInformation("Created container folder: {ContainerFolder}", containerFolder);
            }

            // Збереження файлу (наприклад, sprite.png)
            var imagePath = Path.Combine(containerFolder, file.FileName);
            _logger.LogInformation("Saving file {FileName} to container folder {ContainerFolder}", file.FileName, containerFolder);
            using (var stream = new FileStream(imagePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Збереження опису контейнера у текстовому файлі
            var descriptionPath = Path.Combine(containerFolder, "description.txt");
            _logger.LogInformation("Saving container description to {DescriptionPath}", descriptionPath);
            await System.IO.File.WriteAllTextAsync(descriptionPath, description);

            _logger.LogInformation("Container {ContainerName} uploaded successfully", containerName);
            return Ok(new { message = "Container uploaded successfully." });
        }
        
        [HttpDelete("deleteContainer/{containerName}")]
        public IActionResult DeleteContainer(string containerName)
        {
            if (string.IsNullOrEmpty(containerName))
            {
                _logger.LogWarning("Container name is missing for deletion.");
                return BadRequest("Container name is required.");
            }

            var baseFolder = Path.Combine("wwwroot", "containers");
            var containerFolder = Path.Combine(baseFolder, containerName);

            if (!Directory.Exists(containerFolder))
            {
                _logger.LogWarning("Container {ContainerName} not found for deletion.", containerName);
                return NotFound(new { message = "Container not found." });
            }

            try
            {
                Directory.Delete(containerFolder, true);
                _logger.LogInformation("Container {ContainerName} deleted successfully.", containerName);
                return Ok(new { message = "Container deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting container {ContainerName}.", containerName);
                return StatusCode(500, new { message = "Error deleting container." });
            }
        }

        // ====== Новий endpoint для отримання списку контейнерів ======
        [HttpGet("containers")]
        public IActionResult GetAllContainers()
        {
            var baseFolder = Path.Combine("wwwroot", "containers");
            if (!Directory.Exists(baseFolder))
            {
                Directory.CreateDirectory(baseFolder);
            }

            var containerDirs = Directory.GetDirectories(baseFolder);
            var result = new List<ContainerInfo>();
            
            foreach (var dir in containerDirs)
            {
                var name = Path.GetFileName(dir);
                
                var descriptionPath = Path.Combine(dir, "description.txt");
                var description = System.IO.File.Exists(descriptionPath)
                    ? System.IO.File.ReadAllText(descriptionPath)
                    : "";
                
                // Знаходимо перший файл з розширенням .png/.jpg/.jpeg
                var imagePath = Directory.GetFiles(dir).FirstOrDefault(f =>
                    f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

                string imageBase64 = "";
                if (imagePath != null)
                {
                    var imageBytes = System.IO.File.ReadAllBytes(imagePath);
                    imageBase64 = Convert.ToBase64String(imageBytes);
                }

                result.Add(new ContainerInfo
                {
                    Name = name,
                    Description = description,
                    ImageBase64 = imageBase64
                });
            }

            return Ok(result);
        }
    }

    public class MessageRequest
    {
        public string message { get; set; }
    }

    // ====== Клас-модель для повернення списку контейнерів ======
    public class ContainerInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageBase64 { get; set; }
    }
}