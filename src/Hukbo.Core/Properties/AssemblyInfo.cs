using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Hukbo.Core.Tests")]

// The deadlock probe under tools/ reconstructs the collision resolver's
// resolution order from outside the simulation, which needs the one internal
// pure function that produces it, CollisionPriority.Resolve. The probe is a
// hand-run measurement harness: it is not in Hukbo.slnx, not in the canonical
// gate, and it observes the simulation rather than participating in it. This
// grant adds no behaviour and no hashed state.
[assembly: InternalsVisibleTo("Hukbo.Tools.DeadlockProbe")]
