using Microsoft.AspNetCore.Mvc;

namespace ServerDotaMania.Controllers;

[ApiController]
[Route("")]
public class DefaultController : ControllerBase
{
    [HttpGet]
    public ContentResult Index()
    {
        // HTML із простим JavaScript, який кожну секунду забирає логи з /logs
        var html = @"
<html>
<head>
    <title>My Server</title>
</head>
<body>
    <h1>Welcome to my server!</h1>
    <pre id='logContainer' style='background:#f0f0f0; padding:10px;'></pre>
    <script>
        async function fetchLogs() {
            const res = await fetch('/logs');
            const data = await res.json();
            document.getElementById('logContainer').innerText = data.join('\n');
        }
        setInterval(fetchLogs, 1000);
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