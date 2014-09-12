using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using JetBrains.Annotations;

namespace BasicSocketUsage
{
    internal interface IEnableLog
    {
    }

    static class LoggerExtensions
    {
        private static readonly ConsoleLogger Logger = new ConsoleLogger();

        public static ConsoleLogger Log(this IEnableLog self)
        {
            return Logger;
        }
    }

    public class ConsoleLogger
    {
        private static readonly int ProcessId = Process.GetCurrentProcess().Id;
        public void Flush(TimeSpan? maxTimeToWait = null) {}

        [StringFormatMethod("format")]
        public void Fatal(string format, params object[] args)
        {
            Console.WriteLine(Log("FATAL", format, args));
        }

        [StringFormatMethod("format")]
        public void Error(string format, params object[] args)
        {
            Console.WriteLine(Log("ERROR", format, args));
        }

        [StringFormatMethod("format")]
        public void Info(string format, params object[] args)
        {
            Console.WriteLine(Log("INFO ", format, args));
        }

        [StringFormatMethod("format")]
        public void Debug(string format, params object[] args)
        {
            Console.WriteLine(Log("DEBUG", format, args));
        }

        [StringFormatMethod("format")]
        public void Trace(string format, params object[] args)
        {
            Console.WriteLine(Log("TRACE", format, args));
        }

        [StringFormatMethod("format")]
        public void FatalException(Exception exc, string format, params object[] args)
        {
            Console.WriteLine(Log("FATAL", exc, format, args));
        }

        [StringFormatMethod("format")]
        public void ErrorException(Exception exc, string format, params object[] args)
        {
            Console.WriteLine(Log("ERROR", exc, format, args));
        }

        [StringFormatMethod("format")]
        public void InfoException(Exception exc, string format, params object[] args)
        {
            Console.WriteLine(Log("INFO ", exc, format, args));
        }

        [StringFormatMethod("format")]
        public void DebugException(Exception exc, string format, params object[] args)
        {
            Console.WriteLine(Log("DEBUG", exc, format, args));
        }

        [StringFormatMethod("format")]
        public void TraceException(Exception exc, string format, params object[] args)
        {
            Console.WriteLine(Log("TRACE", exc, format, args));
        }

        [StringFormatMethod("format")]
        private static string Log(string level, string format, params object[] args)
        {
            return string.Format("[{0:00000},{1:00},{2:HH:mm:ss.fff},{3}] {4}",
                                 ProcessId,
                                 Thread.CurrentThread.ManagedThreadId,
                                 DateTime.UtcNow,
                                 level,
                                 args.Length == 0 ? format : string.Format(format, args));
        }

        [StringFormatMethod("format")]
        private static string Log(string level, Exception exc, string format, params object[] args)
        {
            var sb = new StringBuilder();
            while (exc != null)
            {
                sb.AppendLine();
                sb.AppendLine(exc.ToString());
                exc = exc.InnerException;
            }

            return string.Format("[{0:00000},{1:00},{2:HH:mm:ss.fff},{3}] {4}\nEXCEPTION(S) OCCURRED:{5}",
                                 ProcessId,
                                 Thread.CurrentThread.ManagedThreadId,
                                 DateTime.UtcNow,
                                 level,
                                 args.Length == 0 ? format : string.Format(format, args),
                                 sb);
        }
    }
}