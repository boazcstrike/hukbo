using Hukbo.Client.Presentation;

namespace Hukbo.Client.Tests;

public sealed class PlaybackControllerTests
{
    [Fact]
    public void InitialState_IsPaused()
    {
        var controller = new PlaybackController();

        Assert.False(controller.IsPlaying);
    }

    [Fact]
    public void Play_MakesPlaybackActive()
    {
        var controller = new PlaybackController();

        controller.Play();

        Assert.True(controller.IsPlaying);
    }

    [Fact]
    public void Pause_MakesPlaybackInactive()
    {
        var controller = new PlaybackController();
        controller.Play();

        controller.Pause();

        Assert.False(controller.IsPlaying);
    }

    [Fact]
    public void Toggle_ChangesStateExactlyOncePerCall()
    {
        var controller = new PlaybackController();

        controller.Toggle();
        Assert.True(controller.IsPlaying);

        controller.Toggle();
        Assert.False(controller.IsPlaying);
    }

    [Fact]
    public void ApplyingSameState_IsIdempotent()
    {
        var controller = new PlaybackController();

        controller.Play();
        controller.Play();
        Assert.True(controller.IsPlaying);

        controller.Pause();
        controller.Pause();
        Assert.False(controller.IsPlaying);
    }
}
