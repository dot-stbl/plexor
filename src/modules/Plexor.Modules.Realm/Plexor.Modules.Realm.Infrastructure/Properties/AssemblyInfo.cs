// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AssemblyInfo — only contains the InternalsVisibleTo grant for the
// unit-test project. The realm auth-providers EF seeder is
// `internal sealed` by convention; this grant lets
// Plexor.Modules.Realm.Unit exercise it directly.
// ============================================================================

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Plexor.Modules.Realm.Unit")]
