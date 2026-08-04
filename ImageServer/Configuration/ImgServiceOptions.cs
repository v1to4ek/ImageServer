using ImageServer.Abstractions;

namespace ImageServer.Configuration
{
    public class ImgServiceOptions : IConfigurationOption
    {
        public static string SectionName => "Service";

        public int ParallelismDegree { get; set; } = Environment.ProcessorCount == 1 ? 1 : Environment.ProcessorCount/2;

        public int MaxAllowedPageSize { get; set; } = 100;

        public int MaxSequentalBatchSize { get; set; } = 25;

        public int MaxAllowedBatchSize { get; set; } = 100;
    }
}
