using Microsoft.AspNetCore.Mvc;

namespace Softtek_APIExplorer_Backend.Models;

public class PlaygroundLoadFormRequest
{
    public string? SwaggerUrl { get; set; }

    public IFormFile? OpenApiFile { get; set; }

    public string? SessionId { get; set; }
}
