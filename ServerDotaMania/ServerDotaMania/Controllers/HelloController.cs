using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace ServerDotaMania.Controllers
{
    [Route("api")]
    [ApiController]
    public class HelloController : ControllerBase
    {
        private readonly ILogger<HelloController> _logger;
        private readonly Cloudinary _cloudinary;
        private readonly IHttpClientFactory _httpClientFactory;

        public HelloController(
            ILogger<HelloController> logger,
            Cloudinary cloudinary,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _cloudinary = cloudinary;
            _httpClientFactory = httpClientFactory;
        }

        // ================== Завантаження контейнера ==================
        [HttpPost("uploadContainer")]
        public IActionResult UploadContainer(
            [FromForm] string? containerName,
            [FromForm] string? description,
            [FromForm] IFormFile? file)
        {
            _logger.LogInformation("=== POST /api/uploadContainer: Start ===");
            _logger.LogInformation("Parameters: containerName='{ContainerName}', description='{Description}', fileName='{FileName}', fileSize={FileSize}",
                containerName, description, file?.FileName, file?.Length);

            if (string.IsNullOrEmpty(containerName))
            {
                _logger.LogWarning("Container name is missing.");
                return BadRequest("Container name is required.");
            }

            bool hasImage = (file != null && file.Length > 0);
            string prefix = $"containers/{containerName}";

            // 1. Видаляємо старі ресурси з public_id, що починається з prefix
            _logger.LogInformation("Deleting existing resources by prefix='{Prefix}'", prefix);
            try
            {
                // Видаляємо всі (image, raw, video), що починаються з containers/{containerName}
                _cloudinary.DeleteResourcesByPrefix(prefix);
                _logger.LogInformation("Successfully deleted resources with prefix='{Prefix}' (if any).", prefix);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting resources by prefix='{Prefix}'", prefix);
                return StatusCode(500, "Error clearing old container resources.");
            }

            // 2. Завантажуємо зображення (якщо є)
            if (hasImage)
            {
                _logger.LogInformation("Uploading image for container='{ContainerName}'...", containerName);
                var imageParams = new ImageUploadParams
                {
                    File = new FileDescription(file!.FileName, file.OpenReadStream()),
                    Folder = prefix
                };

                try
                {
                    // Синхронний метод _cloudinary.Upload(...)
                    var imgResult = _cloudinary.Upload(imageParams);
                    if (imgResult.StatusCode == HttpStatusCode.OK)
                    {
                        _logger.LogInformation("Image uploaded successfully. PublicId={PublicId}", imgResult.PublicId);
                    }
                    else
                    {
                        _logger.LogError("ERROR uploading image. StatusCode={StatusCode}", imgResult.StatusCode);
                        return StatusCode(500, "Error uploading image.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "UNHANDLED EXCEPTION while uploading image for container='{ContainerName}'", containerName);
                    return StatusCode(500, "Unhandled exception during image upload.");
                }
            }
            else
            {
                _logger.LogInformation("No image provided. Only uploading description.txt if any description is given.");
            }

            // 3. Завантажуємо description.txt (raw)
            var descContent = description ?? "";
            var descBytes = Encoding.UTF8.GetBytes(descContent);
            using var ms = new MemoryStream(descBytes);

            var descParams = new RawUploadParams
            {
                File = new FileDescription("description.txt", ms),
                Folder = prefix,
                UseFilename = true
            };

            _logger.LogInformation("Uploading description.txt for container='{ContainerName}'...", containerName);
            try
            {
                var descResult = _cloudinary.Upload(descParams);
                if (descResult.StatusCode == HttpStatusCode.OK)
                {
                    _logger.LogInformation("description.txt uploaded successfully. PublicId={PublicId}", descResult.PublicId);
                }
                else
                {
                    _logger.LogError("ERROR uploading description.txt. StatusCode={StatusCode}", descResult.StatusCode);
                    return StatusCode(500, "Error uploading description file.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UNHANDLED EXCEPTION while uploading description.txt for container='{ContainerName}'", containerName);
                return StatusCode(500, "Unhandled exception during description upload.");
            }

            _logger.LogInformation("POST /api/uploadContainer: Finished successfully for container='{ContainerName}'.", containerName);
            return Ok(new { message = "Container uploaded successfully." });
        }

        // ================== Видалення контейнера ==================
        [HttpDelete("deleteContainer/{containerName}")]
        public IActionResult DeleteContainer(string? containerName)
        {
            _logger.LogInformation("=== DELETE /api/deleteContainer: Start ===");
            _logger.LogInformation("containerName='{ContainerName}'", containerName);

            if (string.IsNullOrEmpty(containerName))
            {
                _logger.LogWarning("Container name is missing for deletion.");
                return BadRequest("Container name is required.");
            }

            var prefix = $"containers/{containerName}";
            _logger.LogInformation("Deleting resources by prefix='{Prefix}'", prefix);

            try
            {
                // Видаляємо все, що починається з containers/{containerName}
                _cloudinary.DeleteResourcesByPrefix(prefix);
                _logger.LogInformation("SUCCESS. Container '{ContainerName}' deleted from Cloudinary.", containerName);
                return Ok(new { message = "Container deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting container '{ContainerName}'", containerName);
                return StatusCode(500, new { message = "Error deleting container." });
            }
        }

        // ================== Отримання списку контейнерів (через Search з type=upload) ==================
        [HttpGet("containers")]
        public IActionResult GetAllContainers()
        {
            _logger.LogInformation("=== GET /api/containers: Start fetching containers from Cloudinary ===");

            // Використовуємо Search(), бо .ListResourcesParams не має Prefix/PublicIdPrefix
            // Спробуємо вираз: type=upload AND public_id:"containers/*"
            var expression = "type=upload AND public_id:\"containers/*\"";

            var searchResult = _cloudinary.Search()
                .Expression(expression)
                .MaxResults(500)
                .Execute();

            if (searchResult?.Resources == null || searchResult.Resources.Count == 0)
            {
                _logger.LogInformation("No containers found. Returning empty list.");
                return Ok(new List<ContainerInfo>());
            }

            _logger.LogInformation("Found {Count} resources with expression='{Expression}'", searchResult.Resources.Count, expression);

            // Парсимо containerName із PublicId
            var dict = new Dictionary<string, List<SearchResource>>();

            foreach (var resource in searchResult.Resources)
            {
                var pubId = resource.PublicId;
                if (!pubId.StartsWith("containers/"))
                    continue;

                var rest = pubId.Substring("containers/".Length);
                var slashIndex = rest.IndexOf('/');
                if (slashIndex < 0)
                {
                    _logger.LogWarning("File {PublicId} doesn't have a second slash. Skipping.", pubId);
                    continue;
                }

                var contName = rest.Substring(0, slashIndex);
                if (!dict.ContainsKey(contName))
                    dict[contName] = new List<SearchResource>();

                dict[contName].Add(resource);
            }

            var result = new List<ContainerInfo>();

            foreach (var kvp in dict)
            {
                var contName = kvp.Key;
                var resources = kvp.Value;
                _logger.LogInformation("Processing container='{ContainerName}' with {Count} resources.", contName, resources.Count);

                string? description = null;
                string? imageBase64 = null;

                foreach (var res in resources)
                {
                    // Якщо це description.txt
                    if (res.PublicId.EndsWith("description.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        if (res.SecureUrl != null)
                        {
                            try
                            {
                                var url = res.SecureUrl.ToString();
                                using var httpClient = _httpClientFactory.CreateClient();
                                var fileBytes = httpClient.GetByteArrayAsync(url).Result; // синхронний виклик
                                description = Encoding.UTF8.GetString(fileBytes);
                                _logger.LogInformation("Downloaded description.txt for container='{ContainerName}'", contName);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error downloading description.txt for container='{ContainerName}'", contName);
                            }
                        }
                    }
                    // Якщо це зображення
                    else if (res.ResourceType == ResourceType.Image && imageBase64 == null)
                    {
                        if (res.SecureUrl != null)
                        {
                            try
                            {
                                var url = res.SecureUrl.ToString();
                                using var httpClient = _httpClientFactory.CreateClient();
                                var imgBytes = httpClient.GetByteArrayAsync(url).Result;
                                imageBase64 = Convert.ToBase64String(imgBytes);
                                _logger.LogInformation("Downloaded image for container='{ContainerName}'", contName);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error downloading image for container='{ContainerName}'", contName);
                            }
                        }
                    }
                }

                result.Add(new ContainerInfo
                {
                    Name = contName,
                    Description = description,
                    ImageBase64 = imageBase64
                });
            }

            _logger.LogInformation("Returning {Count} containers.", result.Count);
            return Ok(result);
        }
    }

    // ====== Класи-моделі ======
    public class ContainerInfo
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ImageBase64 { get; set; }
    }
}
