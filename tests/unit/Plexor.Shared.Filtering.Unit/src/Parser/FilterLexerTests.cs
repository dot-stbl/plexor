using Plexor.Shared.Filtering;
using Shouldly;
using Xunit;

namespace Plexor.Shared.Filtering.Unit.Parser;

/// <summary>
///     Tests for <see cref="FilterLexer" />. The lexer is the first stage
///     of filter DSL parsing — it tokenizes raw input into a stream of
///     <see cref="FilterToken" /> records (identifier, operator, value,
///     separator, paren).
/// </summary>
/// <remarks>
///     The lexer emits an <see cref="FilterTokenKind.EndOfInput" /> sentinel
///     at the end of every token stream, including empty input. Tests
///     count this sentinel as part of <c>tokens.Count</c>.
///     <para>For operator symbols (<c>~</c>, <c>?</c>, <c>!?</c>, etc.),
///     the lexer's <see cref="FilterToken.Text" /> carries the canonical
///     <see cref="FilterOperator" /> enum name (e.g. <c>"Contains"</c>,
///     <c>"IsNull"</c>, <c>"IsNotNull"</c>) — not the raw symbol. The
///     raw symbol is in the input string; the lexer maps to the canonical
///     enum name so the parser doesn't have to.</para>
/// </remarks>
public sealed class FilterLexerTests
{
    [Fact(DisplayName = "Tokenize field-op-number (RHS is Value, not Identifier)")]
    public void Lexer_Tokenize_FieldOpNumber_RhsIsValue()
    {
        // Numeric RHS — IsValidIdentifier('42') == false, so the lexer
        // emits FilterTokenKind.Value. Bare-word RHS like 'John' would be
        // lexed as Identifier (alphanumeric passes IsValidIdentifier).
        var tokens = FilterLexer.Tokenize("name==42");

        // 3 input tokens + 1 EndOfInput sentinel
        tokens.Count.ShouldBe(4);
        tokens[0].Kind.ShouldBe(FilterTokenKind.Identifier);
        tokens[0].Text.ShouldBe("name");
        tokens[1].Kind.ShouldBe(FilterTokenKind.Operator);
        tokens[1].Text.ShouldBe("Eq");  // == -> "Eq"
        tokens[2].Kind.ShouldBe(FilterTokenKind.Value);
        tokens[2].Text.ShouldBe("42");
        tokens[3].Kind.ShouldBe(FilterTokenKind.EndOfInput);
    }

    [Fact(DisplayName = "Tokenize AND chain: name==A;status==Active")]
    public void Lexer_Tokenize_AndChain()
    {
        var tokens = FilterLexer.Tokenize("name==A;status==Active");

        // 7 input tokens + 1 EndOfInput
        tokens.Count.ShouldBe(8);
        tokens[3].Kind.ShouldBe(FilterTokenKind.And);
        tokens[3].Text.ShouldBe(";");
    }

    [Fact(DisplayName = "Tokenize OR chain: name~A|name~B")]
    public void Lexer_Tokenize_OrChain()
    {
        var tokens = FilterLexer.Tokenize("name~A|name~B");

        tokens[3].Kind.ShouldBe(FilterTokenKind.Or);
        tokens[3].Text.ShouldBe("|");
    }

    [Fact(DisplayName = "Tokenize parenthesized group: (a==1|b==2);c==3")]
    public void Lexer_Tokenize_ParenGroup()
    {
        var tokens = FilterLexer.Tokenize("(a==1|b==2);c==3");

        // ( a == 1 | b == 2 ) ; c == 3 EndOfInput
        // (1) a (2) == (3) 1 (4) | (5) b (6) == (7) 2 (8) ) (9) ; (10) c (11) == (12) 3 (13) EndOfInput
        tokens.Count.ShouldBe(14);
        tokens[0].Kind.ShouldBe(FilterTokenKind.OpenParen);
        tokens[^2].Kind.ShouldBe(FilterTokenKind.Value);
        tokens[^1].Kind.ShouldBe(FilterTokenKind.EndOfInput);
    }

    [Fact(DisplayName = "Tokenize double-quoted value with spaces")]
    public void Lexer_Tokenize_QuotedValueWithSpaces()
    {
        var tokens = FilterLexer.Tokenize("name==\"John Doe\"");

        // name, ==, "John Doe" (StringValue, quotes stripped), EndOfInput
        tokens.Count.ShouldBe(4);
        tokens[2].Kind.ShouldBe(FilterTokenKind.StringValue);
        tokens[2].Text.ShouldBe("John Doe");
    }

    [Fact(DisplayName = "Single-quoted value is NOT a string literal (lexer reads raw chars)")]
    public void Lexer_SingleQuotes_NotStringLiteral()
    {
        // Lexer only treats "..." as a StringValue. Single quotes are
        // raw identifier/value characters — useful in DSL extensions but
        // NOT parsed as quoted strings here.
        var tokens = FilterLexer.Tokenize("name=='John Doe'");

        tokens.ShouldNotContain(t => t.Kind == FilterTokenKind.StringValue);
    }

    [Fact(DisplayName = "Tokenize null-check operator: deletedAt?")]
    public void Lexer_Tokenize_NullOperator()
    {
        var tokens = FilterLexer.Tokenize("deletedAt?");

        // deletedAt, ? (Text="IsNull"), EndOfInput
        tokens.Count.ShouldBe(3);
        tokens[0].Kind.ShouldBe(FilterTokenKind.Identifier);
        tokens[1].Kind.ShouldBe(FilterTokenKind.Operator);
        tokens[1].Text.ShouldBe("IsNull");
    }

    [Fact(DisplayName = "Tokenize not-null-check operator: deletedAt!?")]
    public void Lexer_Tokenize_NotNullOperator()
    {
        var tokens = FilterLexer.Tokenize("deletedAt!?");

        tokens.Count.ShouldBe(3);
        tokens[0].Kind.ShouldBe(FilterTokenKind.Identifier);
        tokens[1].Kind.ShouldBe(FilterTokenKind.Operator);
        tokens[1].Text.ShouldBe("IsNotNull");
    }

    [Theory(DisplayName = "Empty / whitespace input returns EndOfInput only")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Lexer_EmptyInput_ReturnsEndOfInput(string raw)
    {
        var tokens = FilterLexer.Tokenize(raw);

        tokens.Count.ShouldBe(1);
        tokens[0].Kind.ShouldBe(FilterTokenKind.EndOfInput);
    }

    [Fact(DisplayName = "Tokenize throws on unbalanced double-quote")]
    public void Lexer_UnbalancedDoubleQuote_Throws()
    {
        Should.Throw<FilterParseException>(
            () => FilterLexer.Tokenize("name==\"unterminated"));
    }

    [Fact(DisplayName = "Each non-end token records source position")]
    public void Lexer_Tokens_CarryPosition()
    {
        var tokens = FilterLexer.Tokenize("name~John");

        tokens[0].Position.ShouldBe(0);
        tokens[1].Position.ShouldBeGreaterThan(tokens[0].Position);
        tokens[2].Position.ShouldBeGreaterThan(tokens[1].Position);
    }
}
