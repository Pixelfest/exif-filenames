using System;

namespace ExifFilenames
{
    public enum LogLevel
    {
        Trace,
        Info,
        Warning,
        Error,
        Success
    }

    public static class Log
    {
        public static LogLevel LogLevel { get; set; } = LogLevel.Info;

        private static string Timestamp => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        public static void Trace(string message)
        {
            if (LogLevel > LogLevel.Trace)
                return;

            Write(ConsoleColor.Cyan, message);
        }

        public static void Info(string message)
        {
            if (LogLevel > LogLevel.Info)
                return;

            Write(ConsoleColor.White, message);
        }


        public static void Info()
        {
            if (LogLevel > LogLevel.Info)
                return;

            Write(ConsoleColor.White, string.Empty);
        }

        public static void Warning(string message)
        {
            if (LogLevel > LogLevel.Warning)
                return;

            Write(ConsoleColor.Yellow, message);
        }

        public static void Error(string message)
        {
            if (LogLevel > LogLevel.Error)
                return;

            Write(ConsoleColor.Red, message);
        }

        public static void Success(string message)
        {
            Write(ConsoleColor.Green, message);
        }

        private static void Write(ConsoleColor color, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                Console.WriteLine();
                return;
            }

            var old = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine($"[{Timestamp}] {message}");
            }
            finally
            {
                Console.ForegroundColor = old;
            }
        }
    }
}
