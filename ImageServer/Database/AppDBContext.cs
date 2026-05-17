using ImageServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ImageServer.Database
{
    public class AppDBContext : DbContext
    {
        public AppDBContext() : base() 
        {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=ImageServerDB;Username=postgres;Password=10250507LovE");
        }

        public DbSet<ImageModel> Images { get; set; } = null!;
    }
}
