using System.Collections.Immutable;
using Sandata.Core.Determinism;
using Sandata.Core.Rules;

namespace Sandata.Core.Simulation;

/// <summary>
/// The mission-level tick policy: how long a mission may run, and how often
/// the state hash is recorded. Design section 4 requires the state hash to
/// be taken "at a cadence recorded in the mission record" — this type is
/// where that cadence is recorded. <see cref="TickLimit"/> is Sandata's
/// analogue of <c>Hukbo.Core.Simulation.Scenario.TickLimit</c>; Sandata's own
/// tick <em>rate</em> is a ruleset concern
/// (<see cref="SandataRuleset.TickRate"/>), not a per-mission one, so it does
/// not appear here.
/// </summary>
public readonly record struct MissionTickPolicy(int TickLimit, int StateHashCadenceTicks)
{
    /// <summary>
    /// Throws a named exception for the first invalid field found. Called
    /// from <see cref="Mission"/>'s constructor; never called implicitly.
    /// </summary>
    internal void Validate()
    {
        if (TickLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TickLimit),
                TickLimit,
                "Tick limit must be positive.");
        }

        if (StateHashCadenceTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(StateHashCadenceTicks),
                StateHashCadenceTicks,
                "State hash cadence must be positive.");
        }

        if (StateHashCadenceTicks > TickLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(StateHashCadenceTicks),
                StateHashCadenceTicks,
                "State hash cadence must not exceed the tick limit.");
        }
    }
}

/// <summary>
/// One faction's placement configuration for a <see cref="Mission"/>.
/// <see cref="FactionId"/> is 0 or 1 — Sandata ships exactly two factions in
/// v0.1, matching design section 1's two-faction firefight. The actual spawn
/// positions come from the map's <c>SPAWN</c> records at load time (a later
/// task); this record only carries the count the loader is expected to
/// place, so <see cref="Mission"/> stays free of any map-loading concern.
/// </summary>
public readonly record struct MissionFactionSetup(int FactionId, int OperatorCount)
{
    /// <summary>
    /// Throws a named exception for the first invalid field found. Called
    /// from <see cref="Mission"/>'s constructor; never called implicitly.
    /// </summary>
    internal void Validate()
    {
        if (FactionId != 0 && FactionId != 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FactionId),
                FactionId,
                "Faction id must be 0 or 1.");
        }

        if (OperatorCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(OperatorCount),
                OperatorCount,
                "Operator count must be positive.");
        }
    }
}

/// <summary>
/// Sandata's <c>Scenario</c> equivalent: the immutable configuration one
/// mission is fought under. Format version, seed, a reference to the map the
/// mission runs on, the tick policy, two faction setups, and the ruleset id
/// all fold into <see cref="MissionContentHash"/> in that fixed order, so
/// changing any one of them, or their order, is a new preset version with new
/// golden expectations — <c>CLAUDE.md</c> section 5 and design section 4.
/// </summary>
/// <remarks>
/// <see cref="MapContentHash"/> is the map's own canonicalised content hash
/// (design section 4's map canonicalisation task computes it), not a
/// filesystem path — <c>Sandata.Core</c> may not touch the filesystem, and a
/// path is not a stable cross-machine identifier in any case. The layer that
/// loads a <c>.hkmap</c> file compares its computed hash against this field
/// and rejects a mismatch before the mission starts. This is a design gap
/// filled by the implementer: design section 4 names "map reference" without
/// pinning its type, and task 15 (map canonicalisation) is a same-wave
/// sibling task not visible from this worktree, so this is the one design
/// decision in this file that a later integration pass should confirm
/// against task 15's actual <c>MapContentHash</c> type once merged.
/// </remarks>
public sealed class Mission
{
    /// <summary>
    /// The only format version this build understands. Bumping it is a new
    /// preset version with new golden expectations, exactly like every other
    /// value that folds into <see cref="MissionContentHash"/>.
    /// </summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>Sandata ships exactly two factions in v0.1.</summary>
    public const int FactionCount = 2;

