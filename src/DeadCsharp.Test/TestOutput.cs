using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using NUnit.Framework;

namespace DeadCsharp.Test
{
    public class ReportTests
    {
        [Test]
        public void TestNoSuspects()
        {
            string path = "/some/Program.cs";

            var hasSuspect = new Output.HasSuspect();
            string nl = System.Environment.NewLine;
            string actual = string.Join(nl, Output.Report(
                path,
                new List<Inspection.Suspect>(),
                hasSuspect));

            Assert.That(!hasSuspect.Value);
            Assert.AreEqual($"OK   /some/Program.cs", actual);
        }

        [Test]
        public void TestOneSuspectOneCue()
        {
            string path = "/some/Program.cs";
            var suspects = new List<Inspection.Suspect>
            {
                new Inspection.Suspect(2, 1, new List<string>() {"some cue"})
            };

            var hasSuspect = new Output.HasSuspect();
            string nl = System.Environment.NewLine;
            string actual = string.Join(nl, Output.Report(path, suspects, hasSuspect));

            Assert.That(hasSuspect.Value);
            Assert.AreEqual($"FAIL /some/Program.cs:{nl}  * 3:2: some cue", actual);
        }

        [Test]
        public void TestOneSuspectMultipleCues()
        {
            string path = "/some/Program.cs";
            var suspects = new List<Inspection.Suspect>
            {
                new Inspection.Suspect(
                    2, 1, new List<string>() {"some cue", "another cue"})
            };

            var hasSuspect = new Output.HasSuspect();
            string nl = System.Environment.NewLine;
            string actual = string.Join(nl, Output.Report(path, suspects, hasSuspect));

            Assert.That(hasSuspect.Value);
            Assert.AreEqual($"FAIL /some/Program.cs:{nl}  * 3:2: some cue; another cue", actual);
        }

        [Test]
        public void TestMultipleSuspects()
        {
            string path = "/some/Program.cs";
            var suspects = new List<Inspection.Suspect>()
            {
                new Inspection.Suspect(2, 1, new List<string>() {"some cue"}),
                new Inspection.Suspect(4, 3, new List<string>() {"another cue"})
            };

            var hasSuspect = new Output.HasSuspect();
            string nl = System.Environment.NewLine;
            string actual = string.Join(nl, Output.Report(path, suspects, hasSuspect));

            Assert.That(hasSuspect.Value);
            Assert.AreEqual($"FAIL /some/Program.cs:{nl}  * 3:2: some cue{nl}  * 5:4: another cue", actual);
        }
    }
}