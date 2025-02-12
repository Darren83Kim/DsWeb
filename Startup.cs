
using StackExchange.Redis;
using DsWebServer.Packets;

namespace DsWebServer
{
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
                        var controllerType = Type.GetType($"DsWebServer.Controllers.{controllerName}Controller");
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
}
