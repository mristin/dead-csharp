using TextWriter = System.IO.TextWriter;
using ArgumentException = System.ArgumentException;
using InvalidOperationException = System.InvalidOperationException;
using Regex = System.Text.RegularExpressions.Regex;

using System.Collections.Generic;

namespace DeadCsharp
{

    public static class Output
    {
        public static string DescribeCharacteristic(Inspection.Characteristic characteristic)
        {
            switch (characteristic)
            {
                case Inspection.Trailing t:
                    return $"a line ends with `{t.Trail}`";
                case Inspection.Prefixed p:
                    return $"a line starts with `{p.Prefix}`";
                case Inspection.Contains c:
                    return $"a line contains `{c.Feature}`";
                case Inspection.Matches m:
                    return $"a line matches the pattern \"{m.Identifier}\"";
                default:
                    throw new InvalidOperationException(
                        $"Unhandled type of {nameof(characteristic)}: {characteristic.GetType()}");
            }
        }

        /// <summary>
        /// Generates the report based on the suspect comments.
        /// </summary>
        /// <param name="path">Path to the inspected file</param>
        /// <param name="suspects">Suspect comments</param>
        /// <param name="writer">Writer that writes the report</param>
        /// <returns>true if there was at least one suspect comment</returns>
        public static bool Report(
            string path, IEnumerable<Inspection.Suspect> suspects, TextWriter writer)
        {
            bool hasSuspect = false;

            var matchedPatterns = new SortedSet<string>();

            foreach (Inspection.Suspect suspect in suspects)
            {
                if (!hasSuspect)
                {
                    hasSuspect = true;
                    writer.WriteLine($"FAIL {path}:");
                }

                if (suspect.Cues == null)
                {
                    throw new ArgumentException(
                        $"Unexpected null cues at the suspect comment starting at line index {suspect.Line} " +
                        $"and column index {suspect.Column}");
                }

                writer.WriteLine($"  * Comment starting at {suspect.Line + 1}:{suspect.Column + 1}:");
                foreach (var cue in suspect.Cues)
                {
                    writer.WriteLine(
                        $"    * Cue at {cue.Line + 1}:{cue.Column + 1}: {DescribeCharacteristic(cue.Characteristic)}");

                    if (cue.Characteristic is Inspection.Matches m)
                    {
                        matchedPatterns.Add(m.Identifier);
                    }
                }
            }

            if (matchedPatterns.Count > 0)
            {
                writer.WriteLine("The matched patterns were:");
                foreach (var identifier in matchedPatterns)
                {
                    Regex regex = Inspection.CodeRegexes[identifier];
                    writer.WriteLine($"  * \"{identifier}\": {regex}");
                }
            }

            if (!hasSuspect)
            {
                writer.WriteLine($"OK   {path}");
            }

            return hasSuspect;
        }
    }
}