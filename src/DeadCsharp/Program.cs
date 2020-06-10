using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SpotDeadCsharp
{
    class CommentCollector : CSharpSyntaxWalker
    {
        public readonly List<SyntaxTrivia> comments = new List<SyntaxTrivia>();

        public CommentCollector() : base(SyntaxWalkerDepth.StructuredTrivia)
        {
        }

        public override void VisitTrivia(SyntaxTrivia trivia)
        {
            var text = trivia.ToString().Trim();
            if (text.Length > 0)
            {
                comments.Add(trivia);
            }

            base.VisitTrivia(trivia);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            const string programText =
                @"using System;
using System.Collections;
using System.Linq;
using System.Text;

namespace HelloWorld
{
    class Program
    {
        static void Main(string[] args)
        {
            /*
                 Some multi-line
                 comment
            */

            // Some comment
            // Another comment
            // probably.dead.code();
            /* if(dead) */
            Console.WriteLine(""Hello, World!"");
        }
    }
}";
            SyntaxTree tree = CSharpSyntaxTree.ParseText(programText);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            var collector = new CommentCollector();
            collector.Visit(root);

            var errors = new List<SyntaxTrivia>();

            foreach (SyntaxTrivia trivia in collector.comments)
            {
                string trimmed = trivia.ToString().Trim();
                if (trimmed.StartsWith("///"))
                {
                    // Ignore structured comments.
                    // They most probably include the code on purpose.
                    continue;
                }

                string[] lines = trimmed.Split(
                    new[] {"\r\n", "\r", "\n"},
                    StringSplitOptions.None
                );
                
                Console.WriteLine("LInes: " + lines);

                foreach (string line in lines)
                {
                    string trimmedLine = line.TrimEnd();
                    
                    if (trimmedLine.EndsWith("*/"))
                    {
                        trimmedLine = trimmedLine.Substring(0, trimmedLine.Length - 2).TrimEnd();
                    }
                    
                    if (trimmedLine.EndsWith(";") || trimmedLine.EndsWith("(") || trimmedLine.EndsWith("{") ||
                        trimmedLine.EndsWith(")"))
                    {
                        errors.Add(trivia);
                    }
                }
            }

            string path = "somefile.cs";
            
            var message = "Probable dead code, at least one line of the comment ends in ';', '(', '{' or ')'";
            Console.WriteLine($"{path}: {message}");
            foreach (SyntaxTrivia trivia in errors)
            {
                var span = tree.GetLineSpan(trivia.Span);
                var position = span.StartLinePosition;
                
                Console.WriteLine($"  * At line {position.Line} and column {position.Character}");
            }
        }
    }
}