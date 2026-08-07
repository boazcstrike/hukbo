using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Hukbo.Core.Tests")]

// The deadlock probe under tools/ reconstructs the collision resolver's
// resolution order from outside the simulation, which needs the one internal
// pure function that produces it, CollisionPriority.Resolve. The probe is a
// hand-run measurement harness: it is not in Hukbo.slnx, not in the canonical
// gate, and it observes the simulation rather than participating in it. This
// grant adds no behaviour and no hashed state.
[assembly: InternalsVisibleTo("Hukbo.Tools.DeadlockProbe")]

// The cohesion trace under tools/ answers design section 7 of
// docs/archives/2026-08-07/2026-07-28-cohesion-scan-narrowing-design.md: why
// cohesion stops firing partway through a faction's advance. It reconstructs
// the six movement gates from two consecutive agent snapshots, which needs the
// two internal pure predicates that decide them,
// MovementRules.IsCohesionEligible and
// MovementRules.IsCohesionWindowOpen, plus FormationPlanner.MaximumContingents
// for the slot arithmetic. Like the deadlock probe it is a hand-run measurement
// harness: not in Hukbo.slnx, not in the canonical gate, and it observes the
// simulation rather than taking part in it. This grant adds no behaviour and no
// hashed state.
[assembly: InternalsVisibleTo("Hukbo.Tools.CohesionTrace")]
