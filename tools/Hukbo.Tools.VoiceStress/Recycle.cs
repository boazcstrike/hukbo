using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace Hukbo.Tools.VoiceStress;

/// <summary>
/// Answers one question the MonoGame source alone cannot settle for this
/// machine: are OpenAL sources genuinely returned to the pool after a
/// fire-and-forget clip finishes, or do they leak until the backend stops
/// producing sound? A leak would explain audio that works at the start of a
/// battle and dies later far better than the cue budget does.
/// </summary>
internal static class Recycle
{
    public static void Run(IReadOnlyList<SoundEffect> effects, float volume)
    {
        Console.WriteLine("=== Phase C: source recycling over repeated saturation ===");
        Console.WriteLine("Saturates the device, drains it, and repeats. If sources leak, the");
        Console.WriteLine("accepted count falls with each round and never recovers.");
        Console.WriteLine();
        Console.WriteLine("round  accepted  refused  afterDrainAccepted");

        var clip = effects.OrderByDescending(effect => effect.Duration).First();

        for (var round = 1; round <= 6; round++)
        {
            var accepted = 0;
            var refused = 0;

            // Push well past the known 256-source ceiling.
            for (var attempt = 0; attempt < 400; attempt++)
            {
                try
                {
                    if (clip.Play(volume, 0f, 0f))
                    {
                        accepted++;
                    }
                    else
                    {
                        refused++;
                    }
                }
                catch (Exception exception) when (
                    exception is InstancePlayLimitException or
                    NoAudioHardwareException or
                    InvalidOperationException)
                {
                    refused++;
                }
            }

            // Let every clip finish, pumping the dispatcher the way Game.Update
            // does, then measure how much capacity came back.
            var drainUntil = Stopwatch.GetTimestamp();
            while (Stopwatch.GetElapsedTime(drainUntil).TotalSeconds < 1.5)
            {
                FrameworkDispatcher.Update();
                Thread.Sleep(16);
            }

            var afterDrain = 0;
            for (var attempt = 0; attempt < 400; attempt++)
            {
                try
                {
                    if (clip.Play(volume, 0f, 0f))
                    {
                        afterDrain++;
                    }
                }
                catch (Exception exception) when (
                    exception is InstancePlayLimitException or
                    NoAudioHardwareException or
                    InvalidOperationException)
                {
                    // Ceiling reached; that is the number being measured.
                }
            }

            Console.WriteLine($"{round,5}  {accepted,8}  {refused,7}  {afterDrain,18}");

            var settleFrom = Stopwatch.GetTimestamp();
            while (Stopwatch.GetElapsedTime(settleFrom).TotalSeconds < 1.5)
            {
                FrameworkDispatcher.Update();
                Thread.Sleep(16);
            }
        }

        Console.WriteLine();
    }
}
