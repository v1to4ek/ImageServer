using ImageServer.Abstractions;

namespace ImageServer.Configuration
{
    public class DatabaseOptions : IConfigurationOption
    {
        public static string SectionName => "Database";

        public string ConnectionString { get; set; } = "Host=localhost;Port=5432;Database=ImageServerDB;Username=postgres;Password=postgres";
    }
}
