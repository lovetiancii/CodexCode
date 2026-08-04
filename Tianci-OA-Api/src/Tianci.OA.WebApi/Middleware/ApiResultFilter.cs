using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Tianci.OA.WebApi.Middleware;

public sealed class ApiResultFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult objectResult
            && objectResult.Value is not ApiResponse<object>
            && objectResult.Value is not ProblemDetails)
        {
            var response = new ApiResponse<object?>(
                true,
                "OK",
                "操作成功",
                objectResult.Value,
                context.HttpContext.TraceIdentifier);

            context.Result = new ObjectResult(response)
            {
                StatusCode = objectResult.StatusCode
            };
        }
        else if (context.Result is EmptyResult or NoContentResult)
        {
            var response = new ApiResponse<object?>(
                true,
                "OK",
                "操作成功",
                null,
                context.HttpContext.TraceIdentifier);

            context.Result = new ObjectResult(response);
        }

        await next();
    }
}
