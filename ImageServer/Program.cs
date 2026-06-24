using ImageServer.Abstractions;
using ImageServer.Database;
using ImageServer.Services;
using ImageServer.Services.Processors;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace ImageServer
{
    public class Program
    {
        static readonly string uploadPath = Path.Combine(Directory.GetCurrentDirectory(),"wwwroot");

        public static void Main()
        {
            var builder = WebApplication.CreateBuilder();

            var configPath = "configuration.json";

            if (!File.Exists(configPath))
            {
                var defaultConfiguration = new
                {
                    ConnectionStrings = new
                    {
                        DBConnection = "Host=localhost;Port=5432;Database=ImageServerDB;Username=postgres;Password=postgres"
                    }
                };
                var configJson = JsonSerializer.Serialize(defaultConfiguration);
                File.WriteAllText(configPath, configJson);
            }

            builder.Configuration.AddJsonFile("configuration.json", true, true);

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 100_000_000;
            });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSingleton(strategy => new ImageConversionProcessor());
            builder.Services.AddSingleton(strategy => new PreviewConversionProcessor(300, 300));
            builder.Services.AddSingleton<ExtentionValidationProcessor>();
            builder.Services.AddSingleton<IImageProcessor, ImageProcessor>();
            builder.Services.AddSingleton<IStorage>(storage => new LocalRepository(uploadPath));
            builder.Services.AddDbContext<AppDBContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DBConnection")));
            builder.Services.AddScoped<ImageService>();

            var app = builder.Build();
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.AddApplicationEndpoints();
            app.AddImageAPI();

            PrintAdress();

            app.Run();
        }

        static void PrintAdress()
        {
            var hostName = Dns.GetHostName();
            var hostIPs = Dns.GetHostAddresses(hostName);
            var IPLists = hostIPs
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                .Select(ip => ip.ToString())
                .Where(ip => ip != "26.187.247.156")
                .ToList();


            foreach (var ip in IPLists)
            {
                Console.WriteLine($"Адрес для подключения: https://{ip}:7179");
            }
        }

    }
}
    