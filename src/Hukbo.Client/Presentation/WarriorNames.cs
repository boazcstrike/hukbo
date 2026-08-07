using Hukbo.Client.Presentation.Catalogs;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Gives a generated warrior a personal name for the battle report and the
/// agent inspector to show.
/// </summary>
/// <remarks>
/// <para>
/// Presentation-only, on the same footing as <see cref="PawnAppearance"/>: a
/// name is derived here, in the client, from identity the simulation already
/// published — the warrior's <c>EntityId</c>, its <c>FactionId</c>, and the
/// match's <c>Scenario.Seed</c>. Nothing is written back. <c>Hukbo.Core</c>
/// neither stores a name nor can name a type that holds one, so a name cannot
/// reach the state hash, the event hash, or any targeting, damage, or victory
/// decision.
/// </para>
/// <para>
/// Because the derivation is a pure function of those three values, the same
/// seed and the same build always produce the same names: a replay names the
/// same warrior the same way, and a name is stable across a pause, a resume,
/// and a camera move.
/// </para>
/// <para>
/// The corpus, the exclusions, and the evidence rules behind every form live
/// in <see cref="WarriorNameCatalog"/> and in
/// docs/names/HISTORICAL_1500s_PERSONAL_NAMES.md.
/// </para>
/// </remarks>
internal static class WarriorNames
{
    /// <summary>
    /// The name for one warrior: the faction's region assignment, then that
    /// region's pool indexed by the warrior's own entity identifier.
    /// </summary>
    internal static WarriorNameEntry Resolve(
        ulong entityId,
        int factionId,
        ulong scenarioSeed)
    {
        var region = WarriorNameCatalog.SelectRegion(scenarioSeed, factionId);
        return WarriorNameCatalog.SelectName(entityId, region);
    }

    /// <summary>
    /// A warrior as a report or highlight line names one: the personal name,
    /// then the entity identifier. The identifier is kept, not replaced,
    /// because the name pools are far smaller than a roster and two warriors
    /// in one faction may honestly share a name — the identifier is what tells
    /// them apart, and it is also what the event-log filter matches on.
    /// </summary>
    internal static string FormatWarrior(
        ulong entityId,
        int factionId,
        ulong scenarioSeed) =>
        $"{Resolve(entityId, factionId, scenarioSeed).DisplayForm} #{entityId}";
}
