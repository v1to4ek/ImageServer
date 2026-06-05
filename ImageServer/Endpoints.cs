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
                var result = await service.GetPagedResultAsync(request);

                return Results.Ok(result.Data);
            });

            //скачивание картинки
            webApplication.MapGet("/download", (ImageService service, string id) =>
            {
                var result = service.GetImage(id);

                return result.IsSuccess
                ? Results.File(result.Data!,"image/webp", id)
                : Results.NotFound(result.Error);
            });

            //загрузка картинок на сервер
            webApplication.MapPost("/upload", async (ImageService service, IFormFileCollection formFiles) =>
            {
                var result = await service.SaveImagesAsync(formFiles);

                return Results.Ok(result.Data!.SavedCount);

            }).DisableAntiforgery();

            //удаление картинки
            webApplication.MapDelete("/delete", async (ImageService service, string id) =>
            {
                var result = await service.DeleteAsync(id);

                return result.IsSuccess
                ? Results.Ok()
                : Results.NotFound(result.Error);
            });
        }

        public static void AddApplicationEndpoints(this WebApplication webApplication)
        {
            webApplication.MapGet("/", async context =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(Path.Combine(webApplication.Environment.ContentRootPath, "wwwroot", "main.html"));
            });

            webApplication.MapFallback(async context =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.StatusCode = 404;
                await context.Response.SendFileAsync(Path.Combine(webApplication.Environment.ContentRootPath, "wwwroot", "not-found.html"));
            });
        }
    }
}
