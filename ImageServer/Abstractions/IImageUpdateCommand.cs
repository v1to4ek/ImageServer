using ImageServer.Models;

namespace ImageServer.Abstractions
{
    public interface IImageUpdateCommand
    {
        public void Execute(ImageModel image);
    }
}
