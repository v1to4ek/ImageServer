using ImageServer.DTOs;
using ImageServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImageServer
{
    public static class Endpoints
    {
        public static void AddImageAPI(this WebApplication webApplication)
        {
            //получение страницы превьюшек
            webApplication.MapGet("/images", async (ImageService service,[AsParameters] PagedRequest request) =>
            {
                Console.WriteLine(request.PageNumber);

                Console.WriteLine(request.PageSize);

                var response = await service.GetPagedResultAsync(request);

                return Results.Ok(response);

            });

            ////получние полноценной картинки
            //webApplication.MapGet("", async (ImageService service, string id) =>
            //{

            //});

            ////скачивание картинки
            //webApplication.MapGet("", async (ImageService service, string id) =>
            //{

            //});

            //загрузка картинок на сервер
            webApplication.MapPost("/upload", async (ImageService service, IFormFileCollection formFiles) =>
            {
                var count = await service.SaveImagesAsync(formFiles);

                return Results.Ok($"изображений загружено: {count}");

            }).DisableAntiforgery();

            ////удаление картинки
            //webApplication.MapDelete("", async (ImageService service, string id) =>
            //{

            //});
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
