namespace DsWebServer.Managers
{
    public static class LogManager
    {
        private static string _logDirectory = "Logs";
        public enum LogLevel { Debug, Info, Warning, Error, Fatal }

        static LogManager()
        {
            if (!Directory.Exists(_logDirectory))
                Directory.CreateDirectory(_logDirectory);
        }

        public static void Log(string message, LogLevel level = LogLevel.Debug)
        {
            string logFilePath = Path.Combine(_logDirectory, $"log_{DateTime.UtcNow:yyyyMMdd HHmmss}.txt");
            string logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
            Console.WriteLine(logEntry);
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
        }
    }
}