    public Mission(
        int formatVersion,
        ulong seed,
        ulong mapContentHash,
        MissionTickPolicy tickPolicy,
        ImmutableArray<MissionFactionSetup> factionSetups,
        SandataPresetId rulesetId)
    {
        if (formatVersion != CurrentFormatVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(formatVersion),
                formatVersion,
                $"Format version must equal {CurrentFormatVersion}.");
        }

        tickPolicy.Validate();

        if (factionSetups.IsDefaultOrEmpty || factionSetups.Length != FactionCount)
        {
            throw new ArgumentException(
                $"Exactly {FactionCount} faction setups are required.",
                nameof(factionSetups));
        }

        var sawFaction0 = false;
        var sawFaction1 = false;
        foreach (var setup in factionSetups)
        {
            setup.Validate();
            if (setup.FactionId == 0)
            {
                sawFaction0 = true;
            }
            else
            {
                sawFaction1 = true;
            }
        }

        if (!sawFaction0 || !sawFaction1)
        {
            throw new ArgumentException(
                "Faction setups must cover exactly faction 0 and faction 1, once each.",
                nameof(factionSetups));
        }

        if (!Enum.IsDefined(rulesetId))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rulesetId),
                rulesetId,
                "Ruleset id must be a defined SandataPresetId member.");
        }

        FormatVersion = formatVersion;
        Seed = seed;
        MapContentHash = mapContentHash;
        TickPolicy = tickPolicy;

        // Stored in ascending FactionId order so every consumer -- including
        // ComputeContentHash below -- sees a total, caller-order-independent
        // sequence. Safe to index [0]/[1] directly: length and coverage were
        // just validated above.
        FactionSetups = factionSetups[0].FactionId <= factionSetups[1].FactionId
            ? factionSetups
            : ImmutableArray.Create(factionSetups[1], factionSetups[0]);

        RulesetId = rulesetId;
        MissionContentHash = ComputeContentHash();
    }

    public int FormatVersion { get; }

    public ulong Seed { get; }

    /// <summary>
    /// The expected <c>MapContentHash</c> of the map this mission runs on.
    /// See the remarks on <see cref="Mission"/> for why this is a hash and
    /// not a path.
    /// </summary>
    public ulong MapContentHash { get; }

    public MissionTickPolicy TickPolicy { get; }

    /// <summary>Exactly two entries, ordered ascending by <c>FactionId</c>.</summary>
    public ImmutableArray<MissionFactionSetup> FactionSetups { get; }

    public SandataPresetId RulesetId { get; }

    /// <summary>
    /// FNV-1a over every field above, folded through <see cref="SandataHash"/>
    /// in the fixed declaration order: <see cref="FormatVersion"/>,
    /// <see cref="Seed"/>, <see cref="MapContentHash"/>,
    /// <see cref="TickPolicy"/>'s two fields, each <see cref="FactionSetups"/>
    /// entry's two fields in ascending <c>FactionId</c> order, then
    /// <see cref="RulesetId"/>. <see cref="Determinism.SandataStateHasher"/>
    /// folds this single rolled-up value rather than re-folding every field
    /// individually, per design section 4's explicit "MissionContentHash and
    /// SandataRuleset.ContentHash" line.
    /// </summary>
    public ulong MissionContentHash { get; }

    private ulong ComputeContentHash()
    {
        var hash = SandataHash.Begin();
        SandataHash.Fold(ref hash, FormatVersion);
        SandataHash.Fold(ref hash, unchecked((long)Seed));
        SandataHash.Fold(ref hash, unchecked((long)MapContentHash));
        SandataHash.Fold(ref hash, TickPolicy.TickLimit);
        SandataHash.Fold(ref hash, TickPolicy.StateHashCadenceTicks);
        foreach (var setup in FactionSetups)
        {
            SandataHash.Fold(ref hash, setup.FactionId);
            SandataHash.Fold(ref hash, setup.OperatorCount);
        }

        SandataHash.Fold(ref hash, (long)RulesetId);
        return hash;
    }
}
