using Hukbo.Client.Audio;

namespace Hukbo.Client.Tests;

public sealed class SoundCueLogTests
{
    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SoundCueLog(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SoundCueLog(-1));
    }

    [Fact]
    public void Append_CollapsesIdenticalConsecutiveCuesIntoOneRow()
    {
        var log = new SoundCueLog(capacity: 8);

        for (var index = 0; index < 40; index++)
        {
            log.Append(7, GameSoundId.AttackGreatBlade, SoundCueStatus.Suppressed);
        }

        var row = Assert.Single(log.Entries);
        Assert.Equal(7, row.Tick);
        Assert.Equal(GameSoundId.AttackGreatBlade, row.Sound);
        Assert.Equal(SoundCueStatus.Suppressed, row.Status);
        Assert.Equal(40, row.Count);
    }

    [Fact]
    public void Append_StartsANewRowWhenTickSlotOrStatusChanges()
    {
        var log = new SoundCueLog(capacity: 8);

        log.Append(1, GameSoundId.Death, SoundCueStatus.Played);
        log.Append(1, GameSoundId.Death, SoundCueStatus.Missing);
        log.Append(1, GameSoundId.Draw, SoundCueStatus.Missing);
        log.Append(2, GameSoundId.Draw, SoundCueStatus.Missing);

        Assert.Equal(4, log.Entries.Count);
        Assert.All(log.Entries, cue => Assert.Equal(1, cue.Count));
    }

    [Fact]
    public void Append_RetainsOnlyTheNewestRowsUpToCapacity()
    {
        var log = new SoundCueLog(capacity: 3);

        for (var tick = 1; tick <= 6; tick++)
        {
            log.Append(tick, GameSoundId.Death, SoundCueStatus.Played);
        }

        Assert.Equal(3, log.Entries.Count);
        Assert.Equal(4, log.Entries[0].Tick);
        Assert.Equal(6, log.Entries[^1].Tick);
    }

    [Fact]
    public void GetVisibleEntries_ShowsTheNewestRowsWhilePinnedToBottom()
    {
        var log = Filled(rowCount: 10);

        var visible = log.GetVisibleEntries(visibleRowCount: 3);

        Assert.True(log.IsPinnedToBottom);
        Assert.Equal(3, visible.Length);
        Assert.Equal(8, visible[0].Tick);
        Assert.Equal(10, visible[^1].Tick);
    }

    [Fact]
    public void Scroll_UnpinsUpwardAndRepinsAtTheBottom()
    {
        var log = Filled(rowCount: 10);

        log.Scroll(-3, visibleRowCount: 3);

        Assert.False(log.IsPinnedToBottom);
        Assert.Equal(4, log.GetScrollStart(3));
        Assert.Equal(5, log.GetVisibleEntries(3)[0].Tick);

        log.Scroll(3, visibleRowCount: 3);

        Assert.True(log.IsPinnedToBottom);
        Assert.Equal(7, log.GetScrollStart(3));
    }

    [Fact]
    public void Scroll_ClampsBeyondBothEnds()
    {
        var log = Filled(rowCount: 5);

        log.Scroll(-1000, visibleRowCount: 2);
        Assert.Equal(0, log.GetScrollStart(2));

        log.Scroll(1000, visibleRowCount: 2);
        Assert.Equal(3, log.GetScrollStart(2));
        Assert.True(log.IsPinnedToBottom);
    }

    [Fact]
    public void Scroll_HoldsPositionWhileNewRowsArriveUnpinned()
    {
        var log = Filled(rowCount: 10);
        log.Scroll(-5, visibleRowCount: 3);
        var start = log.GetScrollStart(3);

        log.Append(99, GameSoundId.Draw, SoundCueStatus.Played);

        Assert.False(log.IsPinnedToBottom);
        Assert.Equal(start, log.GetScrollStart(3));
    }

    [Fact]
    public void Scroll_RejectsANegativeVisibleRowCount()
    {
        var log = new SoundCueLog(capacity: 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => log.Scroll(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => log.GetScrollStart(-1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => log.GetVisibleEntries(-1));
    }

    [Fact]
    public void Clear_EmptiesTheLogAndRepins()
    {
        var log = Filled(rowCount: 10);
        log.Scroll(-4, visibleRowCount: 3);

        log.Clear();

        Assert.Empty(log.Entries);
        Assert.True(log.IsPinnedToBottom);
        Assert.Equal(0, log.GetScrollStart(3));
    }

    private static SoundCueLog Filled(int rowCount)
    {
        var log = new SoundCueLog(capacity: 200);
        for (var tick = 1; tick <= rowCount; tick++)
        {
            log.Append(tick, GameSoundId.Death, SoundCueStatus.Played);
        }

        return log;
    }
}
