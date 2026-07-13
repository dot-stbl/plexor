using Shouldly;
using Xunit;

namespace Plexor.Shared.Filtering.Unit.Parser;

/// <summary>
///     Tests for <see cref="FilterParser" /> — the high-level recursive-descent
///     parser that turns a token stream into a <see cref="FilterNode" /> tree.
///     Complements the lexer tests: the lexer produces tokens, the parser
///     consumes them into AST.
/// </summary>
public sealed class FilterParserTests
{
    [Fact(DisplayName = "Empty / whitespace input returns null")]
    public void Parse_EmptyInput_ReturnsNull()
    {
        FilterParser.Parse(null).ShouldBeNull();
        FilterParser.Parse("").ShouldBeNull();
        FilterParser.Parse("   ").ShouldBeNull();
    }

    [Fact(DisplayName = "Single comparison builds a ComparisonNode")]
    public void Parse_SingleComparison()
    {
        var tree = FilterParser.Parse("name==Apple");

        var cmp = tree.ShouldBeOfType<ComparisonNode>();
        cmp.Field.ShouldBe("name");
        cmp.Operator.ShouldBe(FilterOperator.Eq);
        var scalar = cmp.Value.ShouldBeOfType<ScalarValue>();
        scalar.Raw.ShouldBe("Apple");
    }

    [Fact(DisplayName = "AND chain (a ; b ; c)")]
    public void Parse_AndChain()
    {
        var tree = FilterParser.Parse("a==1;b==2;c==3");

        var and = tree.ShouldBeOfType<AndNode>();
        and.Children.Length.ShouldBe(3);
        ((ComparisonNode)and.Children[0]).Field.ShouldBe("a");
        ((ComparisonNode)and.Children[1]).Field.ShouldBe("b");
        ((ComparisonNode)and.Children[2]).Field.ShouldBe("c");
    }

    [Fact(DisplayName = "OR chain (a | b | c)")]
    public void Parse_OrChain()
    {
        var tree = FilterParser.Parse("a==1|b==2|c==3");

        var or = tree.ShouldBeOfType<OrNode>();
        or.Children.Length.ShouldBe(3);
    }

    [Fact(DisplayName = "AND binds tighter than OR — a | b ; c | d")]
    public void Parse_OperatorPrecedence()
    {
        // a | b ; c | d  =>  (b ; c) grouped inside an OR
        // Grammar:  or := and ('|' and)*
        //          and := term (';' term)*
        // So b ; c is one AND, then the whole thing is an OR of 3 operands:
        // (a) | (b ; c) | (d)
        var tree = FilterParser.Parse("a==1|b==2;c==3|d==4");

        var or = tree.ShouldBeOfType<OrNode>();
        or.Children.Length.ShouldBe(3);
        or.Children[0].ShouldBeOfType<ComparisonNode>();
        or.Children[1].ShouldBeOfType<AndNode>();
        or.Children[2].ShouldBeOfType<ComparisonNode>();
    }

    [Fact(DisplayName = "Parenthesised group is one term")]
    public void Parse_ParenGroup()
    {
        var tree = FilterParser.Parse("(a==1|b==2);c==3");

        var and = tree.ShouldBeOfType<AndNode>();
        and.Children.Length.ShouldBe(2);
        and.Children[0].ShouldBeOfType<OrNode>();
        and.Children[1].ShouldBeOfType<ComparisonNode>();
    }

    [Fact(DisplayName = "Nested parens at three levels")]
    public void Parse_NestedParens()
    {
        var tree = FilterParser.Parse("((a==1))");

        var cmp = tree.ShouldBeOfType<ComparisonNode>();
        cmp.Field.ShouldBe("a");
    }

    [Fact(DisplayName = "Function call (now(-7d)) builds FunctionValue")]
    public void Parse_FunctionCall()
    {
        var tree = FilterParser.Parse("createdAt>=now(-7d)");

        var cmp = tree.ShouldBeOfType<ComparisonNode>();
        cmp.Operator.ShouldBe(FilterOperator.Gte);
        var fn = cmp.Value.ShouldBeOfType<FunctionValue>();
        fn.Name.ShouldBe("now");
        fn.Argument.ShouldBe("-7d");
        cmp.ValueKind.ShouldBe(FilterValueKind.FunctionCall);
    }

