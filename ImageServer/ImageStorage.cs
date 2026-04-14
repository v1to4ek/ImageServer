namespace ImageServer
{
    public class ImageStorage  : IStorage
    {
        private readonly string _path;

        public ImageStorage(string path)
        {
            _path = path;
            if(!Directory.Exists(_path)) Directory.CreateDirectory(_path);
        }

        public Task<string[]> Download()
        {
            var files = Directory.GetFiles(_path)
                .Select(file =>"/images/" + Path.GetFileName(file))
                .ToArray();

            return Task.FromResult(files);
        }

        public async Task Upload(IFormFile image)
        {
            var imageName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";

            var imagePath = Path.Combine(_path, imageName); 

            using(var fileStream = new FileStream(imagePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }
        }

    }
}
