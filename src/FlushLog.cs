using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;

namespace ParaPie
{
    /// <summary>
    /// Writes this mod's log lines to BepInEx/ParaPie.log and flushes every line, so nothing is
    /// lost if the game is force-closed (BepInEx's own LogOutput.log is buffered).
    /// </summary>
    internal sealed class FlushLog : ILogListener
    {
        private readonly ManualLogSource _source;
        private readonly StreamWriter _writer;

        public FlushLog(ManualLogSource source)
        {
            _source = source;
            var path = Path.Combine(Paths.BepInExRootPath, "ParaPie.log");
            _writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            _writer.WriteLine("ParaPie log " + DateTime.Now);
        }

        public void LogEvent(object sender, LogEventArgs e)
        {
            if (!ReferenceEquals(e.Source, _source)) return;
            try { _writer.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " [" + e.Level + "] " + e.Data); } catch { }
        }

        public void Dispose()
        {
            try { _writer.Dispose(); } catch { }
        }
    }
}
