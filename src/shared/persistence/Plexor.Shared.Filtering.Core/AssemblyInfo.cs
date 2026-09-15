using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Plexor.Shared.Filtering.Unit")]

// After the Core/Web split, the internals test surface lives entirely in
// Plexor.Shared.Filtering.Core (the Web project has no test project — schema
// transformers are exercised via OpenAPI integration tests, not unit tests).
// Keep this attribute here, not in Web/AssemblyInfo.cs.
