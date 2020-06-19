using ArgumentException = System.ArgumentException;
using System.Collections.Generic;

namespace DeadCsharp
{

    public static class Output
    {
        /// <summary>
        /// This class is necessary so that we can pass an output variable by reference to
        /// <see cref="Report"/>.
        /// </summary>
        public class HasSuspect
        {
            public bool Value;
        }

        /// <summary>
        /// Generates the report based on the suspect comments.
        /// </summary>
        /// <param name="path">Path to the inspected file</param>
        /// <param name="suspects">Suspect comments</param>
        /// <param name="hasSuspect">Output reference since generators can have no out arguments</param>
        /// <returns>stream of report lines</returns>
        public static IEnumerable<string> Report(string path, IEnumerable<Inspection.Suspect> suspects,
            HasSuspect hasSuspect)
        {
            hasSuspect.Value = false;

            foreach (Inspection.Suspect suspect in suspects)
            {
                if (!hasSuspect.Value)
                {
                    hasSuspect.Value = true;
                    yield return $"FAIL {path}:";
                }

                if (suspect.Cues == null)
                {
                    throw new ArgumentException(
                        $"Unexpected null cues at the suspect with line index {suspect.Line} " +
                        $"and column index {suspect.Column}");
                }

                yield return $"  * {suspect.Line + 1}:{suspect.Column + 1}: {string.Join("; ", suspect.Cues)}";
            }

            if (!hasSuspect.Value)
            {
                yield return $"OK   {path}";
            }
        }
    }
}