using StringWriter = System.IO.StringWriter;

using System.Collections.Generic;

using NUnit.Framework;

namespace DeadCsharp.Test
{
    public class ReportTests
    {
        [Test]
        public void TestNoSuspects()
        {
            string path = "/some/Program.cs";

            using var writer = new StringWriter();
            bool hasSuspect = Output.Report(path, new List<Inspection.Suspect>(), writer);

            string nl = System.Environment.NewLine;

            Assert.IsFalse(hasSuspect);
            Assert.AreEqual($"OK   /some/Program.cs{nl}", writer.ToString());
        }

        [Test]
        public void TestOneSuspectOneCue()
        {
            string path = "/some/Program.cs";
            var suspects = new List<Inspection.Suspect>
            {
                new Inspection.Suspect(
                    2, 1,
                    new List<Inspection.Cue>
                    {
                        new Inspection.Cue(new Inspection.Contains( "something"), 3, 4)
                    })
            };

            string nl = System.Environment.NewLine;
            using var writer = new StringWriter();
            bool hasSuspect = Output.Report(path, suspects, writer);

            Assert.IsTrue(hasSuspect);
            Assert.AreEqual(
                $"FAIL /some/Program.cs:{nl}" +
                $"  * Comment starting at 3:2:{nl}" +
                $"    * Cue at 4:5: a line contains `something`{nl}",
                writer.ToString());
        }

        [Test]
        public void TestOneSuspectMultipleCues()
        {
            string path = "/some/Program.cs";
            var suspects = new List<Inspection.Suspect>
            {
                new Inspection.Suspect(
                    2, 1,
                    new List<Inspection.Cue>
                    {
                        new Inspection.Cue(
                            new Inspection.Contains("some feature"), 3, 4),
                        new Inspection.Cue(
                            new Inspection.Contains("another feature"), 5, 6)
                    })
            };

            string nl = System.Environment.NewLine;

            using var writer = new StringWriter();
            bool hasSuspect = Output.Report(path, suspects, writer);

            Assert.IsTrue(hasSuspect);
            Assert.AreEqual(
                $"FAIL /some/Program.cs:{nl}" +
                $"  * Comment starting at 3:2:{nl}" +
                $"    * Cue at 4:5: a line contains `some feature`{nl}" +
                $"    * Cue at 6:7: a line contains `another feature`{nl}",
                writer.ToString());
        }

        [Test]
        public void TestMultipleSuspects()
        {
            string path = "/some/Program.cs";
            var suspects = new List<Inspection.Suspect>()
            {
                new Inspection.Suspect(
                    2, 1,
                    new List<Inspection.Cue>
                    {
                        new Inspection.Cue(
                            new Inspection.Contains("some feature"), 3, 4)
                    }),
                new Inspection.Suspect(
                    12, 1,
                    new List<Inspection.Cue>
                    {
                        new Inspection.Cue(
                            new Inspection.Contains("another feature"), 13, 14)
                    })
            };

            string nl = System.Environment.NewLine;

            using var writer = new StringWriter();
            bool hasSuspect = Output.Report(path, suspects, writer);

            Assert.IsTrue(hasSuspect);
            Assert.AreEqual(
                $"FAIL /some/Program.cs:{nl}" +
                $"  * Comment starting at 3:2:{nl}" +
                $"    * Cue at 4:5: a line contains `some feature`{nl}" +
                $"  * Comment starting at 13:2:{nl}" +
                $"    * Cue at 14:15: a line contains `another feature`{nl}",
                writer.ToString());
        }
    }
}