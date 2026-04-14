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

            var app = builder.Build();

            app.UseStaticFiles();

            if (!ImageDirectoryExists(uploadPath)) Directory.CreateDirectory(uploadPath);
            PrintAdress();

            app.MapGet("/images", async () => await GetImagesAsync(uploadPath));

            app.MapGet("/", async (context) => await LoadPageAsync(context.Response, app));

            app.MapPost("/upload", async (context) => await UploadImageAsync(context.Request, context.Response));

            app.MapFallback(async (context) =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.StatusCode = 404;
                await context.Response.SendFileAsync(Path.Combine(app.Environment.ContentRootPath,"wwwroot","not-found.html"));
            });

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

        static bool ImageDirectoryExists(string directoryPath) => Directory.Exists(directoryPath);

        static async Task UploadImageAsync(HttpRequest request, HttpResponse response)
        {
            if (!request.HasFormContentType)
            {
                response.StatusCode = 400;
                await response.WriteAsync("Нет данных форм");
                return;
            }

            response.ContentType = "text/html; charset=utf-8";
            var form = await request.ReadFormAsync();
            var files = form.Files;

            if (!files.Any())
            {
                response.StatusCode = 400;
                await response.WriteAsync("Нет файлов");
                return;
            }

            string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".webp" ];
            var savedImagesCount = 0;

            foreach (var image in files)
            {
                var extention = Path.GetExtension(image.FileName).ToLower();
                if (!allowedExtensions.Contains(extention))
                {
                    continue;  
                }

                var imageName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";

                var imagePath = Path.Combine(uploadPath, imageName);

                using (var fileStream = new FileStream(imagePath, FileMode.Create))
                {
                    await image.CopyToAsync(fileStream);
                    savedImagesCount++;
                }

            }

            response.StatusCode = 200;
            await response.WriteAsync($"{savedImagesCount} изображений загружено по пути {uploadPath}");
        }

        static async Task LoadPageAsync(HttpResponse response, WebApplication app)
        {
            response.ContentType = "text/html; charset=utf-8";

            await response.SendFileAsync(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "main.html"));
        }

        //антипаттерн, переделать в синхронный метод
        static async Task<string[]> GetImagesAsync(string path) =>
            await Task.Run(() => 
            {
                var filePaths = Directory.GetFiles(path)
                    .Where(p => p != null)
                    .Select(p => "/images/" + Path.GetFileName(p))
                    .ToArray();

                return filePaths;
            });

    }
}
