namespace ImageServer
{
    //антипаттерн, переделать обобщенную сигнатуру
    //обобщить типизацию данных внутри таска, а не самого таска в GetImagesUrls
    //убрать дженерик из DownloadImageAsync - таск ниче не возвращает в принципе 
    //переименовать методы
    //в дальнейшем разделить интерфейс
    public interface IFileStorage<T,K> 
        where T : Task 
        where K : Task
    {
        public K GetImagesUrls();

        public T DownloadImageAsync(IFormFile file);
    }
}
