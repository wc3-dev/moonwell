namespace Moonwell.Testing
{
    /// <summary>
    /// Cases for the patterns MW0017 and MW0018 allow since decision 80, which the fork's CSharp.lua 2.0.4-moonwell.12
    /// writes right: parentheses in an is test, not inside a property pattern, an extended property pattern
    /// ({ Inner.X: 9 }), and a positional pattern in an is test on a value that cannot be null (a tuple or a struct); and
    /// since 2.0.4-moonwell.14, a type's name with no variable.
    /// </summary>
    public static class PatternCases
    {
        /// <summary>Parentheses in an is test: at the top, under not, inside a property pattern and around a positional pattern.</summary>
        public static void IsParentheses(FakeWorld world)
        {
            var x = 5;
            Expect.True(x is (1 or 5), "x is (1 or 5)");
            Expect.True(x is (> 0 and < 10), "x is (> 0 and < 10)");
            Expect.True(x is not (1 or 2), "x is not (1 or 2)");
            Expect.True(!(x is (> 6 or < 0)), "x is (> 6 or < 0)");
            Expect.True(x is (> 0 and (< 3 or > 4)), "nested parentheses");
            var node = new Node { X = 3 };
            Expect.True(node is { X: (1 or 3) }, "inside a property pattern");
            Expect.True(!(node is { X: (> 5 and < 9) }), "inside a property pattern, false");
            var pair = (4, 2);
            Expect.True(pair is ((> 3 and < 5), 2), "inside a positional pattern");
        }

        /// <summary>A type's name with no variable checks the type: in a case with when, an arm, with or, and and not, and inside a property pattern (C43).</summary>
        public static void TypeNames(FakeWorld world)
        {
            object box = new Box();
            object crate = new Crate();
            object other = new Other();
            object text = "a";
            Expect.Equal("box", CaseWhen(box, true), "case with when");
            Expect.Equal("box", CaseWhen(crate, true), "case with when, a derived class");
            Expect.Equal("none", CaseWhen(box, false), "case with when, false");
            Expect.Equal("none", CaseWhen(other, true), "case with when, another type");
            Expect.Equal("box", Arm(box), "an arm");
            Expect.Equal("box", Arm(crate), "an arm, a derived class");
            Expect.Equal("other", Arm(other), "an arm, the second");
            Expect.Equal("text", Arm(text), "string =>");
            Expect.Equal("number", Arm(1), "int =>");
            Expect.Equal("either", Either(text), "an arm with or, the second");
            Expect.Equal("either", Either(1), "an arm with or, the first");
            Expect.Equal("none", Either(box), "an arm with or, neither");
            Expect.Equal("none", Arm(null), "an arm, null");
            Expect.True(box is Box or Other, "or, the first");
            Expect.True(other is Box or Other, "or, the second");
            Expect.True(!(text is Box or Other), "or, neither");
            Expect.True(box is Box and not Crate, "and not, the plain class");
            Expect.True(!(crate is Box and not Crate), "and not, the derived class");
            Expect.True(new Shelf { Held = crate } is { Held: Box }, "in a property pattern");
            Expect.True(!(new Shelf { Held = other } is { Held: Box }), "in a property pattern, another type");
            Expect.True(!(new Shelf() is { Held: Box }), "in a property pattern, null");
        }

        /// <summary>not inside a property pattern, in an is test and in a switch expression arm, on a null and a set member and on a null input.</summary>
        public static void NotInProperty(FakeWorld world)
        {
            var node = new Node { X = 3 };
            Expect.True(!(node is { Name: not null }), "a null name is not not null");
            node.Name = "a";
            Expect.True(node is { Name: not null }, "a set name");
            Expect.True(!(node is { X: not 3 }) && node is { X: not 4 }, "not a constant");
            Expect.True(node is { X: not (1 or 2), Name: not null }, "two members");
            Expect.True(!(node is { Inner: not null }), "a null inner");
            node.Inner = new Node { X = 1 };
            Expect.True(node is { Inner: not null } && !(node is { Inner: not { X: 1 } }), "a set inner");
            Node? none = null;
            Expect.True(!(none is { Name: not null }), "a null input");
            Expect.Equal("named", Named(node), "an arm, a set name");
            Expect.Equal("anon", Named(new Node()), "an arm, a null name");
            Expect.Equal("anon", Named(null), "an arm, a null input");
        }

        /// <summary>An extended property pattern ({ Inner.X: 9 }), in an is test and in a switch expression arm, with a null inner and a null input.</summary>
        public static void ExtendedProperty(FakeWorld world)
        {
            var node = new Node { X = 1, Inner = new Node { X = 9, Inner = new Node { X = 2 } } };
            Expect.True(node is { Inner.X: 9 }, "Inner.X: 9");
            Expect.True(!(node is { Inner.X: 8 }), "Inner.X: 8");
            Expect.True(node is { Inner.Inner.X: 2 }, "two levels");
            Expect.True(node is { Inner.X: > 5 } && !(node is { Inner.X: > 9 }), "relational");
            Expect.True(node is { X: 1, Inner.X: 9 }, "beside a plain member");
            Expect.True(!(node is { Inner.Name: not null }), "with not, a null name");
            Expect.True(!(new Node() is { Inner.X: 9 }), "a null inner");
            Node? none = null;
            Expect.True(!(none is { Inner.X: 9 }), "a null input");
            Expect.Equal("nine", Nine(node), "an arm");
            Expect.Equal("other", Nine(new Node { Inner = new Node { X = 1 } }), "an arm, another value");
            Expect.Equal("other", Nine(new Node()), "an arm, a null inner");
            Expect.Equal("other", Nine(null), "an arm, a null input");
        }

        /// <summary>A positional pattern in an is test on a tuple, a struct with Deconstruct and a record struct: constants, discards, relational, names, not, a property pattern inside, and with and, or and not around it.</summary>
        public static void PositionalOnValue(FakeWorld world)
        {
            var pair = (1, 2);
            Expect.True(pair is (1, _) && !(pair is (2, _)), "a tuple, a constant and a discard");
            Expect.True(pair is (> 0, >= 2) && !(pair is (> 0, > 5)), "a tuple, relational");
            Expect.Equal(12, pair is (var a, var b) ? a * 10 + b : -1, "a tuple, names");
            Expect.True(pair is (not 3, _), "a tuple, not inside");
            Expect.True(pair is (2, _) or (1, _), "or around it");
            Expect.True(pair is (1, _) and (_, 2), "and around it");
            Expect.True(!(pair is not (1, _)), "not around it");
            var withNode = (new Node { Name = "a" }, 1);
            Expect.True(withNode is ({ Name: not null }, 1), "a property pattern inside");
            var size = new Size { Width = 1, Height = 2 };
            Expect.True(size is (1, 2) && !(size is (1, 3)), "a struct");
            Expect.Equal(2, size is Size(1, var h) ? h : -1, "a struct named, with a name inside");
            var spot = new Spot(1, 2);
            Expect.True(spot is (1, 2) && !(spot is (1, 3)), "a record struct");
            Expect.Equal(2, spot is Spot(_, var y) ? y : -1, "a record struct named");
        }

        /// <summary>The name of a node's kind: whether it has a name.</summary>
        private static string Named(Node? node) => node switch { { Name: not null } => "named", _ => "anon" };

        /// <summary>Whether a node's inner X is 9.</summary>
        private static string Nine(Node? node) => node switch { { Inner.X: 9 } => "nine", _ => "other" };

        private static string CaseWhen(object value, bool yes)
        {
            switch (value)
            {
                case Box when yes:
                    return "box";
                case Other when !yes:
                    return "other";
                default:
                    return "none";
            }
        }

        private static string Arm(object? value)
        {
            return value switch
            {
                Box => "box",
                Other => "other",
                string => "text",
                int => "number",
                _ => "none",
            };
        }

        private static string Either(object value)
        {
            return value switch
            {
                int or string => "either",
                _ => "none",
            };
        }

        /// <summary>A class with a class under it.</summary>
        private class Box
        {
        }

        /// <summary>The class under <see cref="Box"/>.</summary>
        private sealed class Crate : Box
        {
        }

        /// <summary>A class that is no <see cref="Box"/>.</summary>
        private sealed class Other
        {
        }

        /// <summary>A class with a member of any type.</summary>
        private sealed class Shelf
        {
            public object? Held;
        }

        /// <summary>A class with a number, a name and an inner node.</summary>
        private sealed class Node
        {
            public int X;

            public string? Name;

            public Node? Inner;
        }

        /// <summary>A struct that deconstructs into its width and height.</summary>
        private struct Size
        {
            public int Width;

            public int Height;

            /// <summary>Gives the width and height.</summary>
            public void Deconstruct(out int width, out int height)
            {
                width = Width;
                height = Height;
            }
        }

        /// <summary>A record struct of two numbers.</summary>
        private record struct Spot(int X, int Y);
    }
}
