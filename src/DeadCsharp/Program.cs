using Console = System.Console;
using Environment = System.Environment;
using System.Collections.Generic;

// We can not cherry-pick imports from System.CommandLine since InvokeAsync is a necessary extension.
using System.CommandLine;

namespace DeadCsharp
{
    public class Program
    {
        private static int Handle(string[] inputs, string[]? excludes, bool remove)
        {
            int exitCode = 0;

            string cwd = System.IO.Directory.GetCurrentDirectory();
            IEnumerable<string> paths = Input.MatchFiles(
                cwd,
                new List<string>(inputs),
                new List<string>(excludes ?? new string[0]));

            foreach (string path in paths)
            {
                string programText = System.IO.File.ReadAllText(path);
                var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(programText);

                if (remove)
                {
                    string newProgramText = Inspection.Remove(tree);
                    System.IO.File.WriteAllText(path, newProgramText);
                    Console.WriteLine($"FIXED {path}");
                }
                else
                {
                    IEnumerable<Inspection.Suspect> suspects = Inspection.Inspect(tree);

                    var hasSuspect = new Output.HasSuspect();
                    IEnumerable<string> lines = Output.Report(path, suspects, hasSuspect);

                    foreach (string line in lines)
                    {
                        Console.WriteLine(line);
                    }

                    if (hasSuspect.Value)
                    {
                        exitCode = 1;
                    }
                }
            }

            return exitCode;
        }

        public static int MainWithCode(string[] args)
        {
            var rootCommand = new RootCommand(
                "Examines the C# code for dead code in the comments.")
            {
                new Option<string[]>(
                        new[] {"--inputs", "-i"},
                        "Glob patterns of the files to be inspected")
                    { Required=true },

                new Option<string[]>(
                    new[] {"--excludes", "-e"},
                    "Glob patterns of the files to be excluded from inspection"),

                new Option<bool>(
                    new[] {"--remove", "-r"},
                    "If set, removes the comments suspected to contain dead code"
                )
            };

            rootCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create(
                (string[] inputs, string[] excludes, bool remove) => Handle(inputs, excludes, remove));

            int exitCode = rootCommand.InvokeAsync(args).Result;
            return exitCode;
        }

        public static void Main(string[] args)
        {
            int exitCode = MainWithCode(args);
            Environment.ExitCode = exitCode;
        }
    }
}