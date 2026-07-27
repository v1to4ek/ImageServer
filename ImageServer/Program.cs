using ImageServer.Abstractions;
using ImageServer.Configuration;
using ImageServer.Database;
using ImageServer.Services;
using ImageServer.Services.Processors;
using ImageServer.Services.Storages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using System.Net;
using System.Net.Sockets;

namespace ImageServer
{
    public class Program
    {
        public static void Main()
        {
            var builder = WebApplication.CreateBuilder();

            builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 100_000_000);

            builder.AddConfigOption<StorageOptions>();
            builder.AddConfigOption<DatabaseOptions>();
            builder.AddConfigOption<ImgServiceOptions>();
            builder.AddConfigOption<DeletionOptions>();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddSingleton<IImageProcessor, ImageProcessor>();
            builder.Services.AddSingleton(serviceProvider => new ImageConversionProcessor());
            builder.Services.AddSingleton(serviceProvider => new PreviewConversionProcessor(300, 300));
            builder.Services.AddSingleton<ExtentionValidationProcessor>();

            builder.Services.AddSingleton<LocalStorage>();
            builder.Services.AddSingleton<IStorage>(serviceProvider => new ConcurrentStorage(serviceProvider.GetRequiredService<LocalStorage>()));

            var DbOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>();
            builder.Services.AddDbContext<AppDBContext>(options => options.UseNpgsql(DbOptions!.ConnectionString));

            builder.Services.AddScoped<ImageService>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var serviceProvider = scope.ServiceProvider;

                var DBContext = serviceProvider.GetRequiredService<AppDBContext>();

                if (!DBContext.Database.CanConnect()) throw new Exception("БД недоступна при старте");
            }

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
    