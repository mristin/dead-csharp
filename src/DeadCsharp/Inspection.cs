using InvalidOperationException = System.InvalidOperationException;
using ArgumentException = System.ArgumentException;
using StringSplitOptions = System.StringSplitOptions;
using Regex = System.Text.RegularExpressions.Regex;
using Match = System.Text.RegularExpressions.Match;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis.CSharp;  // This is needed for extensions.

using SyntaxNode = Microsoft.CodeAnalysis.SyntaxNode;
using SyntaxTrivia = Microsoft.CodeAnalysis.SyntaxTrivia;
using SyntaxTree = Microsoft.CodeAnalysis.SyntaxTree;
using SyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using CompilationUnitSyntax = Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax;
using CSharpSyntaxRewriter = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxRewriter;


namespace DeadCsharp
{
    public static class Inspection
    {
        public abstract class Characteristic { }

        public class Trailing : Characteristic
        {
            public readonly string Trail;

            public Trailing(string trail)
            {
                Trail = trail;
            }
        }

        public class Prefixed : Characteristic
        {
            public readonly string Prefix;

            public Prefixed(string prefix)
            {
                Prefix = prefix;
            }
        }

        public class Contains : Characteristic
        {
            public readonly string Feature;

            public Contains(string feature)
            {
                Feature = feature;
            }
        }

        public class Matches : Characteristic
        {
            public readonly string Identifier;

            public Matches(string identifier)
            {
                Identifier = identifier;
            }
        }

        public class Cue
        {
            public readonly Characteristic Characteristic;
            public readonly int Line;  // starts at 0
            public readonly int Column;  // starts at 0

            public Cue(Characteristic characteristic, int line, int column)
            {
                if (line < 0)
                {
                    throw new ArgumentException($"Unexpected negative line: {line}");
                }

                if (column < 0)
                {
                    throw new ArgumentException($"Unexpected negative column: {column}");
                }

                Characteristic = characteristic;
                Line = line;
                Column = column;
            }
        }
        private class SuspectTrivia
        {
            public readonly SyntaxTrivia Trivia;
            public readonly List<Cue> Cues;

            public SuspectTrivia(SyntaxTrivia trivia, List<Cue> cues)
            {
                Trivia = trivia;
                Cues = cues;
            }
        }

        public class Suspect
        {
            public readonly int Line; // starts at 0
            public readonly int Column; // starts at 0
            public readonly List<Cue> Cues;

            public Suspect(int line, int column, List<Cue> cues)
            {
                if (line < 0)
                {
                    throw new ArgumentException($"Unexpected negative line: {line}");
                }

                if (column < 0)
                {
                    throw new ArgumentException($"Unexpected negative column: {column}");
                }

                Line = line;
                Column = column;
                Cues = cues;
            }
        }

        public static bool ShouldSkipTrivia(string triviaAsString)
        {
            return triviaAsString.StartsWith("///") ||
                   triviaAsString.Contains("dead-csharp ignore this comment") ||
                   (!triviaAsString.StartsWith("//") && !triviaAsString.StartsWith("/*"));
        }

        /// <summary>
        /// Extracts the content of the comment stripping the prefix/suffix `//`, `/*` and `*/`.
        /// </summary>
        /// <param name="triviaAsString">Comment as string (including prefix/suffix)</param>
        /// <returns>Text of the content without the prefix/suffix</returns>
        /// <exception cref="ArgumentException">on invalid input string</exception>
        private static string WithoutCommentTokens(string triviaAsString)
        {
            string text;
            if (triviaAsString.StartsWith("//"))
            {
                text = triviaAsString.Substring(2);
            }
            else if (triviaAsString.StartsWith("/*"))
            {
                text = triviaAsString.Substring(2);

                string suffix = text.Substring(text.Length - 2);
                if (suffix != "*/")
                {
                    throw new ArgumentException(
                        $"Unexpected comment starting with '/*' but no trailing '*/': {text}");
                }

                text = text.Substring(0, text.Length - 2);
            }
            else
            {
                throw new ArgumentException(
                    $"Unexpected comment not having neither '//' nor '/*' prefix: {triviaAsString}");
            }

            return text;
        }

        private static readonly List<string> FeatureSubstrings = new List<string>
        {
            "//", "/*", "*/", "||", "&&"
        };

        private static readonly Regex AllWhitespaceRegex = new Regex(@"^[ \t]*$");

