using ImageServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ImageServer.Database
{
    public class AppDBContext : DbContext
    {
        public AppDBContext(DbContextOptions options) : base(options) {}

        public DbSet<ImageModel> Images { get; set; } = null!;

        public DbSet<FileToDeletionModel> FilesToDeletion { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FileToDeletionModel>().HasQueryFilter(e => e.DeletionFailures == 0); 
        }
    }
}
