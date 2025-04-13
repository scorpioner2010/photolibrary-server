namespace ServerDotaMania.DTOs
{
    public class ContainerCreateDto
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public IFormFile? Image { get; set; }
    }
}