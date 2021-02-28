using AutoWrapper.Extensions;
using AutoWrapper.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AutoWrapper.Filters;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using Serilog.Events;
using static Microsoft.AspNetCore.Http.StatusCodes;
using ILogger = Serilog.ILogger;

namespace AutoWrapper.Base
{
    internal abstract class WrapperBase
    {
        private readonly RequestDelegate _next;
        private readonly AutoWrapperOptions _options;
        private readonly ILogger _logger;
        private IActionResultExecutor<ObjectResult> _executor { get; }

        public WrapperBase(RequestDelegate next,
            AutoWrapperOptions options,
            ILogger logger,
            IActionResultExecutor<ObjectResult> executor)
        {
            _next = next;
            _options = options;
            _logger = logger;
            _executor = executor;
        }

        public virtual async Task InvokeAsyncBase(HttpContext context, AutoWrapperMembers awm)
        {
            if (awm.IsSwagger(context, _options.SwaggerPath) || !awm.IsApi(context))
                await _next(context);
            else
            {
                var stopWatch = Stopwatch.StartNew();
                var requestBody = await awm.GetRequestBodyAsync(context.Request);
                Exception ex = null;
                var originalResponseBodyStream = context.Response.Body;
                bool isRequestOk = false;

                using var memoryStream = new MemoryStream();

                try
                {
                    context.Response.Body = memoryStream;
                    await _next.Invoke(context);

                    isRequestOk = awm.IsRequestSuccessful(context.Response.StatusCode);
                    if (context.Response.HasStarted)
                    {
                        LogResponseHasStartedError(context, requestBody, stopWatch, isRequestOk, ex);
                        return;
                    }

                    var endpoint = context.GetEndpoint();
                    if (endpoint?.Metadata?.GetMetadata<AutoWrapIgnoreAttribute>() is object)
                    {
                        await awm.RevertResponseBodyStreamAsync(memoryStream, originalResponseBodyStream);
                        return;
                    }

                    var bodyAsText = await awm.ReadResponseBodyStreamAsync(memoryStream);
                    context.Response.Body = originalResponseBodyStream;

                    if (context.Response.StatusCode != Status304NotModified &&
                        context.Response.StatusCode != Status204NoContent)
                    {
                        if (!_options.IsApiOnly
                            && (bodyAsText.IsHtml()
                                && !_options.BypassHTMLValidation)
                            && context.Response.StatusCode == Status200OK)
                        {
                            context.Response.StatusCode = Status404NotFound;
                        }

                        if (!context.Request.Path.StartsWithSegments(new PathString(_options.WrapWhenApiPathStartsWith))
                            && (bodyAsText.IsHtml()
                                && !_options.BypassHTMLValidation)
                            && context.Response.StatusCode == Status200OK)
                        {
                            if (memoryStream.Length > 0)
                            {
                                await awm.HandleNotApiRequestAsync(context);
                            }

                            return;
                        }

                        isRequestOk = awm.IsRequestSuccessful(context.Response.StatusCode);
                        if (isRequestOk)
                        {
                            if (_options.IgnoreWrapForOkRequests)
                            {
                                await awm.WrapIgnoreAsync(context, bodyAsText);
                            }
                            else
                            {
                                await awm.HandleSuccessfulRequestAsync(context, bodyAsText,
                                    context.Response.StatusCode);
                            }
                        }
                        else
                        {
                            if (_options.UseApiProblemDetailsException)
                            {
                                await awm.HandleProblemDetailsExceptionAsync(context, _executor, bodyAsText);
                                return;
                            }

                            await awm.HandleUnsuccessfulRequestAsync(context, bodyAsText, context.Response.StatusCode);
                        }
                    }
                }
                catch (Exception exception)
                {
                    ex = exception;
                    if (context.Response.HasStarted)
                    {
                        LogResponseHasStartedError(context, requestBody, stopWatch, isRequestOk, ex);
                        return;
                    }

                    if (_options.UseApiProblemDetailsException)
                    {
                        await awm.HandleProblemDetailsExceptionAsync(context, _executor, null, exception);
                    }
                    else
                    {
                        await awm.HandleExceptionAsync(context, exception);
                    }

                    await awm.RevertResponseBodyStreamAsync(memoryStream, originalResponseBodyStream);
                }
                finally
                {
                    LogHttpRequest(context, requestBody, stopWatch, isRequestOk, ex);
                }
            }
        }

