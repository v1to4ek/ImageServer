using ImageServer.Abstractions;

namespace ImageServer.Configuration
{
    public class ImgServiceOptions : IConfigurationOption
    {
        public static string SectionName => "Service";

        public int ParallelismDegree { get; set; } = Environment.ProcessorCount == 1 ? 1 : Environment.ProcessorCount/2;
    }
}
