// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AssemblyInfo — only contains the InternalsVisibleTo grant for the
// unit-test assembly. Pure-function helpers (file-static renderers,
// XML builders) stay internal in production code so the public
// surface is the IWorkloadProvider contracts only; tests reach into
// internals to validate snapshot output of renderers.
//
// Adding a new test assembly? Add it here too.
// ============================================================================

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Plexor.NodeAgent.Unit")]
