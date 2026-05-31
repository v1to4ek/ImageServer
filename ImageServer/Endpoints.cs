using ImageServer.DTOs;
using ImageServer.Services;

namespace ImageServer
{
    public static class Endpoints
    {
        public static void AddImageAPI(this WebApplication webApplication)
        {
            //получение страницы картинок
            webApplication.MapGet("/images", async (ImageService service,[AsParameters] PagedRequest request) =>
            {
                var response = await service.GetPagedResultAsync(request);

                return Results.Ok(response);

            });

            //скачивание картинки
            webApplication.MapGet("/download", (ImageService service, string id) =>
            {
                var stream = service.GetImage(id);

                return Results.File(stream);
            });

            //загрузка картинок на сервер
            webApplication.MapPost("/upload", async (ImageService service, IFormFileCollection formFiles) =>
            {
                var count = await service.SaveImagesAsync(formFiles);

                return Results.Ok($"изображений загружено: {count}");

            }).DisableAntiforgery();

            //удаление картинки
            webApplication.MapDelete("/delete", async (ImageService service, string id) =>
            {
                await service.DeleteAsync(id);
            });
        }

        public static void AddApplicationEndpoints(this WebApplication webApplication)
        {
            webApplication.MapGet("/", async context =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(Path.Combine(webApplication.Environment.ContentRootPath, "wwwroot", "main.html"));
            });
        }
    }
}
