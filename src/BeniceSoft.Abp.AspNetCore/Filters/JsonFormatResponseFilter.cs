using BeniceSoft.Abp.Core;
using BeniceSoft.Core;
using BeniceSoft.Core.Reflector;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.AspNetCore.Filters;

/// <summary>
/// 统一响应格式化
/// </summary>
public class JsonFormatResponseFilter : IActionFilter, ITransientDependency
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        var isIgnore = context.ActionDescriptor
            .GetMethodInfo()
            .GetReflector()
            .GetCustomAttribute<IgnoreJsonFormatAttribute>() is not null || 
            context.HttpContext.Request.Headers.ContainsKey(BeniceSoftHttpConstant.IgnoreJsonFormat);
        if (isIgnore)
        {
            return;
        }

        var actionResult = context.Result;
        if (actionResult is ObjectResult objectResult)
        {
            var statusCode = objectResult.StatusCode ?? context.HttpContext.Response.StatusCode;
            context.Result = WarpJsonResult(objectResult.Value, statusCode == 204 ? 200 : statusCode);
        }
        else if (actionResult is EmptyResult)
        {
            context.Result = WarpJsonResult(new ResponseResult(), 200);
        }
        else if (actionResult is NoContentResult)
        {
            context.Result = WarpJsonResult(new ResponseResult(), 200);
        }
        else if (actionResult is StatusCodeResult statusCodeResult && statusCodeResult.StatusCode == 204)
        {
            context.Result = WarpJsonResult(new ResponseResult(), 200);
        }
    }

    private static JsonResult WarpJsonResult(object? originValue, int? statusCode = null)
    {
        var jsonResult = originValue is ResponseResult
            ? new JsonResult(originValue)
            : new JsonResult(originValue.ToSucceed());

        jsonResult.StatusCode = statusCode ?? 200;
        return jsonResult;
    }
}