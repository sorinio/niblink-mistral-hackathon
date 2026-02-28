using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TriggerDetective.Application.Interfaces;

namespace TriggerDetective.Api.Controllers;

[ApiController]
[Route("api/v1/voice")]
[Authorize]
public class VoiceController : ControllerBase
{
    private readonly IVoiceTranscriptionService _transcriptionService;

    public VoiceController(IVoiceTranscriptionService transcriptionService)
    {
        _transcriptionService = transcriptionService;
    }

    [HttpPost("transcribe")]
    public async Task<IActionResult> Transcribe(
        IFormFile audio,
        [FromQuery] string locale = "en")
    {
        if (audio.Length == 0)
            return BadRequest(new { error = "Audio file is empty" });

        if (audio.Length > 25 * 1024 * 1024) // 25MB limit
            return BadRequest(new { error = "Audio file too large (max 25MB)" });

        await using var stream = audio.OpenReadStream();
        var text = await _transcriptionService.TranscribeAsync(stream, audio.FileName, locale);

        return Ok(new { text });
    }
}
