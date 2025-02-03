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
                endpoints.MapPost("/", async context =>
                {
                    var controller = new SessionController();
                    await controller.HandleRequest(context);
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
        public string UserID { get; set; }
        public string UserPass { get; set; }
        public int UserPoint { get; set; }
        public int MaxScore { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime LetestDate { get; set; } = DateTime.Now;
    }

    public class DBManager
    {
        protected static string _connectionString;

        public static void Initialize(IConfiguration configuration)
        {
            _connectionString = $"Server={configuration["Database:Ip"]},{configuration["Database:Port"]};Database={configuration["Database:DatabaseName"]};User Id={configuration["Database:UserId"]};Password={configuration["Database:Password"]};";
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }

    public class GameDBManager : DBManager
    {
        public static async Task<bool> AccountExistsAsync(string accountId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("EXEC CheckAccountExistence @AccountID", connection);
            command.Parameters.AddWithValue("@AccountID", accountId);

            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            return result != null;
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
        public ReqLogin() : base("api/auth/Login") { }

        public string UserId { get; set; }
        public string UserPass { get; set; }
    }

    public class ReqLogOut : BaseRequest
    {
        public ReqLogOut() : base("api/auth/LogOut") { }
    }
    public class ReqCreateUser : BaseRequest
    {
        public ReqCreateUser() : base("api/user/CreateUser") { }

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

            // Check Redis for existing session
            var existingSession = await RedisManager.GetSessionAsync(request.UserId);

            if (!string.IsNullOrEmpty(existingSession))
            {
                response.Status = "error";
                response.Message = "User is already logged in.";
                response.ProcessRet = -1;
                return response;
            }

            var accountExists = await GameDBManager.AccountExistsAsync(request.UserId);
            //if (!string.IsNullOrEmpty(accountExists))
            //{
            //    response.Status = "error";
            //    response.Message = "User does not exist.";
            //    response.ProcessRet = -1;
            //    return response;
            //}

            // Create new session in Redis
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
