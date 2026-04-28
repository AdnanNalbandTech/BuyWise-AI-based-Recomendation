using Microsoft.AspNetCore.Mvc;

namespace BuyWise.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "BUYWISE API is running", timestamp = DateTime.UtcNow });
}
