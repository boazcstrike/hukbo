using Hukbo.Core.Combat;

namespace Hukbo.Core.Movement;

/// <summary>
/// The V10-only weapon-cohort deployment permutation of the battlefield
/// realism design, sections 4.2 through 4.6. Warriors are regrouped across
/// their own army's already-planned formation slots so that each contingent
/// reads as a body dominated by one weapon rather than an even slice of the
/// whole roster, and so that shield-bearing warriors stand on the
/// forward-most slots of whichever contingent they land in. Nothing about
/// the formation itself changes: slot coordinates, the lattice, the jitter,
/// and the SplitMix64 draw count all stay exactly as planned — this is a
/// pure permutation of values the caller already holds, and it never draws.
/// </summary>
/// <remarks>
/// The caller runs this once per faction on the canonical, unmirrored
/// deployment, before the faction-1 X reflection — the same calling
/// discipline <see cref="EquipmentDeploymentAssignment"/> already uses for
/// V6. Unlike that permutation, this one is not confined to a single
/// contingent: a slot's <c>ContingentId</c> travels with it, so moving a
/// warrior onto a different contingent's slot moves the warrior into that
/// contingent, which is exactly what the weapon-grouping ask needs.
/// <para>
/// The assignment runs in two passes. First, every warrior's cohort — the
/// index of its resolved loadout inside <see cref="CombatRuleset.Roster"/>
/// — decides which contingent it lands in: cohorts ranked by member count
/// descending then cohort key ascending are laid end to end and cut into
/// contiguous runs sized to each contingent's own original slot count,
/// contingents themselves ranked by slot count descending then contingent
/// id ascending. Second, inside each contingent, the warriors that landed
/// there are paired against that contingent's own slots, shield-bearing
/// warriors first, against slots ordered by depth — canonical
/// <c>XRaw</c> descending, which is "toward the enemy" for both factions
/// after the faction-1 reflection, then <c>YRaw</c> ascending, then
/// original slot index ascending.
/// </para>
/// <para>
/// Every sort key chain ends in a distinct index — the original slot index
/// for slots, the faction-local warrior index for warriors, the
/// contingent id for contingents — so the unstable
/// <see cref="Array.Sort{T}(T[], Comparison{T})"/> is deterministic here,
/// the identical argument and the identical failure mode
/// <see cref="EquipmentDeploymentAssignment"/> already guards.
/// </para>
/// </remarks>
internal static class CohortDeploymentAssignment
{
    /// <summary>
    /// Computes one faction's slot assignment: element <c>i</c> of the
    /// result is the slot the warrior at faction-local index <c>i</c>
    /// deploys on. The canonical deployment is never mutated.
    /// </summary>
    /// <param name="canonicalDeployment">
    /// The planner's canonical, unmirrored deployment, indexed by
    /// faction-local warrior index.
    /// </param>
    /// <param name="loadoutsByFactionLocalIndex">
    /// This faction's resolved loadout per faction-local warrior index —
    /// resolved the same way the spawn loop resolves it, so the assignment
    /// and the spawned agent can never disagree about a warrior's
    /// equipment.
    /// </param>
    /// <param name="rules">
    /// The combat ruleset the battle runs on; its
    /// <see cref="CombatRuleset.Roster"/> defines cohort identity and
    /// cohort ordering.
    /// </param>
    /// <param name="spreadCohortsLaterally">
    /// When <see langword="false"/>, the run-cut traversal visits
    /// contingent ids ranked by original slot count descending, then
    /// contingent id ascending — the behaviour every registered preset
    /// through <see cref="MovementPresetId.ContingentShapeV12"/> uses, and
    /// the value this parameter must be passed for all of them. When
    /// <see langword="true"/>, the traversal instead visits contingent ids
    /// in lateral-riffle order — even ids ascending, then odd ids
    /// ascending — so that cohort runs ranked adjacent by size land on
    /// non-adjacent lanes instead of collecting at one edge of the army's
    /// frontage (design the cohort lateral spread design
    /// section 3). Only <see cref="MovementPresetId.CohortLateralSpreadV13"/>
    /// passes <see langword="true"/>.
    /// </param>
    internal static (int XRaw, int YRaw, int ContingentId)[] AssignForFaction(
        (int XRaw, int YRaw, int ContingentId)[] canonicalDeployment,
        ReadOnlySpan<CombatLoadout> loadoutsByFactionLocalIndex,
        CombatRuleset rules,
        bool spreadCohortsLaterally)
    {
        ArgumentNullException.ThrowIfNull(canonicalDeployment);
        ArgumentNullException.ThrowIfNull(rules);

        if (loadoutsByFactionLocalIndex.Length != canonicalDeployment.Length)
        {
            throw new ArgumentException(
                $"{loadoutsByFactionLocalIndex.Length} loadouts were " +
                $"supplied for {canonicalDeployment.Length} deployment " +
                "slots; every faction-local warrior index needs exactly " +
                "one resolved loadout.",
                nameof(loadoutsByFactionLocalIndex));
        }

        var warriorCount = canonicalDeployment.Length;
        var rosterCount = rules.Roster.Count;

        // Extracted from the span up front: a ReadOnlySpan cannot be
        // captured by the Array.Sort comparator lambdas below, so every
        // per-warrior value a sort needs is read out into a plain array
        // first, exactly once.
        var cohortKeys = new int[warriorCount];
        var shieldBearing = new bool[warriorCount];
        for (var index = 0; index < warriorCount; index++)
        {
            var loadout = loadoutsByFactionLocalIndex[index];
            cohortKeys[index] = ResolveCohortKey(rules, loadout);
            shieldBearing[index] = loadout.Shield != ShieldId.None;
        }

        var cohortSizes = new int[rosterCount];
        foreach (var key in cohortKeys)
        {
            cohortSizes[key]++;
        }

        var maximumContingentId = -1;
        foreach (var slot in canonicalDeployment)
        {
            maximumContingentId = Math.Max(maximumContingentId, slot.ContingentId);
        }

        var contingentCount = maximumContingentId + 1;
        var contingentSizes = new int[contingentCount];
        foreach (var slot in canonicalDeployment)
        {
            contingentSizes[slot.ContingentId]++;
        }

        var contingentOrder = spreadCohortsLaterally
            ? BuildLateralRiffleOrder(contingentCount)
            : BuildSizeRankedOrder(contingentCount, contingentSizes);

        var cohortOrderedWarriors = new int[warriorCount];
        for (var index = 0; index < warriorCount; index++)
        {
            cohortOrderedWarriors[index] = index;
        }

        Array.Sort(cohortOrderedWarriors, (left, right) =>
        {
            // Cohort member count descending, then cohort key ascending,
            // then faction-local index ascending (design section 4.4).
            var bySize = cohortSizes[cohortKeys[right]]
                .CompareTo(cohortSizes[cohortKeys[left]]);
            if (bySize != 0)
            {
                return bySize;
            }

            var byKey = cohortKeys[left].CompareTo(cohortKeys[right]);
            return byKey != 0 ? byKey : left.CompareTo(right);
        });

        // Cuts the cohort-ordered warrior list into one contiguous run per
        // contingent, each run sized to that contingent's own original slot
        // count, visited in contingent-rank order. A warrior's new
        // contingent id is whichever run it lands in, not the contingent it
        // started in — this is the step that actually groups warriors by
        // weapon across contingent boundaries.
        var newContingentIdByWarrior = new int[warriorCount];
        var cursor = 0;
        foreach (var contingentId in contingentOrder)
        {
            var size = contingentSizes[contingentId];
            for (var member = 0; member < size; member++)
            {
                newContingentIdByWarrior[cohortOrderedWarriors[cursor]] = contingentId;
                cursor++;
            }
        }

        var assigned = new (int XRaw, int YRaw, int ContingentId)[warriorCount];
        for (var contingentId = 0; contingentId < contingentCount; contingentId++)
        {
            AssignWithinContingent(
                canonicalDeployment,
                cohortKeys,
                shieldBearing,
                newContingentIdByWarrior,
                contingentId,
                assigned);
        }

        return assigned;
    }

