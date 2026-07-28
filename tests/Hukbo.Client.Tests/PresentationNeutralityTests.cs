using Hukbo.Client.Presentation;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

/// <summary>
/// The structural half of VIS-038's proof that the visual improvement
/// package's presentation code cannot influence the simulation.
/// </summary>
/// <remarks>
/// <para>
/// <c>DiagnosticLoggingBoundaryTests.FullTraceLoggingDoesNotChangeTheSimulationResult</c>
/// (<c>tests/Hukbo.Core.Tests/DiagnosticLoggingBoundaryTests.cs</c>) is the
/// logging-neutrality style the visual-system-integration design (section 10,
/// leg 3) points to: it runs the same headless workload twice, once silent
/// and once at full trace, and asserts the two hashes, the outcome, and the
/// event stream never move. That test is possible because the debug logger
/// has a genuine seam into a running simulation — it observes each tick as it
/// completes.
/// </para>
/// <para>
/// Client visual settings (theme, army composition, gore intensity, motion
/// intensity, high contrast) have no equivalent seam. They are never
/// constructed inside, passed into, or read by <c>Hukbo.Core</c>, so a "run
/// the workload twice with settings toggled" test would not exercise
/// anything beyond what the project graph already shows: there is no
/// parameter, field, or call anywhere that could carry a presentation value
/// into the simulation for such a test to catch if it were ever added. R-W6.15
/// only asks for a neutrality test "if practical"; per the design, the
/// structural argument below is the substitute where no practical seam
/// exists, rather than a test that would just restate the type signatures it
/// already sees.
/// </para>
/// <para>The structural argument, in three parts:</para>
/// <list type="number">
/// <item><description><c>src/Hukbo.Core/Hukbo.Core.csproj</c> declares zero
/// <c>ProjectReference</c> entries. No type <c>Hukbo.Core</c> defines can
/// even name a <c>Hukbo.Client</c> or <c>Hukbo.Diagnostics</c> type, so no
/// method signature anywhere in the simulation could accept a settings value
/// even if a future edit tried to pass one in.</description></item>
/// <item><description>Confirmed by assembly inspection below (rather than
/// trusted from the <c>.csproj</c> alone): the compiled
/// <see cref="Scenario"/> assembly carries no reference to
/// <c>Hukbo.Client</c> or <c>Hukbo.Diagnostics</c>, with a positive control
/// proving the check is not vacuous.</description></item>
/// <item><description>The only remaining question is whether presentation
/// code reads something unstable and lets it leak into player-visible
/// output — not the simulation, but a hygiene concern of its own. That is
/// covered separately: <c>SourceHygieneTests</c> scans the package's entire
/// variation surface for <c>System.Random</c>, <c>GetHashCode</c>-based
/// selection, and the wall clock, and scans the narrower variant-selection
/// surface (the catalogs, the fallback resolver, the appearance factory, the
/// salt registry) for <c>Dictionary</c>/<c>HashSet</c> iteration order. The
/// only input to any selection is <c>EntityId</c> / <c>CombatLoadout</c> /
/// <c>Scenario.Seed</c>, mixed through the named salts in
/// <see cref="PresentationSalts"/> — themselves the simulation's own
/// deterministic identifiers, not anything a settings screen can
/// reach.</description></item>
/// </list>
/// </remarks>
public sealed class PresentationNeutralityTests
{
    /// <summary>
    /// The mechanically checkable half of the structural argument: the
    /// compiled simulation assembly cannot name a presentation type. If this
    /// ever fails, someone added a reference from <c>Hukbo.Core</c> to
    /// <c>Hukbo.Client</c> or <c>Hukbo.Diagnostics</c>, and R-X.11 is broken
    /// regardless of what the new reference is used for.
    /// </summary>
    [Fact]
    public void TheCoreAssemblyDoesNotReferenceClientOrDiagnostics()
    {
        var referenced = typeof(Scenario).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("Hukbo.Client", referenced);
        Assert.DoesNotContain("Hukbo.Diagnostics", referenced);
    }

    /// <summary>
    /// The positive control for the assertion above. Without it, deleting
    /// the presentation assembly entirely would leave that test passing and
    /// prove nothing about this run of the suite.
    /// </summary>
    [Fact]
    public void TheClientAssemblyDoesReferenceCore()
    {
        var referenced = typeof(PresentationSalts).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.Contains("Hukbo.Core", referenced);
    }
}
