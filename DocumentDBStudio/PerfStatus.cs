using System;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Azure.DocumentDBStudio
{
    class PerfStatus : IDisposable
    {
        string name;
        Stopwatch watch;

        private PerfStatus(string name)
        {
            this.name = name;
            watch = new Stopwatch();
            watch.Start();
        }

        #region IDisposable implementation

        // dispose stops stopwatch and prints time, could do anytying here
        public void Dispose()
        {
            watch.Stop();

            Program.GetMain()
                .SetStatus(string.Format(CultureInfo.InvariantCulture, "{0}: {1}ms", name,
                    watch.Elapsed.TotalMilliseconds));
        }

        #endregion

        public static PerfStatus Start(string name)
        {
            return new PerfStatus(name);
        }
    }
}