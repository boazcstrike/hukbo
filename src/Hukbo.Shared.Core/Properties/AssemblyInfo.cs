using System.Runtime.CompilerServices;

// Fnv1a and FixedPoint.IntegerSquareRoot are internal. No member's
// accessibility is widened to public here; these four grants are the only
// consumers that need it, matching the four projects the design records:
// Hukbo.Core (the original owner), its test project, and the two new
// Sandata.Core projects that will need the same primitives.
[assembly: InternalsVisibleTo("Hukbo.Core")]
[assembly: InternalsVisibleTo("Hukbo.Core.Tests")]
[assembly: InternalsVisibleTo("Sandata.Core")]
[assembly: InternalsVisibleTo("Sandata.Core.Tests")]
