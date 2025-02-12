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
using DsWebServer;
using Microsoft.Identity.Client;
using System.Runtime.CompilerServices;
using Azure.Core;
using Azure;
using DsWebServer.Managers;
using DsWebServer.Packets;
using DsWebServer.Model;
using DsWebServer.Utils;
using DsWebServer.Controllers;

namespace DsWebServer
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

    

    //public class DatabaseSettingInfo
    //{
    //    public string Ip { get; set; }
    //    public string Port { get; set; }
    //    public string DatabaseName { get; set; }
    //    public string UserName { get; set; }
    //    public string Password { get; set; }
    //    public string MaxPoolSize { get; set; }
    //}

    //public class SqlServerDbSettings
    //{
    //    public Dictionary<string, DatabaseSettingInfo> ConnectInfos { get; set; }
    //}
    
}
