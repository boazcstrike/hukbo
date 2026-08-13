namespace Hukbo.Client.Audio;

/// <summary>
/// A pure per-slot lookup giving each of the four melee shield-clash slots a
/// relative playback level and a pitch offset, so that after
/// <see cref="WavePeak"/> normalisation removes the take-to-take spread within
/// one slot, the four slots still read as four different weapons rather than
/// as four identical wooden impacts. Every other slot in the catalog gets
/// identity voicing — level <c>1.0</c>, pitch <c>0</c> — and plays exactly as
/// it did before this table existed.
/// </summary>
/// <remarks>
/// Every value below is a provisional tuning value picked to match the
/// ordering row 173 of the smoke checklist asked for — the Wasay heaviest and
/// lowest, the Kampilan just behind it, the Kalis in the middle, and the Itak
/// the lightest, highest, and quietest of the four. None of it is a
/// measurement of a real weapon and none of it is a historical claim; see
/// <c>docs/plans/2026-08-13-shield-clash-legibility-design.md</c> section 3.
/// </remarks>
internal static class SoundVoicing
{
    /// <summary>
    /// The relative level and pitch offset for one slot. Level multiplies the
    /// gain the director would otherwise play at; pitch is passed to
    /// <see cref="ISoundPlayer.Play"/> unchanged and must already sit inside
    /// MonoGame's <c>[-1, 1]</c> pitch range.
    /// </summary>
    public static (float Level, float Pitch) Get(GameSoundId sound) =>
        sound switch
        {
            GameSoundId.ClashShieldWasay => (1.00f, -0.35f),
            GameSoundId.ClashShieldKampilan => (0.95f, -0.20f),
            GameSoundId.ClashShieldKalis => (0.80f, +0.10f),
            GameSoundId.ClashShieldItak => (0.60f, +0.35f),
            _ => (1.0f, 0.0f),
        };
}
