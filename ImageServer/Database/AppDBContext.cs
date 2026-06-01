using ImageServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ImageServer.Database
{
    public class AppDBContext : DbContext
    {

        public AppDBContext(DbContextOptions options) : base(options) 
        {
            Database.EnsureCreated();
        }

        public DbSet<ImageModel> Images { get; set; } = null!;
    }
}