        private bool ShouldLogRequestData(HttpContext context)
        {
            if (_options.ShouldLogRequestData)
            {
                var endpoint = context.GetEndpoint();
                return !(endpoint?.Metadata?.GetMetadata<RequestDataLogIgnoreAttribute>() is object);
            }

            return false;
        }

        private bool ShouldLogResponseData(HttpContext context)
        {
            if (_options.ShouldLogResponseData)
            {
                var endpoint = context.GetEndpoint();
                return !(endpoint?.Metadata?.GetMetadata<ResponseDataLogIgnoreAttribute>() is object);
            }

            return false;
        }


        private void LogHttpRequest(HttpContext context, string requestBody, Stopwatch stopWatch, bool isRequestOk, Exception ex)
        {
            stopWatch.Stop();
            if (_options.EnableResponseLogging || (!isRequestOk && _options.EnableExceptionLogging))
            {
                bool shouldLogRequestData = ShouldLogRequestData(context);
                JsonDocument requestBodyObject = null;
                if ((shouldLogRequestData || (!isRequestOk && _options.LogResponseDataOnException)) && !string.IsNullOrWhiteSpace(requestBody))
                {
                    try
                    {
                        requestBodyObject = System.Text.Json.JsonDocument.Parse(requestBody);
                    }
                    catch (Exception e) { }
                }
                else
                {
                    requestBody = null;
                }

                var requestData = new
                {
                    ClientIp = context.Connection.RemoteIpAddress,
                    context.Request.Method,
                    context.Request.Scheme,
                    context.Request.Host,
                    context.Request.Path,
                    context.Request.QueryString,
                    context.Request.Query,
                    BodyString = requestBody,
                    Body = requestBodyObject
                };
                
                
                bool shouldLogResponseData = ShouldLogResponseData(context);
                string responseBody = null;
                object responseBodyObject = null;
                if ((shouldLogResponseData || (!isRequestOk && _options.LogResponseDataOnException)))
                {
                    var responseBodyStream = context.Response.Body;
                    if (responseBodyStream != null)
                    {
                        responseBodyStream.Seek(0, SeekOrigin.Begin);
                        responseBody = Task.Run(() => new StreamReader(responseBodyStream).ReadToEndAsync()).Result;
                        responseBodyStream.Seek(0, SeekOrigin.Begin);

                        var (IsEncoded, ParsedText) = responseBody.VerifyBodyContent();
                        if (IsEncoded)
                        {
                            responseBody = ParsedText;
                        }
                        try
                        {
                            responseBodyObject = System.Text.Json.JsonDocument.Parse(responseBody);
                        }
                        catch (Exception e) { }
                    }
                }
                var responseData = new
                {
                    context.Response.StatusCode,
                    stopWatch.ElapsedMilliseconds,
                    BodyString = responseBody,
                    Body = responseBodyObject,
                };

                var level = LogEventLevel.Information;
                if (context.Response.StatusCode >= 500)
                {
                    level = LogEventLevel.Error;
                }
                else if (context.Response.StatusCode >= 400)
                {
                    level = LogEventLevel.Warning;
                }

                using (LogContext.PushProperty("HttpRequest", requestData, true))
                using (LogContext.PushProperty("HttpResponse", responseData, true))
                {
                    _logger.Write(level, ex, DefaultRequestCompletionMessageTemplate, new
                    {
                        Request = requestBody,
                        Response = responseBody,
                    });
                }
            }
        }
        
        const string DefaultRequestCompletionMessageTemplate =
            "HTTP Request Completed {@Context}";

        
        private void LogResponseHasStartedError(HttpContext context, string requestBody, Stopwatch stopWatch, bool isRequestOk, Exception ex)
        {
            _logger.Warning("The response has already started, the AutoWrapper.Plus.Serilog middleware will not be executed.");
            LogHttpRequest(context, requestBody, stopWatch, isRequestOk, ex);
        }
    }
}