        public static readonly SortedDictionary<string, Regex> CodeRegexes =
            new SortedDictionary<string, Regex>
        {
            {"control statement", new Regex(@"^\s*(if|else\s+if|for|foreach|switch|while)\s*\(.*\)\s*$")},

            {"variable or member initialization",
                new Regex(@"^\s*([a-zA-Z_0-9]+((\s+|\.)[a-zA-Z_0-9]+)*)" +
                      @"\s*" +
                      @"([a-zA-Z_0-9]+(\.[a-zA-Z_0-9]+)*)" +
                      @"\s*=\s*" +
                      @"new\s*.*\(.*\)$")},

            {"wagon function call",
                new Regex(@"^\s*(\.)([a-zA-Z_0-9]+(\.[a-zA-Z_0-9]+)*)\(.*\)\s*$")}
        };

        /// <summary>
        /// Inspects the given comment and reports any cues suggesting it might contain dead code.
        /// </summary>
        /// <param name="lineOffset">Starting line of the comment</param>
        /// <param name="columnOffset">Starting column of the comment</param>
        /// <param name="triviaAsText">Comment's content from the trivia node of the syntax tree</param>
        /// <returns>null if no cues or a list with one or more cues</returns>
        public static List<Cue>? InspectComment(int lineOffset, int columnOffset, string triviaAsText)
        {
            string content = WithoutCommentTokens(triviaAsText);

            List<Cue>? cues = null;

            string[] lines = content.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.None);

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                // We need to consider for the column offset only in the very first line (as parser gives us offset
                // only at the first line). Additionally, we need to account for the opening token (`//` or `/*`).
                columnOffset = (lineIndex == 0) ? columnOffset + 2 : 0;

                string line = lines[lineIndex];

                if (line.Length == 0 || AllWhitespaceRegex.IsMatch(line))
                {
                    continue;
                }

                // This is just a heuristic which works well in most cases.
                //
                // The alternative approach where the comment content is parsed did not actually
                // work since syntax parsing accepts most of the human language as well.
                //
                // A more sophisticated approach based on machine learning would be preferable
                // once enough data is gathered.

                foreach (string feature in FeatureSubstrings)
                {
                    int column = line.IndexOf(feature, System.StringComparison.InvariantCulture);
                    if (column >= 0)
                    {
                        (cues ??= new List<Cue>()).Add(
                            new Cue(
                                new Contains(feature),
                                lineIndex + lineOffset,
                                column + columnOffset));
                    }
                }

                // Start and end of the substring in the line excluding
                // the whitespace margin and the whitespace trail
                int first = -1;
                int last = -1;
                for (int i = 0; i < line.Length; i++)
                {
                    if (line[i] != ' ' && line[i] != '\t' && line[i] != '\r')
                    {
                        first = i;
                        break;
                    }
                }

                for (int i = line.Length - 1; i >= 0; i--)
                {
                    if (line[i] != ' ' && line[i] != '\t' && line[i] != '\r')
                    {
                        last = i;
                        break;
                    }
                }

                if (first == -1 || last == -1 || first > last)
                {
                    throw new InvalidOperationException(
                        "Line is not all whitespace or empty, " +
                        $"but {nameof(first)} was {first} and {nameof(last)} was {last}." +
                        $"The line was: <<start>>{line}<<end>>");
                }

                char firstChar = line[first];
                if (firstChar == '[')
                {
                    (cues ??= new List<Cue>()).Add(
                        new Cue(
                            new Prefixed(firstChar.ToString()),
                            lineIndex + lineOffset,
                            first + columnOffset));
                }

                char lastChar = line[last];
                if (lastChar == ';' || lastChar == '(' || lastChar == '{' || lastChar == '}' || lastChar == '=')
                {
                    (cues ??= new List<Cue>()).Add(
                        new Cue(
                            new Trailing(lastChar.ToString()),
                            lineIndex + lineOffset,
                            last + columnOffset));
                }

                foreach (var (identifier, regex) in CodeRegexes)
                {
                    var codeMatch = regex.Match(line);
                    if (codeMatch.Success)
                    {
                        if (codeMatch.Groups.Count == 0)
                        {
                            throw new InvalidOperationException(
                                "Expected the match to contain at least one group, " +
                                "but the match contained none. " +
                                $"The regex {identifier} was: {regex}, " +
                                $"the line was: <<start>>{line}<<end>>");
                        }

                        (cues ??= new List<Cue>()).Add(
                            new Cue(
                                new Matches(identifier),
                                lineIndex + lineOffset,
                                codeMatch.Groups[1].Index + columnOffset));
                    }
                }
            }

