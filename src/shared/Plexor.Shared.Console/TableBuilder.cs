// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// TableBuilder — fluent wrapper over Spectre.Console.Table that applies
// Plexor DS defaults (compact borders, muted headers, accent selected
// row) and gives call sites a builder API instead of the imperative
// table.AddColumn() chain.
//
// Usage:
//
//     var tbl = new TableBuilder()
//         .AddColumn("NAME", align: ColumnAlign.Left)
//         .AddColumn("STATE", align: ColumnAlign.Center)
//         .AddColumn("CPU", align: ColumnAlign.Right)
//         .Build();
//     tbl.AddRow("vm-prod-01", "running", "18%");
//     AnsiConsole.Write(tbl);
// ============================================================================

using Spectre.Console;

namespace Plexor.Shared.Console;

/// <summary>
///     Horizontal alignment of a column. Mirrors Spectre's enum but
///     exposes a short name so the fluent API reads cleaner.
/// </summary>
public enum ColumnAlign
{
    /// <summary>Left edge.</summary>
    Left = 0,

    /// <summary>Centered.</summary>
    Center = 1,

    /// <summary>Right edge (numeric columns).</summary>
    Right = 2
}

/// <summary>
///     Fluent builder for <see cref="Spectre.Console.Table" />. Each
///     <see cref="AddColumn" /> returns the same builder so call sites
///     can chain them. <see cref="Build" /> returns a fully-configured
///     <see cref="Table" /> ready to receive rows and render.
/// </summary>
public sealed class TableBuilder
{
    private readonly Table table = new();

    /// <summary>
    ///     Create a new builder with Plexor DS default borders
    ///     and header style applied.
    /// </summary>
    public TableBuilder()
    {
        _ = table.Border(TableBorder.Rounded);
        _ = table.BorderColor(ColorPalette.Muted);
        _ = table.Expand();
    }

    /// <summary>
    ///     Add a column. <paramref name="align" /> defaults to
    ///     left; numeric columns pass <see cref="ColumnAlign.Right" />
    ///     for the conventional alignment.
    /// </summary>
    /// <param name="header"></param>
    /// <param name="align"></param>
    public TableBuilder AddColumn(string header, ColumnAlign align = ColumnAlign.Left)
    {
        var col = new TableColumn(MarkupExtensions.B(header));
        col.Alignment(align switch
        {
            ColumnAlign.Left => Justify.Left,
            ColumnAlign.Center => Justify.Center,
            ColumnAlign.Right => Justify.Right,
            _ => Justify.Left
        });

        _ = table.AddColumn(col);
        return this;
    }

    /// <summary>
    ///     Set the title that renders above the table. Useful
    ///     for "what is this listing" headers.
    /// </summary>
    /// <param name="title"></param>
    public TableBuilder WithTitle(string title)
    {
        table.Title = new TableTitle(MarkupExtensions.Accent(title));
        return this;
    }

    /// <summary>
    ///     Build the configured table. The caller is responsible
    ///     for adding rows and rendering.
    /// </summary>
    public Table Build()
    {
        return table;
    }
}
