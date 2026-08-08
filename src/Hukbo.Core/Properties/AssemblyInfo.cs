using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Hukbo.Core.Tests")]

// Hukbo.Headless reads internal metrics accumulators and internal members of
// BattleSimulation to build its RunReport. This grant used to live at the top
// of Determinism/Fnv1a.cs. When that file moved to Hukbo.Shared.Core in the
// tier-1 extraction the attribute travelled with it, because an assembly-level
// attribute is scoped to the assembly it is compiled into and not to the file
// that declares it — so Hukbo.Core silently lost the grant and Hukbo.Headless
// stopped compiling. Declaring it here, beside the other grants, is what stops
// that from recurring the next time a file moves. The grant adds no behaviour
// and no hashed state.
[assembly: InternalsVisibleTo("Hukbo.Headless")]

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