            return cues;
        }

        private class OnOffTracker
        {
            public bool IsOn { get; private set; }

            public OnOffTracker()
            {
                IsOn = true;
            }

            public void Feed(string triviaAsString)
            {
                var trimmed = triviaAsString.TrimEnd();
                if (trimmed == "// dead-csharp off")
                {
                    IsOn = false;
                }
                else if (trimmed == "// dead-csharp on")
                {
                    IsOn = true;
                }
            }
        }

        private static bool TriviaIsComment(string triviaAsString)
        {
            return triviaAsString.StartsWith("//") || triviaAsString.StartsWith("/*");
        }

        private static IEnumerable<SuspectTrivia> InspectTrivias(SyntaxTree tree)
        {
            var root = (CompilationUnitSyntax)tree.GetRoot();

            var tracker = new OnOffTracker();

            var relevantTrivias =
                root.DescendantTrivia()
                    // Beware: ToString() is an expensive operation on Syntax Nodes and
                    // involves some complex logic and a string builder!
                    // Hence we convert the trivia to string only at this single place.
                    .Select((trivia) => (trivia, trivia.ToString()))
                    .Where(
                        (t) =>
                        {
                            var (_, triviaAsString) = t;
                            tracker.Feed(triviaAsString);

                            return tracker.IsOn &&
                                   TriviaIsComment(triviaAsString) &&
                                   !ShouldSkipTrivia(triviaAsString);
                        });

            foreach (var (trivia, triviaAsString) in relevantTrivias)
            {
                var span = tree.GetLineSpan(trivia.Span);
                var position = span.StartLinePosition;

                List<Cue>? cues = InspectComment(position.Line, position.Character, triviaAsString);

                if (cues != null)
                {
                    yield return new SuspectTrivia(trivia, cues);
                }
            }
        }

        /// <summary>
        /// Inspects the syntax tree and reports the comments which seem to be dead code.
        /// </summary>
        /// <param name="tree">Parsed syntax tree</param>
        /// <returns>List of problematic comments</returns>
        public static IEnumerable<Suspect> Inspect(SyntaxTree tree)
        {
            IEnumerable<SuspectTrivia> suspectTrivias = InspectTrivias(tree);

            return suspectTrivias.Select(
                (suspectTrivia) =>
                {
                    var span = tree.GetLineSpan(suspectTrivia.Trivia.Span);
                    var position = span.StartLinePosition;

                    return new Suspect(position.Line, position.Character, suspectTrivia.Cues);
                });
        }

        private class CommentRemover : CSharpSyntaxRewriter
        {
            private readonly HashSet<int> _rejects;

            /// <param name="rejects">span starts of the trivias to be removed</param>
            public CommentRemover(HashSet<int> rejects)
            {
                _rejects = rejects;
            }

            public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
            {
                if (_rejects.Contains(trivia.SpanStart))
                {
                    return default;
                }

                return trivia;
            }
        }

        private class NodeOrTrivia
        {
            private readonly SyntaxNode? _node;
            public readonly SyntaxTrivia? Trivia;
            public readonly int SpanStart;

            public bool IsNode() => _node != null;

            public bool IsTrivia() => Trivia != null;

            public bool IsEndOfFile() => Trivia == null && _node == null;

            public NodeOrTrivia(SyntaxNode? node, SyntaxTrivia? trivia)
            {
                if (node != null && trivia != null)
                {
                    throw new ArgumentException("Both node and trivia are given.");
                }

                _node = node;
                Trivia = trivia;

                if (node != null)
                {
                    SpanStart = node.Span.Start;
                }
                else if (trivia != null)
                {
                    SpanStart = trivia.Value.Span.Start;
                }
                else
                {
                    // End of file does not have a span start.
                    SpanStart = -1;
                }
            }
        }

        private static IEnumerable<NodeOrTrivia> MergeNodesAndTrivias(CompilationUnitSyntax root)
        {
            using var nodeCursor = root.DescendantNodes().GetEnumerator();
            using var triviaCursor = root.DescendantTrivia().GetEnumerator();

            bool doneWithNodes = !nodeCursor.MoveNext();
            bool doneWithTrivias = !triviaCursor.MoveNext();

            while (!doneWithNodes || !doneWithTrivias)
            {
                if (doneWithNodes && !doneWithTrivias)
                {
                    yield return new NodeOrTrivia(null, triviaCursor.Current);
                    doneWithTrivias = !triviaCursor.MoveNext();
                }
                else if (!doneWithNodes && doneWithTrivias)
                {
                    yield return new NodeOrTrivia(nodeCursor.Current, null);
                    doneWithNodes = !nodeCursor.MoveNext();
                }
                else
                {
                    if (nodeCursor.Current.SpanStart < triviaCursor.Current.SpanStart)
                    {
                        yield return new NodeOrTrivia(nodeCursor.Current, null);
                        doneWithNodes = !nodeCursor.MoveNext();
                    }
                    else
                    {
                        yield return new NodeOrTrivia(null, triviaCursor.Current);
                        doneWithTrivias = !triviaCursor.MoveNext();
                    }
                }
            }

            yield return new NodeOrTrivia(null, null);
        }

        private static readonly List<Regex> RemoveDecisionChain = new List<Regex>
        {
            // Rejected comment spans the whole line
            new Regex(@"^.*(B|N)(S?RS?N)(.)$"),
            
            // Rejected comment is either in-between the code or trails the code
            new Regex(@"^(.*C)(S?RS?)(E|C|N)$"),
            
            // Rejected comment is the last thing in the file
            new Regex(@"^(.*[^S])(S?RS?N?)(E)$")
        };

        /// <summary>
        /// Inspects the syntax tree and remove the comments which seem to be dead code.
        ///
        /// Unlike <see cref="Inspect"/> which does not materialize the list of suspect comments,
        /// this method needs to collect the suspect comments to a hash set (based on their span start).
        /// </summary>
        /// <param name="tree">Parsed syntax tree</param>
        public static string Remove(SyntaxTree tree)
        {
            // Map syntax nodes to be rejected to their span start for faster indexing 
            HashSet<int> rejects = new HashSet<int>(
                InspectTrivias(tree).Select(suspectTrivia => suspectTrivia.Trivia.SpanStart));

            var root = (CompilationUnitSyntax)tree.GetRoot();

            // Decide whether to delete prefix or suffix white space (including new line) in a second pass
            HashSet<int> additionalRejects = new HashSet<int>();

            // Use symbols to represent the last couple of nodes:
            // '_': null / not observed
            // 'S': whitespace trivia
            // 'N': newline trivia
            // 'C': code
            // 'R': rejected comment
            // 'B': beginning of file
            // 'E': end of file
            // 'X': none of the above
            var lastSymbols = "_____B";

            var lastSpanStarts = new List<int>(6) { -1, -1, -1, -1, -1, -1 };

            foreach (NodeOrTrivia current in MergeNodesAndTrivias(root))
            {
                // Put on deque

                char newSymbol = 'X';
                if (current.IsEndOfFile())
                {
                    newSymbol = 'E';
                }
                else if (current.IsNode())
                {
                    newSymbol = 'C';
                }
                else if (current.IsTrivia())
                {
                    if (current.Trivia == null)
                    {
                        throw new InvalidOperationException("IsTrivia is true, but Trivia is null.");
                    }
                    switch (current.Trivia.Value.Kind())
                    {
                        case SyntaxKind.WhitespaceTrivia: newSymbol = 'S'; break;
                        case SyntaxKind.EndOfLineTrivia: newSymbol = 'N'; break;
                        default:
                            if (rejects.Contains(current.Trivia.Value.SpanStart))
                            {
                                newSymbol = 'R';
                            }
                            break;
                    }
                }
                else
                {
                    throw new InvalidOperationException("NodeOrTrivia switch logic is missing a case.");
                }

                // Shift

                if (lastSymbols.Length != lastSpanStarts.Count)
                {
                    throw new InvalidOperationException(
                        $"Expected lastSymbols.Length (== {lastSymbols.Length}) " +
                        $"to be equal lastSpanStarts.Count (== {lastSpanStarts.Count}).");
                }
                lastSymbols = lastSymbols.Substring(1, lastSymbols.Length - 1) + newSymbol;
                lastSpanStarts = lastSpanStarts.GetRange(1, lastSpanStarts.Count - 1);
                lastSpanStarts.Add(current.SpanStart);

                // Determine the action

                Match? match = null;
                foreach (Regex regex in RemoveDecisionChain)
                {
                    match = regex.Match(lastSymbols);
                    if (match.Success)
                    {
                        break;
                    }
                }

                // Perform action

                if (match != null && match.Success)
                {
                    var targetGroup = match.Groups[2];
                    if (targetGroup == null)
                    {
                        throw new InvalidOperationException(
                            $"Expected group 2 to exist in the match: {match}");
                    }

                    var end = targetGroup.Index + targetGroup.Length;
                    for (int i = targetGroup.Index; i < end; i++)
                    {
                        var spanStart = lastSpanStarts[i];
                        if (spanStart < 0)
                        {
                            throw new InvalidOperationException(
                                $"Expected span start to be set at index {i} for matching the range " +
                                $"[{targetGroup.Index}, {targetGroup.Index + targetGroup.Length}) " +
                                $"of pattern: {lastSymbols}");
                        }

                        additionalRejects.Add(spanStart);
                    }
                }
            }

            rejects.UnionWith(additionalRejects);

            // Assume the same order of iteration in visitor and InspectTrivias!
            var commentRemover = new CommentRemover(rejects);

            var newRoot = commentRemover.Visit(root);
            if (newRoot == null)
            {
                throw new InvalidOperationException(
                    "Unexpected null new root from commentRemover");
            }

            return newRoot.ToFullString();
        }
    }
}
