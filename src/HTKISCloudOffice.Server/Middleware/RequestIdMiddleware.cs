namespace HTKISCloudOffice.Server.Middleware;

public class RequestIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string RequestIdHeader = "X-Request-Id";

    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request_id = context.Request.Headers.TryGetValue(RequestIdHeader, out var id)
            ? id.ToString()
            : Guid.NewGuid().ToString();

        context.Items["request_id"] = request_id;
        context.Response.Headers.Append(RequestIdHeader, request_id);

        await _next(context);
    }
}