using ImageServer.Database;
using ImageServer.Services;
using SixLabors.ImageSharp;
using System.Net;
using System.Net.Sockets;

namespace ImageServer
{
    //в дальнейшем можно добавть обработчик, чтобы можно было выбирать способ отображения изображений 
    public class Program
    {
        static string uploadPath = Path.Combine(Directory.GetCurrentDirectory(),"wwwroot");

        public static void Main()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 100_000_000;
            });
            builder.Services.AddSingleton<IImageProcessor>(processor => new ImageProcessor());
            builder.Services.AddSingleton<IStorage>(storage => new LocalRepository(uploadPath));
            builder.Services.AddDbContext<AppDBContext>();
            builder.Services.AddScoped<ImageService>();

            var app = builder.Build();
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
