using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Flow.Launcher.Infrastructure.Logger
{
    public static class Log
    {
        public static void Exception(string className, string message, System.Exception exception, [CallerMemberName] string methodName = "")
        {
            Trace.WriteLine($"EXCEPTION: {methodName}@{className}: {message}\n\n{exception}");
        }

        private static string CheckClassAndMessageAndReturnFullClassWithMethod(string className, string message,
            string methodName)
        {
            if (!string.IsNullOrWhiteSpace(methodName))
            {
                return className + "." + methodName;
            }

            return className;
        }

        [Conditional("DEBUG")]
        public static void Error(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LOGLEVEL.ERROR, className, message, methodName);
        }

        [Conditional("DEBUG")]
        private static void LogInternal(LOGLEVEL level, string className, string message, [CallerMemberName] string methodName = "")
        {
            var classNameWithMethod = CheckClassAndMessageAndReturnFullClassWithMethod(className, message, methodName);
            System.Diagnostics.Debug.WriteLine($"[{level}] {classNameWithMethod}: {message}");
        }

        [Conditional("DEBUG")]
        public static void Debug(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LOGLEVEL.DEBUG, className, message, methodName);
        }

        [Conditional("DEBUG")]
        public static void Info(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LOGLEVEL.INFO, className, message, methodName);
        }

        [Conditional("DEBUG")]
        public static void Warn(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LOGLEVEL.WARN, className, message, methodName);
        }
    }

    public enum LOGLEVEL
    {
        NONE,
        ERROR,
        WARN,
        INFO,
        DEBUG
    }
}
