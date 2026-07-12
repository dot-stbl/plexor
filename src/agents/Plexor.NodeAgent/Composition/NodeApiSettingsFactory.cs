// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeApiSettingsFactory — builds the Refit settings used by
// AddRefitClient<INodeApi> at DI registration time. v0.1 is a
// trivial pass-through; future versions can add custom JSON
// converters, content serializers, or auth-header providers
// (e.g. an mTLS client certificate) here without touching Program.cs.
// ============================================================================

using Refit;

namespace Plexor.NodeAgent.Composition;

/// <summary>
///     Factory for the <see cref="RefitSettings" /> used when
///     registering <see cref="Infrastructure.INodeApi" /> via
///     <c>AddRefitClient&lt;INodeApi&gt;()</c>.
/// </summary>
public static class NodeApiSettingsFactory
{
    /// <summary>
    ///     Build the default settings. Source-generator
    ///     based, no reflection at runtime, AOT-friendly.
    /// </summary>
    public static RefitSettings Create()
    {
        return new RefitSettings
        {
            CollectionFormat = CollectionFormat.Multi
        };
    }
}
