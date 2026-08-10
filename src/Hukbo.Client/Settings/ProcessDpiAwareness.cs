using System.Runtime.InteropServices;

namespace Hukbo.Client.Settings;

/// <summary>
/// The outcome of asking Windows for per-monitor DPI awareness, in a shape the
/// caller can log without re-deriving anything.
/// </summary>
/// <param name="Attempted">
/// Whether the call was made at all. False on a non-Windows platform, where
/// there is nothing to declare.
/// </param>
/// <param name="Succeeded">
/// Whether the call reported success. Always false when
/// <paramref name="Attempted"/> is false.
/// </param>
/// <param name="Win32ErrorCode">
/// The Win32 error code when the call was attempted and failed; zero
/// otherwise.
/// </param>
/// <param name="State">
/// A stable machine key for the log payload: <c>applied</c>, <c>failed</c>, or
/// <c>skipped</c>. Never a sentence.
/// </param>
internal readonly record struct DpiAwarenessOutcome(
    bool Attempted,
    bool Succeeded,
    int Win32ErrorCode,
    string State);

/// <summary>
/// Declares this process per-monitor DPI aware before any window exists.
/// </summary>
/// <remarks>
/// <para>
/// Without this declaration Windows treats the process as DPI-unaware: it
/// reports a virtualised desktop size, lets the game render at that size, and
/// then bitmap-stretches the finished frame up to the physical panel. On a
/// 2560x1440 display at 125% scaling the game is handed 2048x1152 and its
/// output is upscaled by a non-integer 1.25, which is what made every glyph
/// pixelated in the 2026-08-11 smoke run and failed rows UI-2, UI-4 and UI-6.
/// </para>
/// <para>
/// The virtualised size has a second effect worth naming here, because it
/// looks like a separate bug and is not:
/// <see cref="Theming.UiScalePolicy.Resolve"/> picks a font tier from the
/// viewport in pixels, and a fabricated 2048x1152 clears its 1920x1080
/// threshold but misses its 2560x1440 one. So an unaware process also selects
/// the 125% bake where the real panel deserves the 150% one. The policy needs
/// no change; it needs an honest viewport.
/// </para>
/// <para>
/// This must run before <c>GraphicsDeviceManager</c> is constructed and before
/// SDL creates a window, which is why it lives at the top of
/// <c>Program.Main</c> rather than inside <c>ArenaGame</c>. Awareness is
/// process-wide state that cannot be changed once the first window exists.
/// </para>
/// <para>
/// Failure is not fatal and is deliberately not thrown. An older Windows build
/// or an awareness level already set by a host process leaves the game running
/// exactly as it did before — pixelated on a scaled display, but working. The
/// caller logs the outcome so a run's record says which it got.
/// </para>
/// </remarks>
internal static partial class ProcessDpiAwareness
{
    /// <summary>
    /// <c>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2</c>. A pseudo-handle, not
    /// a pointer: <c>-4</c> is the documented constant of the Win32 ABI and is
    /// declared here rather than imported because it is a single stable value.
    /// </summary>
    private static readonly nint PerMonitorAwareV2 = -4;

    /// <summary>
    /// Whether to attempt the declaration. Pure, so the platform gate is
    /// pinned by a test rather than buried in an <c>if</c> no test can reach.
    /// </summary>
    public static bool ShouldAttempt(bool isWindows) => isWindows;

    /// <summary>
    /// Builds the outcome record from the raw call result. Pure, so the shape
    /// of the logged evidence is asserted rather than assumed.
    /// </summary>
    public static DpiAwarenessOutcome DescribeOutcome(
        bool attempted,
        bool succeeded,
        int win32ErrorCode)
    {
        if (!attempted)
        {
            return new DpiAwarenessOutcome(
                Attempted: false,
                Succeeded: false,
                Win32ErrorCode: 0,
                State: "skipped");
        }

        return succeeded
            ? new DpiAwarenessOutcome(true, true, 0, "applied")
            : new DpiAwarenessOutcome(true, false, win32ErrorCode, "failed");
    }

    /// <summary>
    /// Declares per-monitor v2 awareness on Windows and reports what happened.
    /// Call once, before any graphics device or window exists.
    /// </summary>
    public static DpiAwarenessOutcome Apply()
    {
        if (!ShouldAttempt(OperatingSystem.IsWindows()))
        {
            return DescribeOutcome(
                attempted: false,
                succeeded: false,
                win32ErrorCode: 0);
        }

        var succeeded = SetProcessDpiAwarenessContext(PerMonitorAwareV2);
        var errorCode = succeeded ? 0 : Marshal.GetLastWin32Error();
        return DescribeOutcome(attempted: true, succeeded, errorCode);
    }

    // LibraryImport rather than DllImport: the latter raises SYSLIB1054 under
    // this repository's repo-wide TreatWarningsAsErrors, and CLAUDE.md forbids
    // suppressing a warning to get green. The generated stub is why the
    // project sets AllowUnsafeBlocks.
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessDpiAwarenessContext(nint value);
}