    /// <summary>
    /// The traversal order every registered preset through
    /// <see cref="MovementPresetId.ContingentShapeV12"/> uses: contingent
    /// ids ranked by original slot count descending, then contingent id
    /// ascending (design section 4.4). Under a planner-produced,
    /// non-increasing size table this makes ascending contingent id exactly
    /// ascending rank, which is the row-58 defect
    /// the cohort lateral spread design section 2
    /// traces; this order is kept, unchanged, for every preset that already
    /// shipped it.
    /// </summary>
    private static int[] BuildSizeRankedOrder(int contingentCount, int[] contingentSizes)
    {
        var order = new int[contingentCount];
        for (var contingent = 0; contingent < contingentCount; contingent++)
        {
            order[contingent] = contingent;
        }

        Array.Sort(order, (left, right) =>
        {
            // Slot count descending, then contingent id ascending (design
            // section 4.4).
            var bySize = contingentSizes[right].CompareTo(contingentSizes[left]);
            return bySize != 0 ? bySize : left.CompareTo(right);
        });

        return order;
    }

    /// <summary>
    /// The <see cref="MovementPresetId.CohortLateralSpreadV13"/> traversal
    /// order: even contingent ids ascending, then odd contingent ids
    /// ascending — for seven contingents, <c>0, 2, 4, 6, 1, 3, 5</c>. This
    /// is a total order over the distinct integers
    /// <c>[0, contingentCount)</c>, so it needs no tie-break and draws no
    /// random numbers (design section 3).
    /// </summary>
    private static int[] BuildLateralRiffleOrder(int contingentCount)
    {
        var order = new int[contingentCount];
        var next = 0;
        for (var contingentId = 0; contingentId < contingentCount; contingentId += 2)
        {
            order[next++] = contingentId;
        }

        for (var contingentId = 1; contingentId < contingentCount; contingentId += 2)
        {
            order[next++] = contingentId;
        }

        return order;
    }

