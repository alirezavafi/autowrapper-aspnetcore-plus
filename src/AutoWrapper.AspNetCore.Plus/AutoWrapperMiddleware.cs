using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using AutoWrapper.Base;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AutoWrapper
{
    internal class AutoWrapperMiddleware : WrapperBase
    {
        private readonly AutoWrapperMembers _awm;
        public AutoWrapperMiddleware(RequestDelegate next, AutoWrapperOptions options, IActionResultExecutor<ObjectResult> executor) : base(next, options, executor)
        {
            var jsonSettings = Helpers.JSONHelper.GetJSONSettings(options.IgnoreNullValue, options.ReferenceLoopHandling, options.UseCamelCaseNamingStrategy);
            _awm = new AutoWrapperMembers(options, jsonSettings);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await InvokeAsyncBase(context, _awm);
        }
    }

    internal class AutoWrapperMiddleware<T> : WrapperBase
    {
        private readonly AutoWrapperMembers _awm;
        public AutoWrapperMiddleware(RequestDelegate next, AutoWrapperOptions options, IActionResultExecutor<ObjectResult> executor) : base(next, options, executor)
        {
            var (Settings, Mappings) = Helpers.JSONHelper.GetJSONSettings<T>(options.IgnoreNullValue, options.ReferenceLoopHandling, options.UseCamelCaseNamingStrategy);
            _awm = new AutoWrapperMembers(options, Settings, Mappings, true);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await InvokeAsyncBase(context, _awm);
        }

    }
}
