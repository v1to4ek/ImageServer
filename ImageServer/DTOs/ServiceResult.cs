namespace ImageServer.DTOs
{
    public record ServiceResult<TData> 
    {
        public bool IsSuccess { get; init; }

        public TData? Data { get; init; }

        public string? Error { get; init; }

        public static ServiceResult<TData> Ok(TData value) =>
            new() { IsSuccess = true, Data = value };

        public static ServiceResult<TData> Fail(string error) =>
            new() { IsSuccess = false, Error = error };
    }

    public record SavedResult(int SavedCount, List<string>? ErrorList);

}
