using ImageServer.DTOs;
using ImageServer.Services;
using ImageServer.Services.Commands;
using Microsoft.AspNetCore.Mvc;

namespace ImageServer
{
    public static class Endpoints
    {
        public static void AddImageAPI(this WebApplication webApplication)
        {

            webApplication.MapGet("/images", 
                async (ImageService service,
                [AsParameters] PagedImagesRequest request, 
                CancellationToken ct) =>
                {
                    var result = await service.GetImagesPagedAsync(request, ct);

                    return result.IsSuccess
                    ? Results.Ok(result.Data)
                    : Results.BadRequest(result.Error);
                });

            webApplication.MapGet("/trash",
                async (ImageService service,
                [AsParameters] PagedTrashedRequest requst,
                CancellationToken ct) =>
                {
                    var result = await service.GetTrashedPagedAsync(requst, ct);

                    return result.IsSuccess
                    ? Results.Ok(result.Data)
                    : Results.BadRequest(result.Error);
                });

            webApplication.MapGet("/images/{id}",
                async (ImageService service,
                string id, 
                CancellationToken ct) =>
                {
                    var result = await service.GetAsync(id, ct);

                    return result.IsSuccess
                    ? Results.File(result.Data!,"image/webp", id)
                    : Results.NotFound(result.Error);
                });

            webApplication.MapPut("/images/{id}/favourite",
                async (ImageService service, 
                string id,
                SetFavouriteCommand command, 
                CancellationToken ct) =>
                {
                    var result = await service.UpdateAsync(id, command, ct);

                    return result.IsSuccess
                    ? Results.NoContent()
                    : Results.NotFound(result.Error);
                });

            webApplication.MapPut("/images/{id}/rename", 
                async (ImageService service, 
                string id,
                RenameCommand command,
                CancellationToken ct) =>
                {
                    var result = await service.UpdateAsync(id, command, ct);

                    return result.IsSuccess
                    ? Results.NoContent()
                    : Results.NotFound(result.Error);
                });

            webApplication.MapPost("/images",
                async (ImageService service, 
                IFormFileCollection formFiles, 
                CancellationToken ct) =>
                {
                    var result = await service.SaveAsync(formFiles, ct);

                    return Results.Ok(result.Data!.SuccessCount);
                }).DisableAntiforgery();

            webApplication.MapDelete("/images/{id}",
                async (ImageService service,
                string id,
                CancellationToken ct) =>
                {
                    var result = await service.DeleteOneAsync(id, ct);

                    return result.IsSuccess
                    ? Results.NoContent()
                    : Results.NotFound(result.Error);
                });

            webApplication.MapPost("/images/{id}/restore",
                async (ImageService service,
                string id,
                CancellationToken ct) =>
                {
                    var result = await service.RestoreOneAsync(id, ct);

                    return result.IsSuccess
                    ? Results.NoContent()
                    : Results.NotFound(result.Error);
                });

            webApplication.MapPost("/images/restore-many",
                async (ImageService service,
                List<string> ids,
                CancellationToken ct) =>
                {
                    var result = await service.RestoreManyAsync(ids, ct);

                    return result.IsSuccess
                    ? Results.Ok(result.Data)
                    : Results.NotFound(result.Error);
                });

            webApplication.MapDelete("/images/delete-many",
                async (ImageService service,
                [FromBody] List<string> ids,
                CancellationToken ct) =>
                {
                    var result = await service.DeleteManyAsync(ids, ct);

                    return result.IsSuccess
                    ? Results.Ok(result.Data)
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
