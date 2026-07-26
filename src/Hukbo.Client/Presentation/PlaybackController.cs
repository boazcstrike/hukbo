namespace Hukbo.Client.Presentation;

internal sealed class PlaybackController
{
    public bool IsPlaying { get; private set; }

    public void Play() => IsPlaying = true;

    public void Pause() => IsPlaying = false;

    public void Toggle() => IsPlaying = !IsPlaying;
}
