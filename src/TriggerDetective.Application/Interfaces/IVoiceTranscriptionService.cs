namespace TriggerDetective.Application.Interfaces;

public interface IVoiceTranscriptionService
{
    Task<string> TranscribeAsync(Stream audio, string fileName, string locale);
}
