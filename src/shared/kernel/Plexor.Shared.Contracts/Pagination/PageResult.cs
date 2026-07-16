namespace Plexor.Shared.Contracts.Pagination;

/// <summary>
///     Paged response envelope. Returned by every list endpoint that accepts
///     a <c>FilterQuery</c>. Carries the page slice + the total match count
///     so the client can render pagination controls without a second
///     round-trip.
/// </summary>
/// <typeparam name="T">Row type (typically a read model / summary DTO).</typeparam>
/// <param name="Items">The page slice — never <c>null</c>, may be empty.</param>
/// <param name="Total">
///     Total matching rows across all pages (before
///     pagination).
/// </param>
/// <param name="Page">
///     The page number returned (1-based, mirrors
///     <c>FilterQuery.Page</c>).
/// </param>
/// <param name="PageSize">
///     The page size used (mirrors
///     <c>FilterQuery.PageSize</c>).
/// </param>
public sealed record PageResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);
