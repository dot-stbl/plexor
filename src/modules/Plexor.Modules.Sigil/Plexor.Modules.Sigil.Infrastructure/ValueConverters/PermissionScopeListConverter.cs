// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PermissionScopeListValueConverter — explicit ValueConverter class for
// the IReadOnlyList<PermissionScope> ↔ string[] mapping on
// sigil.roles.permissions. Using a named class (instead of the
// anonymous HasConversion overload) sidesteps EF Core 10.0's
// composite-converter resolution bug that combines our converter
// with built-in IEnumerable<string> → string and produces
// "Cannot compose converter from IReadOnlyList<PermissionScope>
// to string[]" at model-build time.
// ==========================================================================

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Plexor.Modules.Sigil.Domain.ValueObjects;

namespace Plexor.Modules.Sigil.Infrastructure.ValueConverters;

/// <summary>
///     Convert <c>IReadOnlyList&lt;PermissionScope&gt;</c> ↔ <c>string[]</c>
///     for the <c>sigil.roles.permissions</c> text[] column. Element
///     conversion delegates to <see cref="PermissionScopeValueConverter" />.
/// </summary>
internal sealed class PermissionScopeListValueConverter
    : ValueConverter<IReadOnlyList<PermissionScope>, string[]>
{
    public PermissionScopeListValueConverter()
        : base(
            convertToProviderExpression: static permissions => permissions
                .Select(static p => p.Value)
                .ToArray(),
            convertFromProviderExpression: static raw => raw
                .Select(static value => new PermissionScope(value))
                .ToArray())
    {
    }
}

/// <summary>
///     Convert single <see cref="PermissionScope" /> ↔ <see cref="string" />.
///     Defined separately so EF's converter resolution doesn't try to
///     build a composite across collection element / string conversions.
/// </summary>
internal sealed class PermissionScopeValueConverter
    : ValueConverter<PermissionScope, string>
{
    public PermissionScopeValueConverter()
        : base(
            convertToProviderExpression: static p => p.Value,
            convertFromProviderExpression: static v => new PermissionScope(v))
    {
    }
}
