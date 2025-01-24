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
            CreateHostBuilder(args).Build().Run();
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
            // Add Redis session management
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

            // Add MSSQL database connection
            services.AddTransient<SqlConnection>(_ =>
                new SqlConnection(Configuration.GetConnectionString("DefaultConnection")));

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
        private readonly IConnectionMultiplexer _redis;
        private readonly SqlConnection _sqlConnection;

        public LoginController(IConnectionMultiplexer redis, SqlConnection sqlConnection)
        {
            _redis = redis;
            _sqlConnection = sqlConnection;
        }

        public override async Task<ResLogin> Process(ReqLogin request)
        {
            var response = new ResLogin();

            // Check Redis for existing session
            var db = _redis.GetDatabase();
            var existingSession = await db.StringGetAsync(request.UserId);

            if (!string.IsNullOrEmpty(existingSession))
            {
                response.Status = "error";
                response.Message = "User is already logged in.";
                response.ProcessRet = -1;
                return response;
            }

            // Check MSSQL for user credentials
            var query = "SELECT COUNT(*) FROM UserInfo WHERE UserId = @UserId AND UserPass = @UserPass";
            using var command = new SqlCommand(query, _sqlConnection);
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@UserPass", request.UserPass);

            await _sqlConnection.OpenAsync();
            var userExists = (int)await command.ExecuteScalarAsync() > 0;
            await _sqlConnection.CloseAsync();

            if (!userExists)
            {
                response.Status = "error";
                response.Message = "Invalid UserId or UserPass.";
                response.ProcessRet = -1;
                return response;
            }

            // Create new session in Redis
            var token = Guid.NewGuid().ToString();
            await db.StringSetAsync(request.UserId, token, TimeSpan.FromMinutes(30));

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
