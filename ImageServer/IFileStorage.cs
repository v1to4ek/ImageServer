using SixLabors.ImageSharp;

namespace ImageServer
{
    //Переделана обобщённая сигнатура метода GetImagesUrls, теперь обобщается тип а не таск
    //Допилить интерфейс для типа ImageInfo(и аналогичных) и ограничение обобщения для интерфейса 
    public interface IFileStorage<T> 
    {
        public Task<T> GetImagesUrls();

        public Task DownloadImageAsync(IFormFile file);
    }
}
