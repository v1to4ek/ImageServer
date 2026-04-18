using System.Net;

namespace ImageServer
{
    public static class Endpoints
    {
        public static void UseImageEndpoints(this WebApplication application)
        {
            //допилить адекватное отображение страниц + добавить эндпоитны и кнопки для перехода между страницами 
            application.MapGet("/images", async (ImageLoadingService service, int pageNumber = 1, int pageSize = 40) =>
            {
                return await service.GetImageAsync(pageNumber, pageSize);
            });

            application.MapPost("/upload", async (HttpContext context, ImageLoadingService service) =>
            {

                var form = await context.Request.ReadFormAsync();

                var files = form.Files;

                var count = await service.DownloadImagesAsync(files);

                return Results.Ok($"изображений загружено: {count}");
            });
        }

        public static void UseApplicationEndpoints(this WebApplication application)
        {

            application.MapGet("/", async context =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(Path.Combine(application.Environment.ContentRootPath, "wwwroot", "main.html"));
            });

            application.MapFallback(async context =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.StatusCode = 404;
                await context.Response.SendFileAsync(Path.Combine(application.Environment.ContentRootPath, "wwwroot", "not-found.html"));
            });
        }
    }
}
