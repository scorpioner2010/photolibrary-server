using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net;
using ServerDotaMania.Models;
using ServerDotaMania.DTOs;
using ServerDotaMania.Settings;

namespace ServerDotaMania.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContainersController : ControllerBase
    {
        // In-memory сховище контейнерів
        private static readonly List<ContainerModel> _containers = new List<ContainerModel>();
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<ContainersController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public ContainersController(IOptions<CloudinarySettings> config, ILogger<ContainersController> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            var account = new Account(
                config.Value.CloudName,
                config.Value.ApiKey,
                config.Value.ApiSecret);
            _cloudinary = new Cloudinary(account);
            _logger.LogInformation("Cloudinary initialized with CloudName: {CloudName}", config.Value.CloudName);
        }

        // POST: /api/containers
        [HttpPost]
        public async Task<IActionResult> CreateContainer([FromForm] ContainerCreateDto dto)
        {
            _logger.LogInformation("CreateContainer request received. Container Name: {Name}", dto.Name);

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
                _logger.LogInformation("Uploading image for container {Name}.", dto.Name);
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

                var container = new ContainerModel
                {
                    Id = _containers.Count + 1,
                    Name = dto.Name,
                    Description = dto.Description,
                    ImageUrl = uploadResult.SecureUrl?.ToString() ?? "",
                    ImagePublicId = uploadResult.PublicId
                };

                _containers.Add(container);
                _logger.LogInformation("Container {Name} created successfully with Id {Id}.", container.Name, container.Id);
                return Ok(container);
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
            _logger.LogInformation("DeleteContainer request received for container: {Name}", name);

            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning("Container name is missing in delete request.");
                return BadRequest("Container name is required.");
            }

            var container = _containers.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (container == null)
            {
                _logger.LogWarning("Container {Name} not found.", name);
                return NotFound("Container not found.");
            }

            try
            {
                _logger.LogInformation("Deleting image from Cloudinary for container {Name} with PublicId {PublicId}", name, container.ImagePublicId);
                var deletionParams = new DeletionParams(container.ImagePublicId);
                var deletionResult = await _cloudinary.DestroyAsync(deletionParams);
                if (deletionResult.Result != "ok")
                {
                    _logger.LogError("Error deleting image from Cloudinary for container {Name}. Result: {Result}", name, deletionResult.Result);
                    return StatusCode(500, "Error deleting image from Cloudinary.");
                }

                _containers.Remove(container);
                _logger.LogInformation("Container {Name} deleted successfully.", name);
                return Ok("Container deleted successfully.");
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
            _logger.LogInformation("GetAllContainers request received. Total containers in memory: {Count}", _containers.Count);
            var httpClient = _httpClientFactory.CreateClient();
            var containerInfoList = new List<object>();

            foreach (var container in _containers)
            {
                string imageBase64 = "";
                if (!string.IsNullOrEmpty(container.ImageUrl))
                {
                    try
                    {
                        _logger.LogInformation("Fetching image for container '{Name}' from URL: {ImageUrl}", container.Name, container.ImageUrl);
                        byte[] imageBytes = await httpClient.GetByteArrayAsync(container.ImageUrl);
                        imageBase64 = Convert.ToBase64String(imageBytes);
                        _logger.LogInformation("Successfully fetched image for container '{Name}'. Base64 string length: {Length}", container.Name, imageBase64.Length);
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
                    imageBase64 = imageBase64
                });
            }

            _logger.LogInformation("Returning {Count} containers from GET endpoint.", containerInfoList.Count);
            return Ok(containerInfoList);
        }
    }
}
