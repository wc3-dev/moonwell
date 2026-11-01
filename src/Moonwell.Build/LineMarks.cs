using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Moonwell.Build
{
    /// <summary>
    /// Puts a location marker before every statement of a C# file, which CSharp.lua copies into the script as a line of its
    /// own (<c>--@mw 3:12</c>) just before that statement's Lua; <see cref="LineTable"/> turns them into the script's line table.
    /// </summary>
    public static class LineMarks
    {
        /// <inheritdoc cref="Mark(string, int, IEnumerable{string}, out MarkedColumns)"/>
        public static string Mark(string source, int file, IEnumerable<string> symbols)
        {
            return Mark(source, file, symbols, out _);
        }

        /// <summary>
        /// Inserts <c>/*[[--@mw F:L]]*/ </c> at the start of each statement of a block or a switch section (F the file's number,
        /// L the statement's line), and before each block's <c>}</c> a location for the Lua after the block (<see cref="EndTag"/>),
        /// or <c>/*[[--@mw -]]*/ </c> when it is a member's body: lines after it name no C# line. The markers are inline, so no line
        /// moves; CSharp.lua's code template (<c>/*[[...]]*/</c>) keeps them when comments are off. CSharp.lua writes the comments
        /// of blocks and sections only, so a body with no braces gets braces around it, and a section before a case that can fail
        /// gets them too when it can have them; neither changes what the code does.
        /// </summary>
        /// <param name="source">The file's text.</param>
        /// <param name="file">The file's number in the line table, from 1.</param>
        /// <param name="symbols">The preprocessor symbols CSharp.lua compiles with; statements in inactive branches get no marker.</param>
        /// <param name="columns">What was inserted, to read a column of the marked text as the file's own.</param>
        /// <returns>The marked text.</returns>
        public static string Mark(string source, int file, IEnumerable<string> symbols, out MarkedColumns columns)
        {
            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest).WithPreprocessorSymbols(symbols);
            var tree = CSharpSyntaxTree.ParseText(source, options);
            var inserts = new List<(int Position, bool IsEnd, string Text)>();
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                if (node is StatementSyntax statement)
                {
                    if (statement.Parent is BlockSyntax)
                    {
                        inserts.Add((statement.SpanStart, false, Marker(StartTag(statement, file))));
                    }
                    else if (statement.Parent is SwitchSectionSyntax section)
                    {
                        AddSectionStatement(inserts, section, statement, file);
                    }
                    else if (IsBareBody(statement))
                    {
                        inserts.Add((statement.SpanStart, false, "{ " + Marker(StartTag(statement, file))));
                        inserts.Add((statement.Span.End, true, " " + Marker(EndTag(statement, file)) + "}"));
                    }
                }

                if (node is BlockSyntax block && !block.CloseBraceToken.IsMissing)
                {
                    inserts.Add((block.CloseBraceToken.SpanStart, true, Marker(EndTag(block, file))));
                }
            }

            // At one position an end goes before a start, and of two ends the inner one, which comes later in the walk, goes first.
            var lines = tree.GetText().Lines;
            columns = new MarkedColumns();
            var text = source;
            foreach (var insert in inserts.OrderByDescending(insert => insert.Position).ThenBy(insert => insert.IsEnd))
            {
                text = text.Insert(insert.Position, insert.Text);
                var at = lines.GetLinePosition(insert.Position);
                columns.Add(at.Line + 1, at.Character + 1, insert.Text.Length);
            }

            return text;
        }

        // A section's statement gets its marker. The Lua after a section is the next case's condition, which a comment after the
        // section's last statement cannot reach: CSharp.lua drops it. So a last statement that cannot fail itself takes the
        // case's line, and any other last statement gets braces around the section, to hold a marker at their end.
        private static void AddSectionStatement(List<(int Position, bool IsEnd, string Text)> inserts, SwitchSectionSyntax section, StatementSyntax statement, int file)
        {
            var isLast = statement == section.Statements[^1];
            var next = NextCase(section);
            var jumps = section.Statements[^1] is BreakStatementSyntax or ContinueStatementSyntax or GotoStatementSyntax;
            var alone = section.Statements.Count == 1 && statement is BlockSyntax;
            var braces = next != null && !jumps && !alone && !HasDeclarations(section);

            var tag = isLast && jumps && next != null ? Location(next, file) : StartTag(statement, file);
            inserts.Add((statement.SpanStart, false, (braces && statement == section.Statements[0] ? "{ " : string.Empty) + Marker(tag)));
            if (braces && isLast)
            {
                inserts.Add((statement.Span.End, true, " " + Marker(Location(next!, file)) + "}"));
            }
        }

        // A statement that is a body by itself, with no braces. The if of an else stays bare: CSharp.lua writes it as an elseif.
        private static bool IsBareBody(StatementSyntax statement)
        {
            return statement is not BlockSyntax && statement.Parent switch
            {
                IfStatementSyntax or WhileStatementSyntax or DoStatementSyntax or ForStatementSyntax or CommonForEachStatementSyntax => true,
                UsingStatementSyntax or LockStatementSyntax or FixedStatementSyntax => true,
                ElseClauseSyntax => statement is not IfStatementSyntax,
                _ => false,
            };
        }

        // A section's locals and labels belong to the whole switch, so braces would hide them from the other sections.
        private static bool HasDeclarations(SwitchSectionSyntax section)
        {
            return section.Statements.SelectMany(statement => statement.DescendantNodesAndSelf())
                .Any(node => node is LocalDeclarationStatementSyntax or LocalFunctionStatementSyntax or LabeledStatementSyntax or SingleVariableDesignationSyntax);
        }

        // The label that can fail of the section CSharp.lua writes after this one, which is the next but for a default section,
        // written last: a pattern or a when. A constant's label cannot fail.
        private static SwitchLabelSyntax? NextCase(SwitchSectionSyntax section)
        {
            if (IsDefault(section))
            {
                return null;
            }

            var sections = ((SwitchStatementSyntax)section.Parent!).Sections;
            return CaseOf(sections.Skip(sections.IndexOf(section) + 1));
        }

        private static SwitchLabelSyntax? CaseOf(IEnumerable<SwitchSectionSyntax> sections)
        {
            return sections.FirstOrDefault(section => !IsDefault(section))?.Labels.OfType<CasePatternSwitchLabelSyntax>().FirstOrDefault();
        }

        private static bool IsDefault(SwitchSectionSyntax section)
        {
            return section.Labels.Any(label => label is DefaultSwitchLabelSyntax);
        }

        private static string Marker(string tag)
        {
            return "/*[[--@mw " + tag + "]]*/ ";
        }

        private static string Location(SyntaxNode node, int file)
        {
            var line = node.SyntaxTree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
            return file + ":" + line;
        }

        // A statement's own line. CSharp.lua writes the first case's condition right after a switch's head, with no place for a
        // marker between them. A switch on a plain name has nothing in its head that can fail, so it names that condition's line;
        // on anything else it names its own.
        private static string StartTag(StatementSyntax statement, int file)
        {
            return statement is SwitchStatementSyntax { Expression: IdentifierNameSyntax } @switch && CaseOf(@switch.Sections) is { } label
                ? Location(label, file)
                : Location(statement, file);
        }

        // The line of the Lua written after a body: the next condition of an if, a do's condition, the next case of a switch.
        // Otherwise the statement the body is part of: the body itself when it is a statement of a block, else the nearest
        // statement around it (an if, a loop, the statement holding a lambda); none for a member's body or a lambda outside statements.
        private static string EndTag(StatementSyntax body, int file)
        {
            switch (body.Parent)
            {
                case IfStatementSyntax { Else.Statement: IfStatementSyntax next }:
                    return Location(next.Condition, file);
                case DoStatementSyntax loop:
                    return Location(loop.Condition, file);
                case SwitchSectionSyntax section when section.Statements.Count == 1 && NextCase(section) is { } label:
                    return Location(label, file);
                case BlockSyntax or SwitchSectionSyntax:
                    return Location(body, file);
            }

            for (var node = body.Parent; node != null && node is not MemberDeclarationSyntax; node = node.Parent)
            {
                if (node is StatementSyntax and not BlockSyntax)
                {
                    return Location(node, file);
                }
            }

            return "-";
        }
    }
}