    /// <summary>
    /// Pairs one contingent's newly assigned warriors against that same
    /// contingent's own original slots: shield-bearing warriors sorted
    /// first meet slots sorted by depth, so the forward-most slots of the
    /// contingent go to its shield bearers (design section 4.5).
    /// </summary>
    private static void AssignWithinContingent(
        (int XRaw, int YRaw, int ContingentId)[] canonicalDeployment,
        int[] cohortKeys,
        bool[] shieldBearing,
        int[] newContingentIdByWarrior,
        int contingentId,
        (int XRaw, int YRaw, int ContingentId)[] assigned)
    {
        var slots = CollectSlots(canonicalDeployment, contingentId);
        var warriors = CollectWarriors(newContingentIdByWarrior, contingentId);

        var slotOrder = (int[])slots.Clone();
        Array.Sort(slotOrder, (left, right) =>
        {
            // Depth: canonical XRaw descending — toward the enemy on both
            // sides of the mirror — then YRaw ascending, then original slot
            // index ascending (design section 4.5).
            var byX = canonicalDeployment[right].XRaw
                .CompareTo(canonicalDeployment[left].XRaw);
            if (byX != 0)
            {
                return byX;
            }

            var byY = canonicalDeployment[left].YRaw
                .CompareTo(canonicalDeployment[right].YRaw);
            return byY != 0 ? byY : left.CompareTo(right);
        });

        var warriorOrder = (int[])warriors.Clone();
        Array.Sort(warriorOrder, (left, right) =>
        {
            // Shield-bearing first, then cohort key ascending, then
            // faction-local index ascending (design section 4.5).
            var byShield = ShieldRank(shieldBearing[left])
                .CompareTo(ShieldRank(shieldBearing[right]));
            if (byShield != 0)
            {
                return byShield;
            }

            var byCohort = cohortKeys[left].CompareTo(cohortKeys[right]);
            return byCohort != 0 ? byCohort : left.CompareTo(right);
        });

        for (var position = 0; position < warriorOrder.Length; position++)
        {
            assigned[warriorOrder[position]] = canonicalDeployment[slotOrder[position]];
        }
    }

    /// <summary>
    /// Ranks a warrior for the shield-first ordering: 0 for a loadout that
    /// declares a shield, 1 otherwise, so ascending order puts shield
    /// bearers first.
    /// </summary>
    private static int ShieldRank(bool bearsShield) => bearsShield ? 0 : 1;

    /// <summary>
    /// The original slot indices belonging to one contingent, ascending.
    /// </summary>
    private static int[] CollectSlots(
        (int XRaw, int YRaw, int ContingentId)[] canonicalDeployment,
        int contingentId)
    {
        var count = 0;
        foreach (var slot in canonicalDeployment)
        {
            if (slot.ContingentId == contingentId)
            {
                count++;
            }
        }

        var slots = new int[count];
        var next = 0;
        for (var index = 0; index < canonicalDeployment.Length; index++)
        {
            if (canonicalDeployment[index].ContingentId == contingentId)
            {
                slots[next++] = index;
            }
        }

        return slots;
    }

    /// <summary>
    /// The faction-local warrior indices newly assigned to one contingent,
    /// ascending.
    /// </summary>
    private static int[] CollectWarriors(
        int[] newContingentIdByWarrior,
        int contingentId)
    {
        var count = 0;
        foreach (var value in newContingentIdByWarrior)
        {
            if (value == contingentId)
            {
                count++;
            }
        }

        var warriors = new int[count];
        var next = 0;
        for (var index = 0; index < newContingentIdByWarrior.Length; index++)
        {
            if (newContingentIdByWarrior[index] == contingentId)
            {
                warriors[next++] = index;
            }
        }

        return warriors;
    }

    /// <summary>
    /// The cohort key of one resolved loadout — its position inside
    /// <see cref="CombatRuleset.Roster"/>, found by the first exact match
    /// scanning ascending (design section 4.3). <see cref="CombatLoadout"/>
    /// is a value type with structural equality, and
    /// <see cref="CombatRuleset.ResolveLoadout"/> and the expanded-roster
    /// resolution path both return the stored roster row itself, so the
    /// match is exact rather than approximate.
    /// </summary>
    private static int ResolveCohortKey(CombatRuleset rules, CombatLoadout loadout)
    {
        var roster = rules.Roster;
        for (var row = 0; row < roster.Count; row++)
        {
            if (roster[row] == loadout)
            {
                return row;
            }
        }

        throw new InvalidOperationException(
            $"Loadout {loadout} is not a roster entry of combat preset " +
            $"{rules.Id}; the cohort assignment requires every resolved " +
            "loadout to be a registered roster row.");
    }
}
