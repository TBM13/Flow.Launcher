using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Flow.Launcher.Infrastructure.Logger
{
    public static class Log
    {
        public static void Exception(string className, string message, System.Exception exception, [CallerMemberName] string methodName = "")
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("EXCEPTION");
            exception = exception.Demystify();
            ExceptionDispatchInfo.Capture(exception).Throw();
#endif
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

        public static void Error(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LOGLEVEL.ERROR, className, message, methodName);
        }

        private static void LogInternal(LOGLEVEL level, string className, string message, [CallerMemberName] string methodName = "")
        {
#if DEBUG
            var classNameWithMethod = CheckClassAndMessageAndReturnFullClassWithMethod(className, message, methodName);
            System.Diagnostics.Debug.WriteLine($"[{level}] {classNameWithMethod}: {message}");
#endif
        }

        public static void Debug(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LOGLEVEL.DEBUG, className, message, methodName);
        }

        public static void Info(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LOGLEVEL.INFO, className, message, methodName);
        }

        public static void Warn(string className, string message, [CallerMemberName] string methodName = "")
        {
            LogInternal(LOGLEVEL.WARN, className, message, methodName);
        }
    }

    public enum LOGLEVEL
    {
        DEBUG,
        INFO,
        WARN,
        ERROR
    }
}
