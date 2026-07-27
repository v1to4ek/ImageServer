using ImageServer.Abstractions;

namespace ImageServer.Configuration
{
    public class DeletionOptions : IConfigurationOption
    {
        public static string SectionName => "Deletion";

        public int OneCycleDeletionsCount { get; set; } = 20;

        public int DelayTimeInSeconds { get; set; } = 100;
    }
}
