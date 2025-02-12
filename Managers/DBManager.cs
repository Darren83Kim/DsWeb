using Dapper;
using DsWebServer.Model;
using DsWebServer.Utils;
using Microsoft.Data.SqlClient;

namespace DsWebServer.Managers
{
    public class DBManager
    {
        protected static string _connectionString;

        public static void Initialize(IConfiguration configuration)
        {
            var dbSettings = configuration.GetSection("SqlServerDbSettings:ConnectionInfos:CommonDbService");
            var ip = dbSettings["Ip"];
            var port = dbSettings["Port"];
            var dbName = dbSettings["DatabaseName"];
            var userId = dbSettings["UserId"];
            var password = dbSettings["Password"];
            var maxPoolSize = dbSettings["MaxPoolSize"];

            if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(port) || string.IsNullOrEmpty(dbName) ||
                string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(password))
            {
                LogManager.Log("[DBManager] Database connection settings are missing or invalid.", LogManager.LogLevel.Error);
                throw new Exception("Database connection settings are missing or invalid.");
            }

            _connectionString = $"Server={ip},{port};Database={dbName};User Id={userId};Password={password};Max Pool Size={maxPoolSize};Encrypt=False;TrustServerCertificate=True;";

            Console.WriteLine($"[DBManager] Connection String: {_connectionString}");

            //_connectionString = $"Server={ip},{port};database={dbName};uid={userId};pwd={password};Max Pool Size={maxPoolSize};TrustServerCertificate=True;";
            //_connectionString = $"Server={configuration["Database:Ip"]},{configuration["Database:Port"]};Database={configuration["Database:DatabaseName"]};User Id={configuration["Database:UserName"]};Password={configuration["Database:Password"]};";
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public static async Task<T?> ExecuteQuery<T>(string query, DynamicParameters parameters)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                LogManager.Log($"Executing Query: {query} {string.Join(", ", parameters.ParameterNames.ToList())}", LogManager.LogLevel.Info);
                return await connection.QueryFirstOrDefaultAsync<T>(query, parameters, commandType: System.Data.CommandType.StoredProcedure);
            }
            catch (Exception ex)
            {
                throw new GameException(ResultCode.DBError, $"Failed to execute query: {query}, {ex.Message}");
            }

        }

        public static async Task<bool> ExecuteQuery(string query, DynamicParameters parameters)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                LogManager.Log($"Executing Query: {query} {string.Join(", ", parameters.ParameterNames.ToList())}", LogManager.LogLevel.Info);
                var result = await connection.ExecuteAsync(query, parameters, commandType: System.Data.CommandType.StoredProcedure);
                return result > 0;
            }
            catch (Exception ex)
            {
                throw new GameException(ResultCode.DBError, $"Failed to execute query: {query}, {ex.Message}");
            }

        }
    }
}
