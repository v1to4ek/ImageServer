using ImageServer.Abstractions;

namespace ImageServer.Services.Storages
{
    public static class StorageExtensions
    {
        public static async Task<bool> TrySaveFileAsync(this IStorage storage,
            Stream stream,
            string fileId,
            string relativePath,
            CancellationToken ct = default)
        {
            try
            {
                await storage.SaveFileAsync(stream, fileId, relativePath, ct);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<bool> TryDeleteFileAsync(this IStorage storage,
            string fileId,
            string relativePath,
            CancellationToken ct = default)
        {
            try
            {
                await storage.DeleteFileAsync(fileId, relativePath, ct);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<(bool success, Stream? stream)> TryGetFileAsync(this IStorage storage,
            string fileId,
            string relativePath,
            CancellationToken ct = default)
        {
            try
            {
                var stream = await storage.GetFileAsync(fileId, relativePath, ct);
                return (true, stream);
            }
            catch (Exception)
            {
                return (false, null);
            }
        }

        public static async Task MoveFileAsync(this IStorage storage,
            string fileId,
            string sourceRelativePath,
            string destinationRelativePath,
            CancellationToken ct = default)
            => await storage.ExecuteAsync(fileId,
                async innerStorage =>
                {

                    var fileStream = await innerStorage.GetFileAsync(fileId, sourceRelativePath, ct);

                    try
                    {
                        await innerStorage.SaveFileAsync(fileStream, fileId, destinationRelativePath, ct);
                    }
                    finally
                    {
                        await fileStream.DisposeAsync();
                    }

                    await innerStorage.DeleteFileAsync(fileId, sourceRelativePath, ct);

                }, ct);


        public static async Task<bool> TryMoveFileAsync(this IStorage storage,
            string fileId,
            string sourceRelativePath,
            string destinationRelativePath,
            CancellationToken ct = default)
        {
            try
            {
                await storage.MoveFileAsync(fileId, sourceRelativePath, destinationRelativePath, ct);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
