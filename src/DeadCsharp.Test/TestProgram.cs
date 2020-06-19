using Environment = System.Environment;
using NUnit.Framework;

namespace DeadCsharp.Test
{
    public class ProgramTests
    {
        [Test]
        public void TestNoCommandLineArguments()
        {
            using var consoleCapture = new ConsoleCapture();

            int exitCode = Program.MainWithCode(new string[0]);

            string nl = Environment.NewLine;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual($"Option '--inputs' is required.{nl}{nl}", consoleCapture.Error());
        }

        [Test]
        public void TestInvalidCommandLineArguments()
        {
            using var consoleCapture = new ConsoleCapture();

            int exitCode = Program.MainWithCode(new[] { "--invalid-arg" });

            string nl = Environment.NewLine;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(
                $"Option '--inputs' is required.{nl}" +
                $"Unrecognized command or argument '--invalid-arg'{nl}{nl}",
                consoleCapture.Error());
        }

        [Test]
        public void TestWithNonCodeInput()
        {
            using var tmpdir = new TemporaryDirectory();

            using var consoleCapture = new ConsoleCapture();

            string path = System.IO.Path.Join(tmpdir.Path, "SomeProgram.cs");
            System.IO.File.WriteAllText(path, "this is not parsable C# code.");

            int exitCode = Program.MainWithCode(new[] { "--inputs", path });

            string nl = Environment.NewLine;

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual($"OK   {path}{nl}", consoleCapture.Output());
        }

        [Test]
        public void TestSingleInput()
        {
            using var tmpdir = new TemporaryDirectory();

            string path = System.IO.Path.Join(tmpdir.Path, "SomeProgram.cs");
            const string programText =
                @"
namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            // SoDead();
            System.Console.WriteLine(""Hello, World!"");
        }
    }
}";
            using var consoleCapture = new ConsoleCapture();

            System.IO.File.WriteAllText(path, programText);

            int exitCode = Program.MainWithCode(new[] { "--inputs", path });

            string nl = Environment.NewLine;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(
                $"FAIL {path}:{nl}  * 8:13: a line ends with `;`{nl}",
                consoleCapture.Output());
        }

        [Test]
        public void TestMultipleInputs()
        {
            using var tmpdir = new TemporaryDirectory();

            const string programText =
                @"
namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            // SoDead();
            System.Console.WriteLine(""Hello, World!"");
        }
    }
}";
            string path1 = System.IO.Path.Join(tmpdir.Path, "SomeProgram.cs");
            string path2 = System.IO.Path.Join(tmpdir.Path, "AnotherProgram.cs");

            using var consoleCapture = new ConsoleCapture();

            System.IO.File.WriteAllText(path1, programText);
            System.IO.File.WriteAllText(path2, programText);

            int exitCode = Program.MainWithCode(new[] { "--inputs", System.IO.Path.Join(tmpdir.Path, "**.cs") });

            string nl = Environment.NewLine;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(
                $"FAIL {path2}:{nl}  * 8:13: a line ends with `;`{nl}" +
                $"FAIL {path1}:{nl}  * 8:13: a line ends with `;`{nl}",
                consoleCapture.Output());
        }

        [Test]
        public void TestExcludes()
        {
            using var tmpdir = new TemporaryDirectory();

            const string programText =
                @"
namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            // SoDead();
            System.Console.WriteLine(""Hello, World!"");
        }
    }
}";
            string includedPath = System.IO.Path.Join(tmpdir.Path, "SomeProgram.cs");
            string excludedPath = System.IO.Path.Join(tmpdir.Path, "ExcludedProgram.cs");

            using var consoleCapture = new ConsoleCapture();

            System.IO.File.WriteAllText(includedPath, programText);
            System.IO.File.WriteAllText(excludedPath, programText);

            int exitCode = Program.MainWithCode(
                new[]
                {
                    "--inputs", System.IO.Path.Join(tmpdir.Path, "*.cs"),
                    "--excludes", System.IO.Path.Join(tmpdir.Path, "Excluded*.cs"),
                });

            string nl = Environment.NewLine;

            Assert.AreEqual(1, exitCode);
            Assert.AreEqual(
                $"FAIL {includedPath}:{nl}  * 8:13: a line ends with `;`{nl}",
                consoleCapture.Output());
        }

        [Test]
        public void TestRemove()
        {
            using var tmpdir = new TemporaryDirectory();

            string path = System.IO.Path.Join(tmpdir.Path, "SomeProgram.cs");
            const string programText =
                @"
namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            // SoDead();
            System.Console.WriteLine(""Hello, World!"");
        }
    }
}";
            using var consoleCapture = new ConsoleCapture();

            System.IO.File.WriteAllText(path, programText);

            int exitCode = Program.MainWithCode(new[] { "--inputs", path, "--remove" });

            var newProgramText = System.IO.File.ReadAllText(path);

            var expected =
                @"
namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Console.WriteLine(""Hello, World!"");
        }
    }
}";
            string nl = Environment.NewLine;

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(expected, newProgramText);
            Assert.AreEqual(
                $"FIXED {path}{nl}",
                consoleCapture.Output());
        }
    }
}
