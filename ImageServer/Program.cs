using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace ImageServer
{
    public class Program
    {
        static string uploadPath = Path.Combine(Directory.GetCurrentDirectory(),"wwwroot" ,"images");

        public static void Main()
        {
            var builder = WebApplication.CreateBuilder();

            builder.Services.AddSingleton<IStorage>(service => new ImageStorage(uploadPath));
            builder.Services.AddSingleton<ImageService>();

            var app = builder.Build();
            app.UseStaticFiles();
            app.UseImageEndpoints();
            app.UseApplicationEndpoints();

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
