// Shared types for the filter-builder codegen.
// The wire DSL is defined by the backend (Hybrid.Shared.Filtering:
// FilterOperator.cs + FilterLexer.cs). These constants MUST stay in lockstep
// with those — the generated builder serializes to the exact grammar the
// backend FilterExpressionParser<T> accepts.

/**
 * Operator names as they appear in the x-filterable extension (lowercase).
 *
 * Mirrors Hybrid.Shared.Filtering.FilterOperator enum values.
 */
export type FilterOperatorName =
    | 'eq'
    | 'notEq'
    | 'contains'
    | 'startsWith'
    | 'endsWith'
    | 'gt'
    | 'gte'
    | 'lt'
    | 'lte'
    | 'in'
    | 'notIn'
    | 'iContains'
    | 'iStartsWith'
    | 'iEndsWith'
    | 'isNull'
    | 'isNotNull';

/** Maps each operator name to the backend DSL symbol literal. @see FilterOperator.cs */
export const OPERATOR_SYMBOL: Readonly<Record<FilterOperatorName, string>> = {
    eq: '==',
    notEq: '!=',
    contains: '~',
    startsWith: '^=',
    endsWith: '$=',
    gt: '>',
    gte: '>=',
    lt: '<',
    lte: '<=',
    in: '[]=',
    notIn: '![]=',
    // Case-insensitive variants (PG-style symbols). Value is case-folded by the
    // backend via ToLower() on both sides (string fields only).
    iContains: '~*',
    iStartsWith: '^=*',
    iEndsWith: '$=*',
    // IsNull / IsNotNull take no value — symbol is a suffix, not an infix operator.
    // The serializer emits them as a standalone predicate: `field?` / `field!?`.
    isNull: '?',
    isNotNull: '!?',
};

/** Operators that take NO value (null-check predicates). */
export const NULLARY_OPERATORS: ReadonlySet<FilterOperatorName> = new Set(['isNull', 'isNotNull']);

/** Type guard: is this operator a null-check (takes no value)? */
export function isNullaryOperator(op: FilterOperatorName): boolean {
    return NULLARY_OPERATORS.has(op);
}

/** CLR type name → TS mapping. */
export interface ClrTypeMapping {
    readonly tsType: string;
    /** true → values are double-quoted on the wire (Strings). false → bare (numbers, dates, guids, enums). */
    readonly quoted: boolean;
}

/** CLR type name (as emitted in x-filterable.fields[].type) → TS mapping. Unknown names default to string/quoted (enums). */
export const CLR_TYPE_MAP: Readonly<Record<string, ClrTypeMapping>> = {
    String: { tsType: 'string', quoted: true },
    Char: { tsType: 'string', quoted: true },
    Guid: { tsType: 'string', quoted: false },
    Int16: { tsType: 'number', quoted: false },
    Int32: { tsType: 'number', quoted: false },
    Int64: { tsType: 'number', quoted: false },
    UInt32: { tsType: 'number', quoted: false },
    Decimal: { tsType: 'number', quoted: false },
    Double: { tsType: 'number', quoted: false },
    Single: { tsType: 'number', quoted: false },
    Boolean: { tsType: 'boolean', quoted: false },
    DateTimeOffset: { tsType: 'string', quoted: false },
    DateTime: { tsType: 'string', quoted: false },
    DateOnly: { tsType: 'string', quoted: false },
    TimeOnly: { tsType: 'string', quoted: false },
    TimeSpan: { tsType: 'string', quoted: false },
};

/** Returns the CLR→TS mapping for a type name; unknown names default to {string, quoted}. */
export function resolveClrType(clrTypeName: string): ClrTypeMapping {
    return CLR_TYPE_MAP[clrTypeName] ?? { tsType: 'string', quoted: true };
}

/** Shape of a field in the x-filterable extension. */
export interface FilterableField {
    readonly name: string;
    readonly type: string;
    readonly operators: readonly FilterOperatorName[];
}

/** Shape of the x-filterable extension on a schema. */
export interface FilterableExtension {
    readonly fields: readonly FilterableField[];
}

/** All operators that take a single (scalar) value — everything except `in`/`notIn`, `isNull`, `isNotNull`. */
export const SCALAR_OPERATORS: readonly FilterOperatorName[] = [
    'eq',
    'notEq',
    'contains',
    'startsWith',
    'endsWith',
    'gt',
    'gte',
    'lt',
    'lte',
    'iContains',
    'iStartsWith',
    'iEndsWith',
];

/** List-value operators (`field[]=...` / `field![]=...`). */
export const LIST_OPERATORS: readonly FilterOperatorName[] = ['in', 'notIn'];

/**
 * Date-like CLR types that accept the `now(offset)` server-side function.
 * Mirrors FilterFunctions.EvaluateNow's field-type validation.
 */
export const DATE_CLR_TYPES: ReadonlySet<string> = new Set([
    'DateTimeOffset',
    'DateTime',
]);

/** True if the CLR type is date-like and may receive `now(offset)` values. */
export function isDateClrType(clrType: string): boolean {
    return DATE_CLR_TYPES.has(clrType);
}
