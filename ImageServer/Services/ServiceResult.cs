using ImageServer.Enums;
using Microsoft.AspNetCore.Mvc;

namespace ImageServer.Services
{
    public abstract record class ServiceResultBase
    {
        public bool IsSuccess { get; init; }

        public string? Error { get; init; }

        public HttpStatuses Status { get; init; }
    }

    public record class ServiceResult<TData> : ServiceResultBase
    {
        public TData? Data { get; init; }

        public static ServiceResult<TData> Ok(TData value, HttpStatuses status) =>
            new() { IsSuccess = true, 
                Data = value, 
                Status = status };

        public static ServiceResult<TData> Fail(string error, HttpStatuses status) =>
            new() { IsSuccess = false,
                Error = error, 
                Status = status };
    }

    public record class ServiceResult : ServiceResultBase
    {
        public static ServiceResult Ok(HttpStatuses status) => 
            new() { IsSuccess = true, 
                Status = status };

        public static ServiceResult Fail(string error, HttpStatuses status) =>
            new() { IsSuccess = false,
                Error = error,
                Status = status };

    }

    public static class ServiceResultExtensions
    {
        public static IResult ToHttpResult(this ServiceResult result, Func<IResult>? successResultFactory = null)
        {
            if (result.IsSuccess)
            {
                if (successResultFactory is null) return Results.Ok();
                return successResultFactory();

            }
            else
            {
                return Results.Problem(detail: result.Error, statusCode: (int)result.Status);
            }
        }

        public static IResult ToHttpResult<TData>(this ServiceResult<TData> result, Func<TData,IResult>? successResultFactory = null)
        {
            if (result.IsSuccess)
            {
                if (successResultFactory is null) return Results.Ok(result.Data!);
                return successResultFactory(result.Data!);
            }
            else
            {
                return Results.Problem(detail: result.Error, statusCode: (int)result.Status);
            }
        }
    }
}
