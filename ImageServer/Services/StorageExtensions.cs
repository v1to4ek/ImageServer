using ImageServer.Abstractions;

namespace ImageServer.Services
{
    public static class StorageExtensions
    {
        public static bool TryGetFile(this IStorage storage,
            string fileId,
            string relativePath,
            out Stream? stream)
        {
            try
            {
                stream = storage.GetFile(fileId, relativePath);
                return true;
            }
            catch (Exception)
            {
                stream = null;
                return false;
            }
        }

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

        public static bool TryDeleteFile(this IStorage storage,
            string fileId,
            string relativePath)
        {
            try
            {
                storage.DeleteFile(fileId, relativePath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task MoveFile(this IStorage storage,
            string fileId,
            string sourceRelativePath,
            string destinationRelativePath,
            CancellationToken ct = default)
        {
            var fileStream = storage.GetFile(fileId, sourceRelativePath);

            try
            {
                await storage.SaveFileAsync(fileStream, fileId, destinationRelativePath, ct);
            }
            finally
            {
                await fileStream.DisposeAsync();
            }

            storage.DeleteFile(fileId, sourceRelativePath);
        }

        public static async Task<bool> TryMoveFile(this IStorage storage,
            string fileId,
            string sourceRelativePath,
            string destinationRelativePath,
            CancellationToken ct = default)
        {
            try
            {
                await storage.MoveFile(fileId, sourceRelativePath, destinationRelativePath, ct);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
