using ImageServer.Abstractions;
using ImageServer.Models;

namespace ImageServer.Services.Commands
{
    public class SetFavouriteCommand : IImageUpdateCommand
    {
        public void Execute(ImageModel image)
        {
            if(image.IsFavourite) image.IsFavourite = false;
            else image.IsFavourite = true;
        }
    }
}
