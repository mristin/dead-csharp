using System.Collections.Generic;
using System.Linq;
using CSharpSyntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree;
using NUnit.Framework;

namespace DeadCsharp.Test
{
    public class InspectCommentTests
    {
        [TestCase("// A completely valid comment", null)]
        [TestCase("/* A completely valid comment */", null)]
        [TestCase("/* A completely \n\n valid comment */", null)]
        [TestCase("// if(something) // some comment here", new[] { "contains `//`" })]
        [TestCase("// if(something) /* some comment here", new[] { "contains `/*`" })]
        [TestCase("// } */", new[] { "contains `*/`" })]
        [TestCase("/* Not good \n// some comment here\n*/", new[] { "contains `//`" })]
        [TestCase("/* Not good \n/* some comment here\n*/", new[] { "contains `/*`" })]
        [TestCase("/* Not good \nsome comment here*/\n*/", new[] { "contains `*/`" })]
        [TestCase("// if(something) {", new[] { "a line ends with `{`" })]
        [TestCase("// }  ", new[] { "a line ends with `}`" })]
        [TestCase("// doSomething(  ", new[] { "a line ends with `(`" })]
        public void TestTrivialCases(string triviaAsString, string[]? expected)
        {
            List<string>? actual = Inspection.InspectComment(triviaAsString);

            if (expected == null)
            {
                Assert.IsNull(actual);
            }
            else
            {
                Assert.That(actual, Is.EquivalentTo(expected));
            }
        }

        [Test]
        public void TestInspectCommentOnComplexMultilineComment()
        {
            string triviaAsString = "/* Not good \n" +
                                    "/*\n" +
                                    "SoDead();\n" +
                                    "*/" +
                                    "This is wrong */";
            var expected = new[] { "contains `/*`", "contains `*/`", "a line ends with `;`" };

            List<string>? actual = Inspection.InspectComment(triviaAsString);

            Assert.IsNotNull(actual);
            Assert.That(actual, Is.EquivalentTo(expected));
        }

        [TestCase("// for(var i = 0; i < 10; i++)", true)]
        [TestCase("// foreach(var x of someList)", true)]
        [TestCase("// if(dead)", true)]
        [TestCase("// .trainWrack().otherCall(3)", true)]
        [TestCase("// var x = new A()", true)]
        [TestCase("// IA x = new A()", true)]
        [TestCase("// private static readonly A x = new A()", true)]
        [TestCase("// this should not (fire)", false)]
        [TestCase("// Precondition(s)", false)]
        public void TestSuffixBracket(string triviaAsString, bool shouldFire)
        {
            List<string>? actual = Inspection.InspectComment(triviaAsString);
            Assert.AreEqual(shouldFire, actual != null);
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
            // if(dead)
            System.Console.WriteLine(""Hello, World!"");
        }
    }
}";

            var tree = CSharpSyntaxTree.ParseText(programText);
            List<Inspection.Suspect> suspects = Inspection.Inspect(tree).ToList();

            Assert.AreEqual(1, suspects.Count);

            var onlySuspect = suspects[0];

            Assert.AreEqual(12, onlySuspect.Column);
            Assert.AreEqual(7, onlySuspect.Line);
            Assert.IsNotNull(onlySuspect.Cues);
            Assert.That(onlySuspect.Cues, Is.EquivalentTo(
                new List<string>() { @"a line matches `^\s*(if|else\s+if|for|foreach)\(.*\)\s*$`" }));
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
            Assert.That(onlySuspect.Cues, Is.EquivalentTo(
                new List<string>() { "a line ends with `;`" }));
        }
    }

    public class RemoveTests
    {
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
            // yetAnotherDeadCode();
            System.Console.WriteLine(""Hello, World!"");
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
            System.Console.WriteLine(""Hello, World!"");
        }
    }
}";
            Assert.AreEqual(expected, newProgramText);
        }
    }
}
