using AutoWrapper.Extensions;
using AutoWrapper.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoWrapper.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Serilog;
using Serilog.Context;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Extensions.Hosting;
using Serilog.Parsing;
using static Microsoft.AspNetCore.Http.StatusCodes;
using ILogger = Serilog.ILogger;

namespace AutoWrapper.Base
{
    internal abstract class WrapperBase
    {
        private readonly RequestDelegate _next;
        private readonly DiagnosticContext _diagnosticContext;
        private readonly AutoWrapperOptions _options;
        private readonly ILogger _logger;
        readonly MessageTemplate _messageTemplate;
        static readonly LogEventProperty[] NoProperties = new LogEventProperty[0];
        private IActionResultExecutor<ObjectResult> _executor { get; }

        public WrapperBase(RequestDelegate next, DiagnosticContext diagnosticContext,
            AutoWrapperOptions options,
            IActionResultExecutor<ObjectResult> executor)
        {
            _next = next;
            _diagnosticContext = diagnosticContext;
            _options = options;
            _messageTemplate = new MessageTemplateParser().Parse(DefaultRequestCompletionMessageTemplate);
            _logger =  options.Logger ?? Log.ForContext<WrapperBase>();
            _executor = executor;
        }

        public virtual async Task InvokeAsyncBase(HttpContext context, AutoWrapperMembers awm)
        {
            if (awm.IsSwagger(context, _options.SwaggerPath) || !awm.IsApi(context) || awm.IsExclude(context, _options.ExcludePaths))
                await _next(context);
            else
            {
                (int statusCode, string response) finalResponse = (context.Response.StatusCode, string.Empty);
                var collector = _diagnosticContext.BeginCollection();
                var stopWatch = Stopwatch.StartNew();
                string requestBody = null;
                try
                {
                    requestBody = await awm.GetRequestBodyAsync(context.Request);
                }
                catch (Exception e)
                {
                    _logger.Warning(e, "AutoWrapper cannot read request body due to exception");
                }
                Exception ex = null;
                Stream originalResponseBodyStream = null;
                bool isRequestOk = false;
                string responseBodyAsText = null;
                using var memoryStream = new MemoryStream();

                try
                {
                    originalResponseBodyStream = context.Response.Body;
                    context.Response.Body = memoryStream;
                    await _next.Invoke(context);

                    isRequestOk = awm.IsRequestSuccessful(context.Response.StatusCode);
                    if (context.Response.HasStarted)
                    {
                        LogResponseHasStartedError(context, requestBody, responseBodyAsText, stopWatch, isRequestOk, ex);
                        return;
                    }

                    var endpoint = context.GetEndpoint();
                    if (endpoint?.Metadata?.GetMetadata<AutoWrapIgnoreAttribute>() is object)
                    {
                        await awm.RevertResponseBodyStreamAsync(memoryStream, originalResponseBodyStream);
                        return;
                    }

                    responseBodyAsText = await awm.ReadResponseBodyStreamAsync(memoryStream);
                    context.Response.Body = originalResponseBodyStream;

                    if (context.Response.StatusCode != Status304NotModified &&
                        context.Response.StatusCode != Status204NoContent)
                    {
                        if (!_options.IsApiOnly
                            && (responseBodyAsText.IsHtml()
                                && !_options.BypassHTMLValidation)
                            && context.Response.StatusCode == Status200OK)
                        {
                            context.Response.StatusCode = Status404NotFound;
                        }

                        if (!context.Request.Path.StartsWithSegments(new PathString(_options.WrapWhenApiPathStartsWith))
                            && (responseBodyAsText.IsHtml()
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
                                await awm.WrapIgnoreAsync(context, responseBodyAsText);
                            }
                            else
                            {
                               finalResponse = await awm.HandleSuccessfulRequestAsync(context, responseBodyAsText,
                                    context.Response.StatusCode);
                            }
                        }
                        else
                        {
                            if (_options.UseApiProblemDetailsException)
                            {
                                finalResponse = await awm.HandleProblemDetailsExceptionAsync(context, _executor, responseBodyAsText);
                                return;
                            }

                            finalResponse = await awm.HandleUnsuccessfulRequestAsync(context, responseBodyAsText,
                                context.Response.StatusCode);
                        }
                    }
                }
                catch (Exception exception)
                {
                    ex = exception;
                    if (context.Response.HasStarted)
                    {
                        LogResponseHasStartedError(context, requestBody, responseBodyAsText, stopWatch, isRequestOk,
                            ex);
                        return;
                    }

                    if (_options.UseApiProblemDetailsException)
                    {
                        finalResponse = await awm.HandleProblemDetailsExceptionAsync(context, _executor, null, exception);
                    }
                    else
                    {
                        finalResponse = await awm.HandleExceptionAsync(context, exception);
                    }

                    responseBodyAsText = await awm.ReadResponseBodyStreamAsync(memoryStream);

                    await awm.RevertResponseBodyStreamAsync(memoryStream, originalResponseBodyStream);
                }
                finally
                {
                    if (string.IsNullOrWhiteSpace(finalResponse.response))
                        finalResponse.response = responseBodyAsText;
                    LogHttpRequest(context, collector, requestBody, finalResponse.response, finalResponse.statusCode, stopWatch, isRequestOk, ex);
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

        
        private void LogHttpRequest(HttpContext context, DiagnosticContextCollector collector, string requestBody, string finalResponseBody, int finalStatusCode, Stopwatch stopWatch,
            bool isRequestOk,
            Exception ex)
        {
            stopWatch.Stop();
            var endpoint = context.GetEndpoint();
            var shouldLogHttpRequest = !(endpoint?.Metadata?.GetMetadata<IgnoreLogAttribute>() is object);
            if (!shouldLogHttpRequest)
                return;
            
            if (_options.EnableResponseLogging || (!isRequestOk && _options.EnableExceptionLogging))
            {
                bool shouldLogRequestData = ShouldLogRequestData(context);
                JsonDocument requestBodyObject = null;
                if ((shouldLogRequestData || (!isRequestOk && _options.LogResponseDataOnException)) &&
                    !string.IsNullOrWhiteSpace(requestBody))
                {
                    try { requestBody = requestBody.MaskFields(_options.MaskedProperties.ToArray(), _options.MaskFormat); } catch (Exception) { }
                    if (requestBody.Length > _options.RequestBodyTextLengthLogLimit)
                        requestBody = requestBody.Substring(0, _options.RequestBodyTextLengthLogLimit);
                    else
                        try { requestBodyObject = System.Text.Json.JsonDocument.Parse(requestBody); } catch (Exception) { }
                }
                else
                {
                    requestBody = null;
                }
                
                var requestHeader = new Dictionary<string, object>();
                if (_options.ShouldLogRequestHeader ||
                    (!isRequestOk && _options.LogRequestHeaderOnException))
                {
                    try
                    {
                        var valuesByKey = context.Request.Headers.Mask(_options.MaskedProperties.ToArray(), _options.MaskFormat).GroupBy(x => x.Key);
                        foreach (var item in valuesByKey)
                        {
                            if (item.Count() > 1)
                                requestHeader.Add(item.Key, item.Select(x => x.Value.ToString()).ToArray());
                            else
                                requestHeader.Add(item.Key, item.First().Value.ToString());
                        }
                    }
                    catch (Exception)
                    {
                        SelfLog.WriteLine("Cannot parse response header");
                    }    
                }
                
                var userAgentDic = new Dictionary<string, string>();
                if (context.Request.Headers.ContainsKey("User-Agent"))
                {
                    var userAgent = context.Request.Headers["User-Agent"].ToString();
                    userAgentDic.Add("_Raw", userAgent);
                    try
                    {
                        var uaParser = UAParser.Parser.GetDefault();
                        var clientInfo = uaParser.Parse(userAgent);
                        userAgentDic.Add("Browser", clientInfo.UA.Family);
                        userAgentDic.Add("BrowserVersion", clientInfo.UA.Major + "." + clientInfo.UA.Minor);
                        userAgentDic.Add("OperatingSystem", clientInfo.OS.Family);
                        userAgentDic.Add("OperatingSystemVersion", clientInfo.OS.Major + "." + clientInfo.OS.Minor);
                        userAgentDic.Add("Device", clientInfo.Device.Family);
                        userAgentDic.Add("DeviceModel", clientInfo.Device.Model);
                        userAgentDic.Add("DeviceManufacturer", clientInfo.Device.Brand);
                    }
                    catch (Exception)
                    {
                        SelfLog.WriteLine("Cannot parse user agent:" + userAgent);
                    }
                }

                var requestQuery = new Dictionary<string, object>();
                try
                {
                    var valuesByKey =context.Request.Query.GroupBy(x => x.Key);
                    foreach (var item in valuesByKey)
                    {
                        if (item.Count() > 1)
                            requestQuery.Add(item.Key, item.Select(x => x.Value.ToString()).ToArray());
                        else
                            requestQuery.Add(item.Key, item.First().Value.ToString());
                    }
                }
                catch (Exception)
                {
                    SelfLog.WriteLine("Cannot parse query string");
                }    

                var requestData = new
                {
                    ClientIp = context.GetClientIp().ToString(),
                    Method = context.Request.Method,
                    Scheme = context.Request.Scheme,
                    Host = context.Request.Host.Value,
                    Path = context.Request.Path.Value,
                    QueryString = context.Request.QueryString.Value,
                    Query = requestQuery,
                    BodyString = requestBody ?? string.Empty,
                    Body = requestBodyObject,
                    Header = requestHeader,
                    UserAgent = userAgentDic,
                };

                bool shouldLogResponseData = ShouldLogResponseData(context);
                object responseBodyObject = null;
                if ((shouldLogResponseData || (!isRequestOk && _options.LogResponseDataOnException)))
                {
                    try { finalResponseBody = finalResponseBody.MaskFields(_options.MaskedProperties.ToArray(), _options.MaskFormat); } catch (Exception) { }

                    if (finalResponseBody != null)
                    {
                        if (finalResponseBody.Length > _options.ResponseBodyTextLengthLogLimit)
                            finalResponseBody = finalResponseBody.Substring(0, _options.ResponseBodyTextLengthLogLimit);
                        else
                            try { responseBodyObject = System.Text.Json.JsonDocument.Parse(finalResponseBody); } catch (Exception) { }
                    }
                }
                else
                {
                    finalResponseBody = null;
                }
                
                var responseHeader = new Dictionary<string, object>();
                if (_options.ShouldLogResponseHeader ||
                    (!isRequestOk && _options.LogResponseHeaderOnException))
                {
                    try
                    {
                        var valuesByKey = context.Response.Headers.Mask(_options.MaskedProperties.ToArray(), _options.MaskFormat).GroupBy(x => x.Key);
                        foreach (var item in valuesByKey)
                        {
                            if (item.Count() > 1)
                                responseHeader.Add(item.Key, item.Select(x => x.Value.ToString()).ToArray());
                            else
                                responseHeader.Add(item.Key, item.First().Value.ToString());
                        }
                    }
                    catch (Exception)
                    {
                        SelfLog.WriteLine("Cannot parse response header");
                    }    
                }

                var responseData = new
                {
                    StatusCode = finalStatusCode,
                    stopWatch.ElapsedMilliseconds,
                    BodyString = finalResponseBody ?? string.Empty,
                    Body = responseBodyObject,
                    Header = responseHeader
                };

                var level = LogEventLevel.Information;
                if (finalStatusCode >= 500)
                {
                    level = LogEventLevel.Error;
                }
                else if (finalStatusCode >= 400)
                {
                    level = LogEventLevel.Warning;
                }

                var props = endpoint?.Metadata?.GetOrderedMetadata<LogCustomPropertyAttribute>()?
                    .ToDictionary(x => x.Name, x => x.Value);

                if (!collector.TryComplete(out var collectedProperties))
                    collectedProperties = NoProperties;
                _logger.Write(level, ex, DefaultRequestCompletionMessageTemplate, new
                {
                    Request = requestData,
                    Response = responseData,
                    Properties = props,
                    Context = collectedProperties.ToDictionary(x => x.Name, x => x.Value.ToString()),
                });
            }
        }

        const string DefaultRequestCompletionMessageTemplate =
            "HttpRequest Completed with {@Data}";


        private void LogResponseHasStartedError(HttpContext context, string requestBody, string responseStream,
            Stopwatch stopWatch,
            bool isRequestOk, Exception ex)
        {
            _logger.Warning(
                "The response has already started, the AutoWrapper.Plus.Serilog middleware will not be executed.");
        }
    }
}