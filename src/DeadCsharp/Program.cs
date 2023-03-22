using Console = System.Console;
using Environment = System.Environment;
using System.Collections.Generic;

// We can not cherry-pick imports from System.CommandLine since InvokeAsync is a necessary extension.
using System.CommandLine;

namespace DeadCsharp
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Program
    {
        private static int Handle(string[] inputs, string[]? excludes, bool remove, string? output)
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

                    if (newProgramText != programText)
                    {
                        System.IO.File.WriteAllText(path, newProgramText, System.Text.Encoding.UTF8);
                        Console.WriteLine($"FIXED {path}");
                    }
                    else
                    {
                        Console.WriteLine($"OK   {path}");
                    }
                }
                else
                {
                    IEnumerable<Inspection.Suspect> suspects = Inspection.Inspect(tree);

                    bool hasSuspect = false;

                    switch (output)
                    {
                        case "count-by-file":
                            hasSuspect = Output.ReportCountByFile(path, suspects, Console.Out);
                            break;

                        default:
                            hasSuspect = Output.Report(path, suspects, Console.Out);
                            break;
                    }

                    if (hasSuspect)
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
                    "If set, removes the comments suspected to contain dead code"),

                new Option<string>(
                    new[] {"--output", "-o"},
                    "Select output format. Possible values: list-comments (default), count-by-file")
            };

            rootCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create(
                (string[] inputs, string[] excludes, bool remove, string output) => Handle(inputs, excludes, remove, output));

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