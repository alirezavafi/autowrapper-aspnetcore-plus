using System;
using System.Collections.Generic;
using AutoWrapper.Base;
using AutoWrapper.Models;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;

namespace AutoWrapper
{
    public class AutoWrapperOptions : OptionBase
    {
        public bool UseCustomSchema { get; set; } = false;
        public ReferenceLoopHandling ReferenceLoopHandling { get; set; } = ReferenceLoopHandling.Ignore;
        public bool UseCustomExceptionFormat { get; set; } = false;
        public bool UseApiProblemDetailsException { get; set; } = true;
        public string SwaggerPath { get; set; } = "/swagger";
        public bool IgnoreWrapForOkRequests { get; set; } = false;
        
        /// <summary>
        /// Determines when logging requests information. Default is true.
        /// </summary>
        public LogMode LogMode { get; set; } = LogMode.LogAll;

        /// <summary>
        /// Determines when logging request headers
        /// </summary>
        public LogMode RequestHeaderLogMode { get; set; } = LogMode.LogAll;

        /// <summary>
        /// Determines when logging request body data
        /// </summary>
        public LogMode RequestBodyLogMode { get; set; } = LogMode.LogAll;

        /// <summary>
        /// Determines weather to log request as structured object instead of string. This is useful when you use Elastic, Splunk or any other platform to search on object properties. Default is true. Masking only works when this options is enabled.
        /// </summary>
        public bool LogRequestBodyAsStructuredObject { get; set; } = true;

        /// <summary>
        /// Determines when logging response headers
        /// </summary>
        public LogMode ResponseHeaderLogMode { get; set; } = LogMode.LogAll;

        /// <summary>
        /// Determines when logging response body data
        /// </summary>
        public LogMode ResponseBodyLogMode { get; set; } = LogMode.LogFailures;
        
        /// <summary>
        /// Determines weather to log response as structured object instead of string. This is useful when you use Elastic, Splunk or any other platform to search on object properties. Default is true. Masking only works when this options is enabled.
        /// </summary>
        public bool LogResponseBodyAsStructuredObject { get; set; } = true;

        /// <summary>
        /// Properties to mask before logging to output to prevent sensitive data leakage
        /// </summary>
        public IList<string> MaskedProperties { get; } = new List<string>() {"*password*", "*token*", "*secret*", "*bearer*", "*authorization*", "*otp"};

        /// <summary>
        /// Mask format to replace for masked properties
        /// </summary>
        public string MaskFormat { get; set; } = "*** MASKED ***";
        
        /// <summary>
        /// Maximum allowed length of response body text to capture in logs. response bodies that exceeds this limit will be trimmed.
        /// </summary>
        public int ResponseBodyLogTextLengthLimit { get; set; } = 4000;

        /// <summary>
        /// Maximum allowed length of request body text to capture in logs. request bodies that exceeds this limit will be trimmed.
        /// </summary>
        public int RequestBodyLogTextLengthLimit { get; set; } = 4000;
        public ILogger Logger { get; internal set; }
        public IEnumerable<AutoWrapperExcludePath> ExcludePaths { get; set; } = null;
        
        /// <summary>
        /// A function returning the <see cref="LogEventLevel"/> based on the <see cref="HttpContext"/>, the number of
        /// elapsed milliseconds required for handling the request, and an <see cref="Exception" /> if one was thrown.
        /// The default behavior returns <see cref="LogEventLevel.Error"/> when the response status code is greater than 499 or if the
        /// <see cref="Exception"/> is not null. Also default log level for 4xx range errors set to <see cref="LogEventLevel.Warning"/>   
        /// </summary>
        /// <value>
        /// A function returning the <see cref="LogEventLevel"/>.
        /// </value>
        public Func<HttpContext, double, Exception, LogEventLevel> GetLevel { get; set; }

        /// <summary>
        /// A function returning the <see cref="LogEntryParameters"/> based on the <see cref="HttpContextInfo"/> information,
        /// default behavior is logging message with template "HTTP request {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms"
        /// and attaching HTTP contextual data <see cref="HttpContextInfo"/> as property named "Context"
        /// </summary>
        public Func<HttpContextInfo, LogEntryParameters> GetLogMessageAndProperties { get; set; }
        
        public AutoWrapperOptions()
        {
            GetLevel = DefaultGetLevel;
            GetLogMessageAndProperties = DefaultLogMessageAndProperties;
        }
        
        private static LogEventLevel DefaultGetLevel(HttpContext ctx, double _, Exception ex)
        {
            var level = LogEventLevel.Information;
            if (ctx.Response.StatusCode >= 500)
            {
                level = LogEventLevel.Error;
            }
            else if (ctx.Response.StatusCode >= 400)
            {
                level = LogEventLevel.Warning;
            }
            else if (ex != null)
            {
                level = LogEventLevel.Error;
            }

            return level;
        }
        
        private static LogEntryParameters DefaultLogMessageAndProperties(HttpContextInfo h)
        {
            return new LogEntryParameters()
            {
                MessageTemplate = "HTTP Request {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms",
                MessageParameters = new object[]{ h.Request.Method, h.Request.Path, h.Response.StatusCode, h.Response.ElapsedMilliseconds},
                AdditionalProperties = { ["Context"] = h }
            };
        }
    }
}
