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
        public string UserId { get; set; }
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
            var userId = dbSettings["UserId"];
            var password = dbSettings["Password"];
            var maxPoolSize = dbSettings["MaxPoolSize"];

            _connectionString = $"Server={ip},{port};Database={dbName};User Id={userId};Password={password};Max Pool Size={maxPoolSize};TrustServerCertificate=True;";


            //_connectionString = $"Server={configuration["Database:Ip"]},{configuration["Database:Port"]};Database={configuration["Database:DatabaseName"]};User Id={configuration["Database:UserId"]};Password={configuration["Database:Password"]};";
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }

    public class GameDBManager : DBManager
    {
        public static async Task<UserDBInfo> AccountExistsAsync(string userName)
        {
            using var connection = new SqlConnection(_connectionString);
            var parameters = new DynamicParameters();
            parameters.Add("@UserName", userName, DbType.String, size: 20);

            await connection.OpenAsync();
            var userInfo = await connection.QueryFirstOrDefaultAsync<UserDBInfo>("SELECT_USER_INFO", parameters, commandType: System.Data.CommandType.StoredProcedure);

            return userInfo;
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

        public string UserId { get; set; }
        public string UserPass { get; set; }
    }

    public class ReqLogOut : BaseRequest
    {
        public ReqLogOut() : base("api/LogOut") { }
    }
    public class ReqCreateUser : BaseRequest
    {
        public ReqCreateUser() : base("api/CreateUser") { }

        public string UserId { get; set; }
        public string UserPass { get; set; }
    }

    public class BaseResponse
    {
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class ResLogin : BaseResponse
    {
        public string Token { get; set; }
        public int ProcessRet { get; set; } = 0;
    }

    public class ResLogOut : BaseResponse
    {
    }

    public class ResCreateUser : BaseResponse
    {
        public int ProcessRet { get; set; } = 0;
    }

    public abstract class BaseController<TRequest, TResponse>
        where TRequest : BaseRequest
        where TResponse : BaseResponse, new()
    {
        public abstract Task<TResponse> Process(TRequest request);

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
                        Status = "error",
                        Message = "Invalid request format"
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
                    Status = "error",
                    Message = ex.Message
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
            var response = new BaseResponse
            {
                Status = "success",
                Message = "Session processed"
            };

            return await Task.FromResult(response);
        }
    }

    public class LoginController : BaseController<ReqLogin, ResLogin>
    {
        public override async Task<ResLogin> Process(ReqLogin request)
        {
            var response = new ResLogin();

            var existingSession = await RedisManager.GetSessionAsync(request.UserId);

            if (!string.IsNullOrEmpty(existingSession))
            {
                response.Status = "error";
                response.Message = "User is already logged in.";
                response.ProcessRet = -1;
                return response;
            }

            var userInfo = await GameDBManager.AccountExistsAsync(request.UserId);

            if (userInfo == null)
            {
                response.Status = "error";
                response.Message = "Account does not exist.";
                response.ProcessRet = -1;
                return response;
            }

            var token = Guid.NewGuid().ToString();
            await RedisManager.SetSessionAsync(request.UserId, token, TimeSpan.FromMinutes(30));

            response.Status = "success";
            response.Message = "Login successful.";
            response.Token = token;

            return response;
        }
    }

    public class LogOutController : BaseController<ReqLogOut, ResLogOut>
    {
        public override async Task<ResLogOut> Process(ReqLogOut request)
        {
            var response = new ResLogOut
            {
                Status = "success",
                Message = "Logout successful"
            };

            return await Task.FromResult(response);
        }
    }
}
