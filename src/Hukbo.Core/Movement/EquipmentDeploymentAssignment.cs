using Hukbo.Core.Combat;

namespace Hukbo.Core.Movement;

/// <summary>
/// The V6-only equipment-aware deployment reassignment of the
/// weapon-relative movement design, section 12. Warriors are reassigned to
/// the formation slots the planner already produced, so loadouts whose
/// resolved profiles need more ally clearance open on the roomier existing
/// positions. Nothing about the formation itself changes: contingent
/// membership, slot coordinates, the lattice, the jitter, and the SplitMix64
/// draw count all stay exactly as planned — this is a pure permutation of
/// values the caller already holds, and it never draws.
/// </summary>
/// <remarks>
/// The caller runs this once per faction on the canonical, unmirrored
/// deployment, before the faction-1 X reflection. Faction 1's slots are
/// therefore ranked by their canonical pre-reflection coordinates, so equal
/// faction-local loadout multisets produce the same permutation on both
/// sides and the existing mirror survives exactly; default round-robin
/// rosters, whose faction-local loadout multisets can differ, are not
/// required to mirror. Sorting keys are the design section 12.2 rules: slots
/// by minimum squared distance to another slot of the same contingent
/// descending, then canonical <c>XRaw</c>, <c>YRaw</c>, and original slot
/// index ascending; warriors by resolved
/// <see cref="LoadoutMovementProfile.AllyClearanceBodyDiametersBasisPoints"/>
/// descending, then canonical loadout index and faction-local index
/// ascending. Every tie is broken down to a distinct index, so the unstable
/// <see cref="Array.Sort{T}(T[], Comparison{T})"/> is deterministic here.
/// </remarks>
internal static class EquipmentDeploymentAssignment
{
    /// <summary>
    /// Computes one faction's slot assignment: element <c>i</c> of the
    /// result is the canonical slot the warrior at faction-local index
    /// <c>i</c> deploys on. The canonical deployment itself is never
    /// mutated. A singleton contingent, or one whose warriors all resolve
    /// equal ally clearance, keeps its original faction-local
    /// entity-to-slot mapping — an exact identity, taken directly rather
    /// than recovered from a sort.
    /// </summary>
    /// <param name="canonicalDeployment">
    /// The planner's canonical, unmirrored deployment, indexed by
    /// faction-local warrior index.
    /// </param>
    /// <param name="loadoutsByFactionLocalIndex">
    /// This faction's resolved loadout per faction-local warrior index —
    /// resolved the same way the spawn loop resolves it, so the assignment
    /// and the spawned agent can never disagree about a warrior's equipment.
    /// </param>
    /// <param name="movement">
    /// The movement ruleset the battle runs on; must carry
    /// equipment-relative footwork, because no other preset defines an ally
    /// clearance to rank warriors by.
    /// </param>
    internal static (int XRaw, int YRaw, int ContingentId)[] AssignForFaction(
        (int XRaw, int YRaw, int ContingentId)[] canonicalDeployment,
        ReadOnlySpan<CombatLoadout> loadoutsByFactionLocalIndex,
        MovementRuleset movement)
    {
        ArgumentNullException.ThrowIfNull(canonicalDeployment);
        ArgumentNullException.ThrowIfNull(movement);

        if (!movement.UsesEquipmentRelativeFootwork)
        {
            throw new ArgumentException(
                "Equipment-aware deployment assignment is defined only for " +
                "a preset with equipment-relative footwork; movement preset " +
                $"{movement.Id} does not resolve ally clearances.",
                nameof(movement));
        }

        if (loadoutsByFactionLocalIndex.Length != canonicalDeployment.Length)
        {
            throw new ArgumentException(
                $"{loadoutsByFactionLocalIndex.Length} loadouts were " +
                $"supplied for {canonicalDeployment.Length} deployment " +
                "slots; every faction-local warrior index needs exactly one " +
                "resolved loadout.",
                nameof(loadoutsByFactionLocalIndex));
        }

        var warriorCount = canonicalDeployment.Length;
        var clearances = new int[warriorCount];
        var loadoutRanks = new int[warriorCount];
        var maximumContingentId = -1;

        for (var index = 0; index < warriorCount; index++)
        {
            var profile = movement.ResolveLoadoutProfile(
                loadoutsByFactionLocalIndex[index]);
            clearances[index] = profile.AllyClearanceBodyDiametersBasisPoints;
            loadoutRanks[index] = CanonicalRowIndex(movement, profile);
            maximumContingentId = Math.Max(
                maximumContingentId,
                canonicalDeployment[index].ContingentId);
        }

        // Start from the identity so every skipped contingent keeps its
        // original mapping byte-for-byte, then permute one contingent at a
        // time — membership never changes, so a warrior can only receive a
        // slot that carries its own ContingentId.
        var assigned = new (int XRaw, int YRaw, int ContingentId)[warriorCount];
        Array.Copy(canonicalDeployment, assigned, warriorCount);

        for (var contingent = 0; contingent <= maximumContingentId; contingent++)
        {
            AssignWithinContingent(
                canonicalDeployment,
                clearances,
                loadoutRanks,
                contingent,
                assigned);
        }

        return assigned;
    }

