using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TriggerDetective.Application.DTOs.ProductScan;
using TriggerDetective.Application.Interfaces;

namespace TriggerDetective.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductScannerService _scannerService;

    public ProductsController(IProductScannerService scannerService)
    {
        _scannerService = scannerService;
    }

    /// <summary>
    /// Scan a product label photo and get a safety verdict.
    /// Returns a traffic light rating (Green/Yellow/Red) with per-ingredient analysis.
    /// </summary>
    [HttpPost("scan")]
    public async Task<ActionResult<ProductScanResultDto>> ScanLabel(
        IFormFile photo,
        [FromQuery] string locale = "en")
    {
        if (photo.Length == 0)
            return BadRequest(new { error = "Photo file is empty" });

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(photo.ContentType.ToLowerInvariant()))
            return BadRequest(new { error = "Invalid file type. Allowed: jpg, png, webp" });

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        Stream? photoStream = photo.OpenReadStream();
        try
        {
            var result = await _scannerService.ScanProductLabelAsync(userId, photoStream, locale);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }
        finally
        {
            await photoStream.DisposeAsync();
        }
    }
}
