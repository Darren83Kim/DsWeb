using DsWebServer.Managers;
using DsWebServer.Model;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DsWebServer.Utils
{
    public class GameException : Exception
    {
        public override string Message { get; }
        public ResultCode ResultCode;

        public GameException(ResultCode resultCode, string message, [CallerMemberName] string method = null, [CallerLineNumber] int lineNumber = 0)
        {
            var errorMessage = new Dictionary<string, string>();
            errorMessage.Add("message", message);
            errorMessage.Add("method", method);
            errorMessage.Add("lineNumber", lineNumber.ToString());

            Message = JsonSerializer.Serialize(errorMessage);
            ResultCode = resultCode;

            LogManager.Log($"[Exception] [{Message}], retCode:[{ResultCode}]", LogManager.LogLevel.Fatal);
        }
    }
}
