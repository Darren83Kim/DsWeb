using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using System;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using System.Data;
using SimpleFramework;
using Microsoft.Identity.Client;
using System.Runtime.CompilerServices;

namespace SimpleFramework
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            // Initialize RedisManager and DBManager
            RedisManager.Initialize(host.Services.GetRequiredService<IConnectionMultiplexer>());
            DBManager.Initialize(host.Services.GetRequiredService<IConfiguration>());

            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();

                    // 포트를 appsettings.json에서 읽어 설정
                    var configuration = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        .Build();
                    var port = configuration.GetValue<int>("Server:Port", 5000); // 기본값 5000
                    webBuilder.UseUrls($"http://*:{port}");
                });
    }

    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Add Redis connection multiplexer as singleton
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(Configuration["Redis:ConnectionString"]));

            // Add Redis cache for session handling
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = Configuration["Redis:ConnectionString"];
            });

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseSession();


            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapPost("api/{controller}", async context =>
                {
                    var controllerName = context.Request.RouteValues["controller"]?.ToString();

                    if (controllerName != null)
                    {
                        var controllerType = Type.GetType($"SimpleFramework.{controllerName}Controller");
                        if (controllerType != null)
                        {
                            var controller = Activator.CreateInstance(controllerType);
                            var method = controllerType.GetMethod("HandleRequest");

                            if (method != null)
                            {
                                await (Task)method.Invoke(controller, new object[] { context });
                                return;
                            }
                        }
                    }

                    context.Response.StatusCode = 404;
                });
            });
        }
    }

    public class RedisManager
    {
        private static IConnectionMultiplexer _redis;

        public static void Initialize(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public static async Task<string> GetSessionAsync(string key)
        {
            var db = _redis.GetDatabase();
            return await db.StringGetAsync(key);
        }

        public static async Task SetSessionAsync(string key, string value, TimeSpan expiry)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(key, value, expiry);
        }

        public static async Task DeleteSessionAsync(string key)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
    }

    public class UserDBInfo
    {
        public long UserID { get; set; }
        public string UserName { get; set; }
        public string UserPass { get; set; }
        public int UserPoint { get; set; }
        public int MaxScore { get; set; }
        public DateTime LetestDate { get; set; } = DateTime.Now;
        public DateTime CreateDate { get; set; }
    }

    public class DatabaseSettingInfo
    {
        public string Ip { get; set; }
        public string Port { get; set; }
        public string DatabaseName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string MaxPoolSize { get; set; }
    }

    public class SqlServerDbSettings
    {
        public Dictionary<string, DatabaseSettingInfo> ConnectInfos { get; set; }
    }

    public class DBManager
    {
        protected static string _connectionString;

        public static void Initialize(IConfiguration configuration)
        {
            var dbSettings = configuration.GetSection("SqlServerDbSettings:ConnectionInfos:CommonDbService");
            var ip = dbSettings["Ip"];
            var port = dbSettings["Port"];
            var dbName = dbSettings["DatabaseName"];
            var userId = dbSettings["UserName"];
            var password = dbSettings["Password"];
            var maxPoolSize = dbSettings["MaxPoolSize"];

            _connectionString = $"Server={ip},{port};Database={dbName};User Id={userId};Password={password};Max Pool Size={maxPoolSize};TrustServerCertificate=True;";


            //_connectionString = $"Server={configuration["Database:Ip"]},{configuration["Database:Port"]};Database={configuration["Database:DatabaseName"]};User Id={configuration["Database:UserName"]};Password={configuration["Database:Password"]};";
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public static async Task<T?> ExecuteQuery<T>(string query, DynamicParameters parameters)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            return await connection.QueryFirstOrDefaultAsync<T>(query, parameters, commandType: System.Data.CommandType.StoredProcedure);
        }

        public static async Task<bool> ExecuteQuery(string query, DynamicParameters parameters)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            var result = await connection.ExecuteAsync(query, parameters, commandType: System.Data.CommandType.StoredProcedure);
            return result > 0;
        }
    }

    public class GameDBManager : DBManager
    {
        public static async Task<UserDBInfo?> SelectUserInfo(string userName)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@UserName", userName, DbType.String, size: 20);

            return await ExecuteQuery<UserDBInfo>("SELECT_USER_INFO", parameters);
        }

        public static async Task<bool> InsertUserInfo(string userName, string userPass)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@UserName", userName, DbType.String, size: 20);
            parameters.Add("@UserPass", userPass, DbType.String, size: 20);

            return await ExecuteQuery("INSERT_USER_INFO", parameters);
        }
    }

    public class Session
    {
        public string Token { get; set; }
        public string Id { get; set; }
        public string Sequence { get; set; }
    }

    public class BaseRequest
    {
        public string Token { get; set; } = string.Empty;
        public int Sequence { get; set; } = 0;
        public string Url { get; set; }

        public BaseRequest(string url)
        {
            Url = url;
        }
    }

    public class ReqLogin : BaseRequest
    {
        public ReqLogin() : base("api/Login") { }

        public string UserName { get; set; }
        public string UserPass { get; set; }
    }

    public class ReqLogOut : BaseRequest
    {
        public ReqLogOut() : base("api/LogOut") { }
    }
    public class ReqCreateUser : BaseRequest
    {
        public ReqCreateUser() : base("api/CreateUser") { }

        public string UserName { get; set; }
        public string UserPass { get; set; }
    }

    public class BaseResponse
    {
        public int ResultCode { get; set; } = 0;
    }

    public class ResLogin : BaseResponse
    {
        public string Token { get; set; }
    }

    public class ResLogOut : BaseResponse
    {
    }

    public class ResCreateUser : BaseResponse
    {
    }

    public enum ResultCode
    {
        Success = 0,
        Fail    = -1,

        UserAlreadyLoggedIn  = -100,
        UserNotFindDBInfo    = -101,
        UserAlreadyExistInfo = -102,
    }

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
        }
    }

    public abstract class BaseController<TRequest, TResponse>
        where TRequest : BaseRequest
        where TResponse : BaseResponse, new()
    {
        public abstract Task<TResponse> Process(TRequest request);

        public TResponse SendResponse(TResponse response, ResultCode resultCode = ResultCode.Success)
        {
            response.ResultCode = (int)resultCode;
            return response;
        }

        public async Task HandleRequest(HttpContext context)
        {
            try
            {
                var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
                var request = JsonSerializer.Deserialize<TRequest>(requestBody);

                if (request == null)
                {
                    var errorResponse = new TResponse
                    {
                        ResultCode = -1
                    };
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(errorResponse);
                    return;
                }

                var response = await Process(request);
                await context.Response.WriteAsJsonAsync(response);
            }
            catch (Exception ex)
            {
                var errorResponse = new TResponse
                {
                    ResultCode = -1
                };
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(errorResponse);
            }
        }
    }

    public class SessionController : BaseController<BaseRequest, BaseResponse>
    {
        public override async Task<BaseResponse> Process(BaseRequest request)
        {
            var response = new BaseResponse();
            //{
            //    ResultCode = 0
            //};

            return await Task.FromResult(response);
        }
    }

    public class LoginController : BaseController<ReqLogin, ResLogin>
    {
        public override async Task<ResLogin> Process(ReqLogin request)
        {
            var response = new ResLogin();

            var existingSession = await RedisManager.GetSessionAsync(request.UserName);

            if (!string.IsNullOrEmpty(existingSession))
                return SendResponse(response, ResultCode.UserAlreadyLoggedIn);

            var userInfo = await GameDBManager.SelectUserInfo(request.UserName);
            if (userInfo == null)
                return SendResponse(response, ResultCode.UserNotFindDBInfo);

            var token = Guid.NewGuid().ToString();
            await RedisManager.SetSessionAsync(request.UserName, token, TimeSpan.FromMinutes(30));
            
            response.Token = token;

            return SendResponse(response);
        }
    }

    public class CreateUser : BaseController<ReqCreateUser, ResCreateUser>
    {
        public override async Task<ResCreateUser> Process(ReqCreateUser request)
        {
            var response = new ResCreateUser();
            var existingSession = await RedisManager.GetSessionAsync(request.UserName);
            if (!string.IsNullOrEmpty(existingSession))
                return SendResponse(response, ResultCode.UserAlreadyLoggedIn);

            var userInfo = await GameDBManager.SelectUserInfo(request.UserName);
            if (userInfo == null)
            {
                if (!await GameDBManager.InsertUserInfo(request.UserName, request.UserPass))
                    throw new GameException(ResultCode.Fail, "Failed to insert user info");

                userInfo = await GameDBManager.SelectUserInfo(request.UserName);
            }
            else
                return SendResponse(response, ResultCode.UserAlreadyExistInfo);


            var token = Guid.NewGuid().ToString();
            await RedisManager.SetSessionAsync(request.UserName, token, TimeSpan.FromMinutes(30));
            return SendResponse(response);
        }
    }

    public class LogOutController : BaseController<ReqLogOut, ResLogOut>
    {
        public override async Task<ResLogOut> Process(ReqLogOut request)
        {
            var response = new ResLogOut
            {
                //Status = "success",
                //Message = "Logout successful"
            };

            return await Task.FromResult(response);
        }
    }
}