    /// <summary>
    /// Permutes one contingent's warriors across that same contingent's
    /// slots: warriors sorted by ally clearance descending meet slots sorted
    /// by isolation descending, so the roomiest existing position goes to
    /// the equipment that needs the most room.
    /// </summary>
    private static void AssignWithinContingent(
        (int XRaw, int YRaw, int ContingentId)[] canonicalDeployment,
        int[] clearances,
        int[] loadoutRanks,
        int contingent,
        (int XRaw, int YRaw, int ContingentId)[] assigned)
    {
        var members = CollectMembers(canonicalDeployment, contingent);
        if (members.Length <= 1 || AllClearancesEqual(clearances, members))
        {
            // Design 12.2 rule 1: identity, already in place in `assigned`.
            return;
        }

        // Each slot's minimum squared distance to another slot of the same
        // contingent — the isolation the descending slot sort ranks by.
        // Indexed sparsely by faction-local index so the comparison below
        // reads it without an ordinal lookup.
        var isolationBySlot = new long[canonicalDeployment.Length];
        foreach (var slot in members)
        {
            var minimum = long.MaxValue;
            foreach (var other in members)
            {
                if (other == slot)
                {
                    continue;
                }

                minimum = Math.Min(
                    minimum,
                    SquaredSlotDistance(
                        canonicalDeployment[slot],
                        canonicalDeployment[other]));
            }

            isolationBySlot[slot] = minimum;
        }

        var slotOrder = (int[])members.Clone();
        Array.Sort(slotOrder, (left, right) =>
        {
            // Isolation descending, then canonical XRaw, YRaw, and original
            // slot index ascending (design 12.2 step 3).
            var byIsolation =
                isolationBySlot[right].CompareTo(isolationBySlot[left]);
            if (byIsolation != 0)
            {
                return byIsolation;
            }

            var byX = canonicalDeployment[left].XRaw
                .CompareTo(canonicalDeployment[right].XRaw);
            if (byX != 0)
            {
                return byX;
            }

            var byY = canonicalDeployment[left].YRaw
                .CompareTo(canonicalDeployment[right].YRaw);
            return byY != 0 ? byY : left.CompareTo(right);
        });

        var warriorOrder = (int[])members.Clone();
        Array.Sort(warriorOrder, (left, right) =>
        {
            // Ally clearance descending, then canonical loadout index and
            // faction-local index ascending (design 12.2 step 4).
            var byClearance = clearances[right].CompareTo(clearances[left]);
            if (byClearance != 0)
            {
                return byClearance;
            }

            var byLoadout = loadoutRanks[left].CompareTo(loadoutRanks[right]);
            return byLoadout != 0 ? byLoadout : left.CompareTo(right);
        });

        for (var position = 0; position < members.Length; position++)
        {
            assigned[warriorOrder[position]] =
                canonicalDeployment[slotOrder[position]];
        }
    }

    /// <summary>
    /// The faction-local indices belonging to one contingent, ascending —
    /// the shared index set of that contingent's warriors and slots.
    /// </summary>
    private static int[] CollectMembers(
        (int XRaw, int YRaw, int ContingentId)[] canonicalDeployment,
        int contingent)
    {
        var count = 0;
        foreach (var slot in canonicalDeployment)
        {
            if (slot.ContingentId == contingent)
            {
                count++;
            }
        }

        var members = new int[count];
        var next = 0;
        for (var index = 0; index < canonicalDeployment.Length; index++)
        {
            if (canonicalDeployment[index].ContingentId == contingent)
            {
                members[next++] = index;
            }
        }

        return members;
    }

    private static bool AllClearancesEqual(int[] clearances, int[] members)
    {
        var first = clearances[members[0]];
        foreach (var member in members)
        {
            if (clearances[member] != first)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The canonical loadout index — <c>KP</c> 0 through <c>IS</c> 5 — of a
    /// resolved profile row, recovered as the row's position in the
    /// ruleset's stored collection, which construction validated to be in
    /// canonical order. <see cref="MovementRuleset.ResolveLoadoutProfile"/>
    /// returns the stored row itself, so reference identity is exact.
    /// </summary>
    private static int CanonicalRowIndex(
        MovementRuleset movement,
        LoadoutMovementProfile profile)
    {
        var rows = movement.LoadoutMovementProfiles;
        for (var row = 0; row < rows.Length; row++)
        {
            if (ReferenceEquals(rows[row], profile))
            {
                return row;
            }
        }

        throw new InvalidOperationException(
            "The resolved profile row is not a stored row of movement " +
            $"preset {movement.Id}; ResolveLoadoutProfile must return the " +
            "registered instance.");
    }

    /// <summary>
    /// Integer squared distance between two canonical slots. Deployment
    /// coordinates are non-negative raw map coordinates, so each difference
    /// magnitude stays below 2^31 and the checked sum of two squares fits a
    /// <see langword="long"/>.
    /// </summary>
    private static long SquaredSlotDistance(
        (int XRaw, int YRaw, int ContingentId) first,
        (int XRaw, int YRaw, int ContingentId) second)
    {
        var deltaX = (long)first.XRaw - second.XRaw;
        var deltaY = (long)first.YRaw - second.YRaw;
        return checked((deltaX * deltaX) + (deltaY * deltaY));
    }
}
