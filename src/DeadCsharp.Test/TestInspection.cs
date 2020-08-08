using System.Collections.Generic;
using System.Linq;

using CSharpSyntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree;

using NUnit.Framework;

namespace DeadCsharp.Test
{
    public class InspectCommentTests
    {
        [Test]
        public void TestCases()
        {
            var testCases = new List<(string, List<(string, int, int)>?, string)>
            {
                ("// A completely valid comment", null, "single-line valid comment"),
                ("// Precondition(s)", null, "single-line valid comment with parentheses"),
                ("/* A completely valid comment */", null, "single-line valid block comment"),
                ("/* A completely \n\n valid comment */", null, "multi-line valid comment"),
                ("// var x; // do something",
                    new List<(string, int, int)>{("a line contains `//`", 100, 210)},
                    "dead code with trailing single-line comment"),
                ("// var x; /* do something",
                    new List<(string, int, int)>
                    {
                        ("a line contains `/*`", 100, 210),
                    },
                    "dead code with opening block comment"),
                ("// } */",
                    new List<(string, int, int)> { ("a line contains `*/`", 100, 205) },
                    "dead code with closing block comment"),
                ("/*\n" +
                 "// do something\n" +
                 "*/",
                    new List<(string, int, int)> { ("a line contains `//`", 101, 0) },
                    "Single-line comment in a block comment"),
                ("// b &&",
                    new List<(string, int, int)> { ("a line contains `&&`", 100, 205) },
                    "Single-line comment with AND operator"),
                ("// b ||",
                    new List<(string, int, int)> { ("a line contains `||`", 100, 205) },
                    "Single-line comment with OR operator"),
                ("// [SomeAttribute",
                    new List<(string, int, int)> { ("a line starts with `[`", 100, 203) },
                    "Single-line comment with opening bracket"),
                ("// doSomething();",
                    new List<(string, int, int)> { ("a line ends with `;`", 100, 216) },
                    "Single-line comment with trailing semi-colon"),
                ("// doSomething(",
                    new List<(string, int, int)> { ("a line ends with `(`", 100, 214) },
                    "Single-line comment with trailing opening parenthesis"),
                ("// {",
                    new List<(string, int, int)> { ("a line ends with `{`", 100, 203) },
                    "Single-line comment with trailing opening curly brace"),
                ("// }",
                    new List<(string, int, int)> { ("a line ends with `}`", 100, 203) },
                    "Single-line comment with trailing closing curly brace"),
                ("// var x =",
                    new List<(string, int, int)> { ("a line ends with `=`", 100, 209) },
                    "Single-line comment with trailing equal sign"),
                ("// if (smc != null)",
                    new List<(string, int, int)>
                    {
                        (@"a line matches the pattern ""control statement""",
                            100, 203)
                    },
                    "Single-line comment with a control statement"),
                ("// var x = new A()",
                    new List<(string, int, int)>
                    {
                        (@"a line matches the pattern ""variable or member initialization""",
                            100, 203)
                    },
                    "Single-line comment with a variable initialization"),
                ("// private static readonly A x = new A()",
                    new List<(string, int, int)>
                    {
                        (@"a line matches the pattern ""variable or member initialization""",
                            100, 203)
                    },
                    "Single-line comment with a member initialization"),
                ("// .trainWrack().otherCall(3)",
                    new List<(string, int, int)>
                    {
                        (@"a line matches the pattern ""wagon function call""",
                            100, 203)
                    },
                    "Single-line comment with a wagon function call"),
            };

            foreach (var (triviaAsString, expectedCharacteristicsLinesColumns, label) in testCases)
            {
                int lineOffset = 100;
                int columnOffset = 200;
                List<Inspection.Cue>? got = Inspection.InspectComment(lineOffset, columnOffset, triviaAsString);

                var gotCharacteristicsLinesColumns =
                    got?
                        .Select(c => (Output.DescribeCharacteristic(c.Characteristic), c.Line, c.Column))
                        .ToList();

                Assert.That(
                    gotCharacteristicsLinesColumns,
                    Is.EqualTo(expectedCharacteristicsLinesColumns),
                    label);
            }
        }
    }

    public class ShouldSkipTests
    {
        [TestCase("///test me", true)]
        [TestCase("/// test me", true)]
        [TestCase("///////", true)]
        [TestCase("/// /* */", true)]
        [TestCase("not parsable c# code", true)]
        [TestCase("//test me", false)]
        [TestCase("// test me", false)]
        [TestCase("/* test me */", false)]
        public void TestShouldSkipTrivia(string triviaAsString, bool expectedResult)
        {
            Assert.AreEqual(expectedResult, Inspection.ShouldSkipTrivia(triviaAsString));
        }
    }

