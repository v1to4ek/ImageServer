namespace ImageServer.Services
{
    public record class ServiceResult<TData> 
    {
        public bool IsSuccess { get; init; }

        public TData? Data { get; init; }

        public string? Error { get; init; }

        public static ServiceResult<TData> Ok(TData value) =>
            new() { IsSuccess = true, Data = value };

        public static ServiceResult<TData> Fail(string error) =>
            new() { IsSuccess = false, Error = error };
    }

    public record class ServiceResult
    {
        public bool IsSuccess { get; init; }

        public string? Error { get; init; }

        public static ServiceResult Ok() => 
            new() { IsSuccess = true };

        public static ServiceResult Fail(string error) =>
            new() { IsSuccess = false, Error = error };

    }
}
