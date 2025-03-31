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
            _cloudName = config.Value.CloudName;
            _logger.LogInformation("Cloudinary initialized with CloudName: {CloudName}", _cloudName);
        }

        // Формує URL з міткою часу для уникнення кешування
        private string GetContainersDataUrl()
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return $"https://res.cloudinary.com/{_cloudName}/raw/upload/{ContainersDataPublicId}.json?v={timestamp}";
        }

        // Завантаження даних у вигляді словника (ключ – GUID)
        private async Task<Dictionary<string, ContainerModel>> DownloadContainersDataAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = GetContainersDataUrl();
                var jsonData = await client.GetStringAsync(url);
                var containers = JsonConvert.DeserializeObject<Dictionary<string, ContainerModel>>(jsonData);
                _logger.LogInformation("Containers data downloaded successfully. Count: {Count}", containers?.Count ?? 0);
                return containers ?? new Dictionary<string, ContainerModel>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not download containers data. Initializing empty dictionary. Exception: {Exception}", ex);
                return new Dictionary<string, ContainerModel>();
            }
        }

        // Завантаження оновлених даних у Cloudinary
        private async Task UploadContainersDataAsync(Dictionary<string, ContainerModel> containers)
        {
            try
            {
                var jsonData = JsonConvert.SerializeObject(containers);
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));
                var uploadParams = new RawUploadParams
                {
                    File = new FileDescription("containers.json", ms),
                    PublicId = ContainersDataPublicId,
                    Invalidate = true
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

        // Очікування підтвердження оновлення даних
        private async Task<Dictionary<string, ContainerModel>> WaitForUpdatedContainersDataAsync(Dictionary<string, ContainerModel> expectedContainers)
        {
            string expectedJson = JsonConvert.SerializeObject(expectedContainers);
            int maxAttempts = 10;
            int delayMs = 1000;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    var url = GetContainersDataUrl();
                    var jsonData = await client.GetStringAsync(url);
                    if (jsonData.Trim() == expectedJson.Trim())
                    {
                        _logger.LogInformation("Updated containers data confirmed on attempt {Attempt}.", attempt + 1);
                        return JsonConvert.DeserializeObject<Dictionary<string, ContainerModel>>(jsonData);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Attempt {Attempt} failed: {Exception}", attempt + 1, ex.Message);
                }
                await Task.Delay(delayMs);
            }
            _logger.LogWarning("Failed to confirm updated containers data after {Attempts} attempts.", maxAttempts);
            return expectedContainers;
        }

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
                var containers = await DownloadContainersDataAsync();

                // Перевірка на дублювання за ім'ям (без врахування регістру)
                if (containers.Values.Any(c => c.Name.Equals(dto.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Container with name {Name} already exists.", dto.Name);
                    return Conflict("Такий контейнер вже існує.");
                }

                _logger.LogInformation("Uploading image for container {Name}", dto.Name);
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(dto.Image.FileName, dto.Image.OpenReadStream())
                };
                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                if (uploadResult.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogError("Error uploading image for container {Name}. Status: {StatusCode}", dto.Name, uploadResult.StatusCode);
                    return StatusCode(500, "Error uploading image.");
                }

                // Генерація унікального GUID
                string newId = Guid.NewGuid().ToString();
                ContainerModel container = new ContainerModel
                {
                    Id = newId,
                    Name = dto.Name,
                    Description = dto.Description,
                    ImageUrl = uploadResult.SecureUrl?.ToString() ?? "",
                    ImagePublicId = uploadResult.PublicId
                };

                containers.Add(newId, container);
                _logger.LogInformation("Container {Name} created successfully with Id {Id}", container.Name, container.Id);

                await UploadContainersDataAsync(containers);
                var confirmedContainers = await WaitForUpdatedContainersDataAsync(containers);

                _logger.LogInformation("=== CreateContainer END. ===");
                return Ok(confirmedContainers.Values.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in CreateContainer for container {Name}", dto.Name);
                return StatusCode(500, "Internal server error in creating container.");
            }
        }

        // DELETE: /api/containers/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteContainer(string id)
        {
            _logger.LogInformation("=== DeleteContainer START. Id: {Id} ===", id);
            if (string.IsNullOrWhiteSpace(id))
            {
                _logger.LogWarning("Container Id is missing in delete request.");
                return BadRequest("Container Id is required.");
            }

            try
            {
                var containers = await DownloadContainersDataAsync();
                if (!containers.TryGetValue(id, out var container))
                {
                    _logger.LogWarning("Container with Id {Id} not found.", id);
                    return NotFound("Container not found.");
                }

                _logger.LogInformation("Deleting image from Cloudinary for container Id {Id}", id);
                var deletionParams = new DeletionParams(container.ImagePublicId);
                var deletionResult = await _cloudinary.DestroyAsync(deletionParams);
                if (deletionResult.Result != "ok")
                {
                    _logger.LogError("Error deleting image for container Id {Id}. Result: {Result}", id, deletionResult.Result);
                    return StatusCode(500, "Error deleting image from Cloudinary.");
                }

                containers.Remove(id);
                _logger.LogInformation("Container with Id {Id} deleted successfully.", id);

                await UploadContainersDataAsync(containers);
                var confirmedContainers = await WaitForUpdatedContainersDataAsync(containers);

                _logger.LogInformation("=== DeleteContainer END. ===");
                return Ok(confirmedContainers.Values.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in DeleteContainer for Id {Id}", id);
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
                var containers = await DownloadContainersDataAsync();
                var httpClient = _httpClientFactory.CreateClient();
                var containerInfoList = new List<object>();

                foreach (var container in containers.Values)
                {
                    string imageBase64 = "";
                    if (!string.IsNullOrEmpty(container.ImageUrl))
                    {
                        try
                        {
                            var imageBytes = await httpClient.GetByteArrayAsync(container.ImageUrl);
                            imageBase64 = Convert.ToBase64String(imageBytes);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error fetching image for container Id {Id} from URL: {ImageUrl}", container.Id, container.ImageUrl);
                        }
                    }

                    containerInfoList.Add(new
                    {
                        id = container.Id,
                        name = container.Name,
                        description = container.Description,
                        imageBase64
                    });
                }

                _logger.LogInformation("Returning {Count} containers.", containerInfoList.Count);
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
