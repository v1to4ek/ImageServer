namespace ImageServer
{
    public interface IStorage
    {
        public Task Upload(IFormFile file);

        public Task<string[]> Download();
    }
}
