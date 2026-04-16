namespace ImageServer
{
    public interface IFileStorage<T,K> 
        where T : Task 
        where K : Task
    {
        public K GetImagesUrls();

        public T DownloadImageAsync(IFormFile file);
    }
}
