// Exports the serializer source as a string literal so the plugin can emit it
// into generated/filters/_runtime.ts. Keeping the runtime inline (rather than
// importing from the plugin package) makes generated/ self-contained — no
// runtime dep on @plexor/kubb-plugin-filter from app code.
//
// The string below MUST stay byte-identical to src/serializer.ts semantics.
// It is the only copy of the wire-format logic that ships to generated/.
export const SERIALIZER_SOURCE = `// Inline runtime emitted by @plexor/kubb-plugin-filter.
// Mirrors tooling/codegen/kubb-plugin-filter/src/serializer.ts — do not edit here.

export type FilterOperatorName =
    | 'eq' | 'notEq' | 'contains' | 'startsWith' | 'endsWith'
    | 'gt' | 'gte' | 'lt' | 'lte' | 'in' | 'notIn'
    | 'iContains' | 'iStartsWith' | 'iEndsWith'
    | 'isNull' | 'isNotNull';

export interface ComparisonTerm {
    readonly kind: 'comparison';
    readonly field: string;
    readonly operator: FilterOperatorName;
    readonly value?: unknown;
    readonly clrType: string;
}

export interface CombineTerm {
    readonly kind: 'combine';
    readonly op: 'and' | 'or';
    readonly children: readonly Term[];
}

export interface GroupTerm {
    readonly kind: 'group';
    readonly inner: Term;
}

export type Term = ComparisonTerm | CombineTerm | GroupTerm;

const OPERATOR_SYMBOL: Readonly<Record<FilterOperatorName, string>> = {
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
    iContains: '~*',
    iStartsWith: '^=*',
    iEndsWith: '$=*',
    isNull: '?',
    isNotNull: '!?',
};

const NULLARY_OPERATORS: ReadonlySet<FilterOperatorName> = new Set(['isNull', 'isNotNull']);

const CLR_TYPE_QUOTED: Readonly<Record<string, boolean>> = {
    String: true, Char: true,
    Guid: false, Int16: false, Int32: false, Int64: false, UInt32: false,
    Decimal: false, Double: false, Single: false, Boolean: false,
    DateTimeOffset: false, DateTime: false, DateOnly: false, TimeOnly: false, TimeSpan: false,
};

function isQuoted(clrType: string): boolean {
    return CLR_TYPE_QUOTED[clrType] ?? true;
}

function isNullary(op: FilterOperatorName): boolean {
    return NULLARY_OPERATORS.has(op);
}

function serializeValue(value: unknown, quoted: boolean): string {
    if (Array.isArray(value)) throw new Error('serializeValue: array value must go through serializeInList');
    if (value === null || value === undefined) throw new Error('serializeValue: null/undefined value');
    const rendered = typeof value === 'string' ? value : String(value);
    if (!quoted) return rendered;
    const escaped = rendered.replace(/\\\\/g, '\\\\\\\\').replace(/"/g, '\\\\"');
    return '"' + escaped + '"';
}

function serializeInList(values: readonly unknown[], clrType: string): string {
    if (values.length === 0) throw new Error('serializeInList: empty list');
    const quoted = isQuoted(clrType);
    return values.map((v) => serializeValue(v, quoted)).join(',');
}

function serializeComparison(term: ComparisonTerm): string {
    if (isNullary(term.operator)) {
        if (term.value !== undefined) {
            throw new Error('null-check operator ' + term.operator + ' must not have a value');
        }
        return term.field + OPERATOR_SYMBOL[term.operator];
    }
    if (term.value === undefined) {
        throw new Error('operator ' + term.operator + ' requires a value for ' + term.field);
    }
    const symbol = OPERATOR_SYMBOL[term.operator];
    if (term.operator === 'in' || term.operator === 'notIn') {
        if (!Array.isArray(term.value)) throw new Error(term.operator + ' requires array for ' + term.field);
        return term.field + symbol + serializeInList(term.value, term.clrType);
    }
    return term.field + symbol + serializeValue(term.value, isQuoted(term.clrType));
}

function serializeTerm(term: Term): string {
    switch (term.kind) {
        case 'comparison': return serializeComparison(term);
        case 'group': return '(' + serializeTerm(term.inner) + ')';
        case 'combine': return serializeCombine(term);
    }
}

function serializeCombine(term: CombineTerm): string {
    const rendered = term.children.map(serializeTerm).filter((c) => c.length > 0);
    if (rendered.length === 0) return '';
    if (rendered.length === 1) return rendered[0]!;
    return rendered.join(term.op === 'and' ? ';' : '|');
}

export function serializeFilter(root: Term | undefined): string | undefined {
    if (root === undefined) return undefined;
    const out = serializeTerm(root);
    return out.length === 0 ? undefined : out;
}

// now(offset) server-side function helper. The lexer treats the identifier(...)
// pattern as a function call; we just emit it as a value string for date fields.
// Caller is responsible for passing a valid duration token like '-7d' or '1h'.
export function now(duration: string): string {
    return 'now(' + duration + ')';
}
`;