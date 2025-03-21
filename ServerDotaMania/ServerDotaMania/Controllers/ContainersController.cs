using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using ServerDotaMania.Models;
using ServerDotaMania.DTOs;
using ServerDotaMania.Settings;

namespace ServerDotaMania.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContainersController : ControllerBase
    {
        // Постійний PublicId для JSON-файлу
        private const string ContainersDataPublicId = "containers_data";

        private readonly Cloudinary _cloudinary;
        private readonly ILogger<ContainersController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _cloudName;

        public ContainersController(
            IOptions<CloudinarySettings> config,
            ILogger<ContainersController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;

            var account = new Account(
                config.Value.CloudName,
                config.Value.ApiKey,
                config.Value.ApiSecret);

            _cloudinary = new Cloudinary(account);
            _cloudName = config.Value.CloudName;  // Запам'ятовуємо для побудови URL
            _logger.LogInformation("Cloudinary initialized with CloudName: {CloudName}", _cloudName);
        }

        // ────────────────────────────────────────────────────────────────────────────────
        // ДОПОМІЖНІ МЕТОДИ
        // ────────────────────────────────────────────────────────────────────────────────

        // Формує URL до JSON-файлу з параметром часу для уникнення кешування
        private string GetContainersDataUrl()
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return $"https://res.cloudinary.com/{_cloudName}/raw/upload/{ContainersDataPublicId}.json?v={timestamp}";
        }

        // Завантажує JSON-файл з Cloudinary та десеріалізує у список контейнерів
        private async Task<List<ContainerModel>> DownloadContainersDataAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = GetContainersDataUrl();

                var jsonData = await client.GetStringAsync(url);
                var containers = JsonConvert.DeserializeObject<List<ContainerModel>>(jsonData);

                _logger.LogInformation("Containers data downloaded successfully. Items count: {Count}",
                    containers?.Count ?? 0);

                return containers ?? new List<ContainerModel>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not download containers data. Initializing empty list. Exception: {Exception}", ex);
                return new List<ContainerModel>();
            }
        }

        // Завантажує оновлений список контейнерів у вигляді JSON на Cloudinary
        private async Task UploadContainersDataAsync(List<ContainerModel> containers)
        {
            try
            {
                var jsonData = JsonConvert.SerializeObject(containers);

                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));
                var uploadParams = new RawUploadParams
                {
                    File = new FileDescription("containers.json", ms),
                    PublicId = ContainersDataPublicId,
                    Invalidate = true // Просимо Cloudinary очистити кеш
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                _logger.LogInformation("Containers data uploaded. PublicId: {PublicId}", uploadResult.PublicId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading containers data to Cloudinary.");
                throw;
            }
        }

        // Метод для перевірки оновлення даних із Cloudinary
        private async Task<List<ContainerModel>> WaitForUpdatedContainersDataAsync(List<ContainerModel> expectedContainers)
        {
            string expectedJson = JsonConvert.SerializeObject(expectedContainers);
            int maxAttempts = 10;
            int delayMs = 1000; // 1 секунда

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    var url = GetContainersDataUrl();
                    var jsonData = await client.GetStringAsync(url);

                    // Порівнюємо отриманий JSON із очікуваним (без пробільних символів)
                    if (jsonData.Trim() == expectedJson.Trim())
                    {
                        _logger.LogInformation("Updated containers data confirmed on attempt {Attempt}.", attempt + 1);
                        return JsonConvert.DeserializeObject<List<ContainerModel>>(jsonData);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Attempt {Attempt} failed to confirm updated containers data: {Exception}", attempt + 1, ex.Message);
                }
                await Task.Delay(delayMs);
            }

            _logger.LogWarning("Failed to confirm updated containers data after {Attempts} attempts.", maxAttempts);
            // Якщо не вдалося підтвердити оновлення, повертаємо очікувані дані
            return expectedContainers;
        }

        // ────────────────────────────────────────────────────────────────────────────────
        // ENDPOINTS
        // ────────────────────────────────────────────────────────────────────────────────

        // POST: /api/containers
        [HttpPost]
        public async Task<IActionResult> CreateContainer([FromForm] ContainerCreateDto dto)
        {
            _logger.LogInformation("=== CreateContainer START. Name: {Name} ===", dto.Name);

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                _logger.LogWarning("Container name is missing.");
                return BadRequest("Container name is required.");
            }
            if (dto.Image == null || dto.Image.Length == 0)
            {
                _logger.LogWarning("Image file is missing for container {Name}", dto.Name);
                return BadRequest("Image file is required.");
            }

            try
            {
                // 1. Завантаження поточного списку контейнерів
                var containers = await DownloadContainersDataAsync();

                // 2. Завантаження зображення на Cloudinary
                _logger.LogInformation("Uploading image for container {Name}", dto.Name);

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(dto.Image.FileName, dto.Image.OpenReadStream())
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                _logger.LogInformation("Cloudinary upload status code: {StatusCode}", uploadResult.StatusCode);

                if (uploadResult.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogError("Error uploading image for container {Name}. Status: {StatusCode}", dto.Name, uploadResult.StatusCode);
                    return StatusCode(500, "Error uploading image.");
                }

                // 3. Створення нового контейнера з унікальним Id
                int newId = containers.Any() ? containers.Max(c => c.Id) + 1 : 1;
                var container = new ContainerModel
                {
                    Id = newId,
                    Name = dto.Name,
                    Description = dto.Description,
                    ImageUrl = uploadResult.SecureUrl?.ToString() ?? "",
                    ImagePublicId = uploadResult.PublicId
                };

                containers.Add(container);
                _logger.LogInformation("Container {Name} created successfully with Id {Id}", container.Name, container.Id);

                // 4. Оновлення JSON на Cloudinary
                await UploadContainersDataAsync(containers);

                // 5. Очікуємо підтвердження оновлення даних
                var confirmedContainers = await WaitForUpdatedContainersDataAsync(containers);

                _logger.LogInformation("=== CreateContainer END. ===");
                // Повертаємо оновлений список контейнерів
                return Ok(confirmedContainers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in CreateContainer for container {Name}", dto.Name);
                return StatusCode(500, "Internal server error in creating container.");
            }
        }

        // DELETE: /api/containers/{name}
        [HttpDelete("{name}")]
        public async Task<IActionResult> DeleteContainer(string name)
        {
            _logger.LogInformation("=== DeleteContainer START. Name: {Name} ===", name);

            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning("Container name is missing in delete request.");
                return BadRequest("Container name is required.");
            }

            try
            {
                // 1. Завантаження поточного списку контейнерів
                var containers = await DownloadContainersDataAsync();

                // 2. Пошук контейнера
                var container = containers.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (container == null)
                {
                    _logger.LogWarning("Container {Name} not found.", name);
                    return NotFound("Container not found.");
                }

                // 3. Видалення зображення з Cloudinary
                _logger.LogInformation("Deleting image from Cloudinary for container {Name} with PublicId {PublicId}", name, container.ImagePublicId);
                var deletionParams = new DeletionParams(container.ImagePublicId);
                var deletionResult = await _cloudinary.DestroyAsync(deletionParams);

                if (deletionResult.Result != "ok")
                {
                    _logger.LogError("Error deleting image from Cloudinary for container {Name}. Result: {Result}", name, deletionResult.Result);
                    return StatusCode(500, "Error deleting image from Cloudinary.");
                }

                // 4. Видалення контейнера зі списку
                containers.Remove(container);
                _logger.LogInformation("Container {Name} deleted successfully.", name);

                // 5. Оновлення JSON на Cloudinary
                await UploadContainersDataAsync(containers);

                // 6. Очікуємо підтвердження оновлення даних
                var confirmedContainers = await WaitForUpdatedContainersDataAsync(containers);

                _logger.LogInformation("=== DeleteContainer END. ===");
                // Повертаємо оновлений список контейнерів
                return Ok(confirmedContainers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in DeleteContainer for container {Name}", name);
                return StatusCode(500, "Internal server error in deleting container.");
            }
        }

        // GET: /api/containers
        [HttpGet]
        public async Task<IActionResult> GetAllContainers()
        {
            _logger.LogInformation("=== GetAllContainers START. ===");
            try
            {
                // Завантаження поточного списку контейнерів з урахуванням часу для уникнення кешування
                var containers = await DownloadContainersDataAsync();
                var httpClient = _httpClientFactory.CreateClient();
                var containerInfoList = new List<object>();

                foreach (var container in containers)
                {
                    string imageBase64 = "";
                    _logger.LogInformation("Processing container: {Name}, Url: {Url}", container.Name, container.ImageUrl);

                    if (!string.IsNullOrEmpty(container.ImageUrl))
                    {
                        try
                        {
                            _logger.LogInformation("Fetching image for container '{Name}' from URL: {ImageUrl}", container.Name, container.ImageUrl);
                            var imageBytes = await httpClient.GetByteArrayAsync(container.ImageUrl);
                            imageBase64 = Convert.ToBase64String(imageBytes);
                            _logger.LogInformation("Successfully fetched image for container '{Name}'. Base64 length: {Length}", container.Name, imageBase64.Length);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error fetching image for container '{Name}' from URL: {ImageUrl}", container.Name, container.ImageUrl);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Container '{Name}' has no ImageUrl set.", container.Name);
                    }

                    containerInfoList.Add(new
                    {
                        name = container.Name,
                        description = container.Description,
                        imageBase64
                    });
                }

                _logger.LogInformation("Returning {Count} containers from GET endpoint.", containerInfoList.Count);
                _logger.LogInformation("=== GetAllContainers END. ===");
                return Ok(containerInfoList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in GetAllContainers");
                return StatusCode(500, "Internal server error in retrieving containers.");
            }
        }
    }
}
