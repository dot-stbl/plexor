// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Plexor Installer CLI — `plx` — NativeAOT single-binary installer.
// ============================================================================
// Skeleton placeholder. Real implementation will wire:
//   - System.CommandLine for CLI parsing
//   - Spectre.Console for terminal UI
//   - Provider discovery via Plexor.Core.Providers
//   - Discovery → Resolver → Planner → Applier → Handoff flow
//
// Rule: synchronous top-level Main only. No `await` (VSTHRD200), no
// `.GetAwaiter().GetResult()` (VSTHRD103). The CLI is short-lived
// NativeAOT — async work is dispatched via System.CommandLine handlers
// that own their own async lifecycle (each handler can be async Task
// returning).
// ============================================================================

using Plexor.Installer;

return InstallerCli.Run(args);