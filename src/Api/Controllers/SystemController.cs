using Microsoft.AspNetCore.Mvc;

namespace FlashAssessment.Api.Controllers;

[ApiController]
[Route("/health")] 
public sealed class SystemController : ControllerBase
{
    [HttpGet]
    public IActionResult Liveness() => Ok(new { status = "ok" });

    [HttpGet("ready")]
    public IActionResult Readiness() => Ok(new { status = "ready" });
}


