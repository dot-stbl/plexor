// Pure serializer: builder AST → wire filter DSL string.
// Grammar (from backend FilterQuery.cs EBNF, AND binds tighter than OR):
//   filter   := orExpr
//   orExpr   := andExpr ('|' andExpr)*
//   andExpr  := term (';' term)*
//   term     := '(' orExpr ')' | comparison
//   comparison := field operator value?
//   operator := '==' | '!=' | '~' | '^=' | '$=' | '>=' | '<=' | '>' | '<' | '[]=' | '?' | '!?'
//   value    := '"' ... '"' | bare | functionCall
// This module has zero Kubb/runtime deps — fully unit-testable.

import { OPERATOR_SYMBOL, isNullaryOperator, resolveClrType, type FilterOperatorName } from './types';

/** A single comparison: field + operator + optional value (undefined for null-checks). */
export interface ComparisonTerm {
    readonly kind: 'comparison';
    readonly field: string;
    readonly operator: FilterOperatorName;
    /** Single value for scalar operators; array for `in`. Undefined for `isNull`/`isNotNull`. */
    readonly value?: unknown;
    /** CLR type name from x-filterable — decides quoting. */
    readonly clrType: string;
}

/** A logical combinator joining its children. */
export interface CombineTerm {
    readonly kind: 'combine';
    readonly op: 'and' | 'or';
    readonly children: readonly Term[];
}

/** A parenthesized group. */
export interface GroupTerm {
    readonly kind: 'group';
    readonly inner: Term;
}

export type Term = ComparisonTerm | CombineTerm | GroupTerm;

/**
 * Serializes a value for the wire. Strings are double-quoted with `\` and `"`
 * escaped; everything else (numbers, dates, guids, enum names) is rendered bare
 * via String(). See FilterLexer.ReadQuotedString for the backend escape rules.
 */
function serializeValue(value: unknown, quoted: boolean): string {
    if (Array.isArray(value)) {
        throw new Error('serializeValue: array value must go through serializeInList');
    }
    if (value === null || value === undefined) {
        throw new Error('serializeValue: null/undefined value — filter DSL has no null literal');
    }
    const rendered = typeof value === 'string' ? value : String(value);
    if (!quoted) {
        return rendered;
    }
    const escaped = rendered.replace(/\\/g, '\\\\').replace(/"/g, '\\"');
    return `"${escaped}"`;
}

/** Serializes an `in` list: each member quoted/bare per CLR type, comma-joined. */
function serializeInList(values: readonly unknown[], clrType: string): string {
    if (values.length === 0) {
        throw new Error('serializeInList: empty list — `in` requires at least one value');
    }
    const { quoted } = resolveClrType(clrType);
    return values.map((value) => serializeValue(value, quoted)).join(',');
}

/** Serializes a single comparison. Null-ops emit `field?` / `field!?` with no value. */
function serializeComparison(term: ComparisonTerm): string {
    if (isNullaryOperator(term.operator)) {
        if (term.value !== undefined) {
            throw new Error(
                `serializeComparison: null-check operator '${term.operator}' must not have a value (field ${term.field})`,
            );
        }
        return `${term.field}${OPERATOR_SYMBOL[term.operator]}`;
    }

    if (term.value === undefined) {
        throw new Error(
            `serializeComparison: operator '${term.operator}' requires a value (field ${term.field})`,
        );
    }
    const symbol = OPERATOR_SYMBOL[term.operator];
    if (term.operator === 'in' || term.operator === 'notIn') {
        if (!Array.isArray(term.value)) {
            throw new Error(
                `serializeComparison: operator '${term.operator}' requires an array value for field ${term.field}`,
            );
        }
        return `${term.field}${symbol}${serializeInList(term.value, term.clrType)}`;
    }
    const { quoted } = resolveClrType(term.clrType);
    return `${term.field}${symbol}${serializeValue(term.value, quoted)}`;
}

/** Top-level serializer. Returns the DSL string, or undefined for an empty term set. */
export function serializeFilter(root: Term | undefined): string | undefined {
    if (root === undefined) {
        return undefined;
    }
    const out = serializeTerm(root);
    return out.length === 0 ? undefined : out;
}

/** Renders a term, respecting precedence: andExpr (;) binds tighter than orExpr (|). */
function serializeTerm(term: Term): string {
    switch (term.kind) {
        case 'comparison':
            return serializeComparison(term);
        case 'group':
            return `(${serializeTerm(term.inner)})`;
        case 'combine':
            return serializeCombine(term);
    }
}

function serializeCombine(term: CombineTerm): string {
    const rendered = term.children.map(serializeTerm).filter((child) => child.length > 0);
    if (rendered.length === 0) {
        return '';
    }
    if (rendered.length === 1) {
        return rendered[0];
    }
    const separator = term.op === 'and' ? ';' : '|';
    return rendered.join(separator);
}