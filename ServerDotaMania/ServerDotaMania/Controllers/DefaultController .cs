using Microsoft.AspNetCore.Mvc;

namespace ServerDotaMania.Controllers;

[ApiController]
[Route("")]
public class DefaultController : ControllerBase
{
    [HttpGet]
    public ContentResult Index()
    {
        var html = @"
<html>
<head>
    <title>My Server</title>
</head>
<body>
    <h1>Welcome to my server!</h1>
    <pre id='logContainer' style='background:#f0f0f0; padding:10px;'></pre>

    <button onclick='testCloudinary()'>Test Cloudinary</button>

    <script>
        // Функція для отримання логів
        async function fetchLogs() {
            const res = await fetch('/logs');
            const data = await res.json();
            document.getElementById('logContainer').innerText = data.join('\n');
        }
        setInterval(fetchLogs, 1000);

        // Тестове завантаження (GET /api/testCloudinary)
        async function testCloudinary() {
            try {
                const res = await fetch('/api/testCloudinary');
                const text = await res.text();
                alert(text);
            } catch(e) {
                alert('Error: ' + e);
            }
        }
    </script>
</body>
</html>";

        return new ContentResult
        {
            ContentType = "text/html",
            Content = html
        };
    }
}