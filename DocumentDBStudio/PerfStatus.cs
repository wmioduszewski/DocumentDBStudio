using System;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Azure.DocumentDBStudio
{
    class PerfStatus : IDisposable
    {
        private readonly string _name;
        private readonly Stopwatch _watch;

        private PerfStatus(string name)
        {
            _name = name;
            _watch = new Stopwatch();
            _watch.Start();
        }

        #region IDisposable implementation

        // dispose stops stopwatch and prints time, could do anything here
        public void Dispose()
        {
            _watch.Stop();

            Program.GetMain()
                .SetStatus(string.Format(CultureInfo.InvariantCulture, "{0}: {1}ms", _name,
                    _watch.Elapsed.TotalMilliseconds));
        }

        #endregion

        public static PerfStatus Start(string name)
        {
            return new PerfStatus(name);
        }
    }
}