    public class InspectTests
    {
        [Test]
        public void TestBasic()
        {
            const string programText =
                @"
namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            /*
             if(dead)
            */
            System.Console.WriteLine(""Hello, World!"");
        }
    }
}";

            var tree = CSharpSyntaxTree.ParseText(programText);
            List<Inspection.Suspect> suspects = Inspection.Inspect(tree).ToList();

            Assert.AreEqual(1, suspects.Count);

            var onlySuspect = suspects[0];

            Assert.AreEqual(7, onlySuspect.Line);
            Assert.AreEqual(12, onlySuspect.Column);
            Assert.IsNotNull(onlySuspect.Cues);

            var characteristicsLinesColumns =
                onlySuspect.Cues
                    .Select(c => (Output.DescribeCharacteristic(c.Characteristic), c.Line, c.Column))
                    .ToList();

            Assert.That(characteristicsLinesColumns, Is.EquivalentTo(
                new List<(string, int, int)>
                {
                    (@"a line matches the pattern ""control statement""", 8, 13)
                }));
        }

        [Test]
        public void TestToggling()
        {
            const string programText =
                @"
namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            // dead-csharp off
            // soDead();
            // dead-csharp on

            // yetAnotherDeadCode();
            System.Console.WriteLine(""Hello, World!"");
        }
    }
}";
            var tree = CSharpSyntaxTree.ParseText(programText);
            List<Inspection.Suspect> suspects = Inspection.Inspect(tree).ToList();

            Assert.AreEqual(1, suspects.Count);

            var onlySuspect = suspects[0];

            Assert.AreEqual(12, onlySuspect.Column);
            Assert.AreEqual(11, onlySuspect.Line);
            Assert.IsNotNull(onlySuspect.Cues);

            var characteristicsLinesColumns =
                onlySuspect.Cues
                    .Select(c => (Output.DescribeCharacteristic(c.Characteristic), c.Line, c.Column))
                    .ToList();

            Assert.That(characteristicsLinesColumns,
                Is.EquivalentTo(
                new List<(string, int, int)> { ("a line ends with `;`", 11, 35) }));
        }
    }

    public class RemoveTests
    {
        [Test]
        public void TestRemoveNothing()
        {
            const string programText =
                @"
namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            // Valid comment
            /* valid comment */
            System.Console.Out(""Hello world"");
        }
    }
}";
            var tree = CSharpSyntaxTree.ParseText(programText);
            string newProgramText = Inspection.Remove(tree);

            Assert.AreEqual(programText, newProgramText);
        }

        // Only rejected comment
        [TestCase("// if(dead)", "")]
        [TestCase("// if(dead)\n", "")]
        // Comment on the first line
        [TestCase("/* if(dead) */\nvar i = 0;", "var i = 0;")]
        [TestCase(" /* if(dead) */\nvar i = 0;", "var i = 0;")]
        [TestCase(" /* if(dead) */ \nvar i = 0;", "var i = 0;")]
        // Comment in-between code on the same line
        [TestCase("if(a/* && dead */&& b)", "if(a&& b)")]
        [TestCase("if(a /* && dead */&& b)", "if(a && b)")]
        [TestCase("if(a/* && dead */ && b)", "if(a && b)")]
        [TestCase("if(a /* && dead */ && b)", "if(a  && b)")]
        // Comment at the end of the line
        [TestCase("var i = 0; /* if(dead) */", "var i = 0;")]
        [TestCase("var i = 0;/* if(dead) */\nvar j = 0;",
            "var i = 0;\nvar j = 0;")]
        [TestCase("var i = 0; /* if(dead) */\nvar j = 0;",
            "var i = 0;\nvar j = 0;")]
        [TestCase("var i = 0; /* if(dead) */ \nvar j = 0;",
            "var i = 0;\nvar j = 0;")]
        // Comment on the last line
        [TestCase("var i = 0;\n/* if(dead) */",
            "var i = 0;\n")]
        [TestCase("var i = 0;\n /* if(dead) */",
            "var i = 0;\n")]
        [TestCase("var i = 0;\n /* if(dead) */ ",
            "var i = 0;\n")]
        public void TestSnippet(string programText, string expected)
        {
            var tree = CSharpSyntaxTree.ParseText(programText);
            string newProgramText = Inspection.Remove(tree);

            Assert.AreEqual(expected, newProgramText);
        }


        [Test]
        public void TestToggling()
        {
            const string programText =
                @"
namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            // dead-csharp off
            // soDead();
            // dead-csharp on
            // Delimiter 1
            /*
            AnotherDeadCode();
            */
            // Delimiter 2
            // YetAnotherDeadCode();
            // Delimiter 3
            Thickness t = new Thickness(72);  // copy.PagePadding;
            copy.PagePadding = new Thickness();
        }
    }
}";

            var tree = CSharpSyntaxTree.ParseText(programText);
            string newProgramText = Inspection.Remove(tree);

            const string expected = @"
namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            // dead-csharp off
            // soDead();
            // dead-csharp on
            // Delimiter 1
            // Delimiter 2
            // Delimiter 3
            Thickness t = new Thickness(72);
            copy.PagePadding = new Thickness();
        }
    }
}";
            Assert.AreEqual(expected, newProgramText);
        }
    }
}
