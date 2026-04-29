namespace AutoTest.Api.Middleware;

// Tells browsers/proxies that response bodies depend on the X-Api-Lang and
// Accept-Language request headers. Without this, the browser may serve a
// cached response in the wrong language when the user switches locale.
public class VaryHeaderMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            // Append (don't overwrite) so existing Vary entries are preserved.
            context.Response.Headers.Append("Vary", "X-Api-Lang");
            context.Response.Headers.Append("Vary", "Accept-Language");
            return Task.CompletedTask;
        });
        return next(context);
    }
}
