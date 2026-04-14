namespace ImageServer
{
    public static class Endpoints
    {
        public static void UseImageEndpoints(this WebApplication application)
        {
            application.MapGet("/images", async (HttpContext context, ImageService service) => await service.DownloadImageAsync(context));

            application.MapPost("/upload", async (HttpContext context, ImageService service) => await service.UploadImageAsync(context));
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
