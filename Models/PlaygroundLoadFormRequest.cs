using Microsoft.AspNetCore.Mvc;

namespace Softtek_APIExplorer_Backend.Models;

public sealed class PlaygroundLoadFormRequest
{
    [FromForm(Name = "swaggerUrl")]
    public string? SwaggerUrl { get; set; }

    [FromForm(Name = "openApiFile")]
    public IFormFile? OpenApiFile { get; set; }

    [FromForm(Name = "sessionId")]
    public string? SessionId { get; set; }
}
