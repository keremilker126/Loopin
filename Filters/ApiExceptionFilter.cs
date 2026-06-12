using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Loopin.Filters;

public class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(
            context.Exception,
            "Controller işleminde beklenmeyen hata oluştu. Path: {Path}",
            context.HttpContext.Request.Path);

        if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
        {
            context.Result = new ObjectResult(new
            {
                message = "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin."
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
        else
        {
            context.Result = new RedirectToActionResult("Error", "Home", null);
        }

        context.ExceptionHandled = true;
    }
}
