using ImageServer.Abstractions;
using ImageServer.Models;

namespace ImageServer.Services.Commands
{
    public class RenameCommand : IImageUpdateCommand
    {
        public string Name { get; set; } = string.Empty;

        public void Execute(ImageModel image) => image.Name = Name;

    }
}