    [Fact(DisplayName = "IN-list builds ListValue with ImmutableArray")]
    public void Parse_InList()
    {
        var tree = FilterParser.Parse("id[]=a,b,c");

        var cmp = tree.ShouldBeOfType<ComparisonNode>();
        cmp.Operator.ShouldBe(FilterOperator.In);
        var list = cmp.Value.ShouldBeOfType<ListValue>();
        list.Items.Length.ShouldBe(3);
        list.Items[0].ShouldBe("a");
        list.Items[1].ShouldBe("b");
        list.Items[2].ShouldBe("c");
    }

    [Fact(DisplayName = "IN-list with quoted items")]
    public void Parse_InList_QuotedItems()
    {
        var tree = FilterParser.Parse("tags[]=\"a b\",\"c d\"");

        var cmp = tree.ShouldBeOfType<ComparisonNode>();
        var list = cmp.Value.ShouldBeOfType<ListValue>();
        list.Items[0].ShouldBe("a b");   // quotes stripped
        list.Items[1].ShouldBe("c d");
    }

    [Fact(DisplayName = "Null check (? operator) builds NullValue")]
    public void Parse_NullCheck()
    {
        var tree = FilterParser.Parse("deletedAt?");

        var cmp = tree.ShouldBeOfType<ComparisonNode>();
        cmp.Operator.ShouldBe(FilterOperator.IsNull);
        cmp.Value.ShouldBeSameAs(NullValue.Instance);
    }

    [Fact(DisplayName = "Not-null check (!? operator) builds NullValue")]
    public void Parse_NotNullCheck()
    {
        var tree = FilterParser.Parse("email!?");

        var cmp = tree.ShouldBeOfType<ComparisonNode>();
        cmp.Operator.ShouldBe(FilterOperator.IsNotNull);
    }

    [Fact(DisplayName = "Bare numeric value is FilterValueKind.Value (not Identifier)")]
    public void Parse_NumericValue()
    {
        var tree = FilterParser.Parse("age>=18");

        var cmp = tree.ShouldBeOfType<ComparisonNode>();
        var scalar = cmp.Value.ShouldBeOfType<ScalarValue>();
        scalar.Raw.ShouldBe("18");
        cmp.ValueKind.ShouldBe(FilterValueKind.Scalar);
    }

    [Fact(DisplayName = "Bare identifier value is FilterValueKind.Scalar (not Identifier)")]
    public void Parse_IdentifierValue()
    {
        // After an operator, a bare alphanumeric token is the value, not a
        // field name. So 'name==Status' means name equals the value "Status"
        // (treated as a string / enum depending on the field's CLR type).
        var tree = FilterParser.Parse("name==Status");

        var cmp = tree.ShouldBeOfType<ComparisonNode>();
        cmp.Field.ShouldBe("name");
        var scalar = cmp.Value.ShouldBeOfType<ScalarValue>();
        scalar.Raw.ShouldBe("Status");
    }

    [Fact(DisplayName = "Longest-prefix operator wins (>= vs >)")]
    public void Parse_LongestPrefixOperator()
    {
        // If lexer only matched single char, '>=' would split into '>' + '='.
        // Lexer uses longest-prefix first, so '>=' stays as one Operator token.
        var tree = FilterParser.Parse("count>=10");

        var cmp = tree.ShouldBeOfType<ComparisonNode>();
        cmp.Operator.ShouldBe(FilterOperator.Gte);
    }

    [Fact(DisplayName = "Unmatched closing paren throws FilterParseException")]
    public void Parse_UnmatchedCloseParen_Throws()
    {
        Should.Throw<FilterParseException>(static () => FilterParser.Parse("name==App)"));
    }

    [Fact(DisplayName = "Unclosed paren throws")]
    public void Parse_UnclosedOpenParen_Throws()
    {
        Should.Throw<FilterParseException>(static () => FilterParser.Parse("(name==App"));
    }

    [Fact(DisplayName = "Trailing junk after root expression throws")]
    public void Parse_TrailingToken_Throws()
    {
        Should.Throw<FilterParseException>(
            static () => FilterParser.Parse("name==App junk"));
    }
}
