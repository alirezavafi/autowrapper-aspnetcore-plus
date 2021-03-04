using System;
using System.Collections.Generic;
using AutoWrapper.Base;
using Newtonsoft.Json;
using Serilog;

namespace AutoWrapper
{
    public class AutoWrapperOptions :OptionBase
    {
        public bool UseCustomSchema { get; set; } = false;
        public ReferenceLoopHandling ReferenceLoopHandling { get; set; } = ReferenceLoopHandling.Ignore;
        public bool UseCustomExceptionFormat { get; set; } = false;
        public bool UseApiProblemDetailsException { get; set; } = false;
        public string SwaggerPath { get; set; } = "/swagger";
        public bool IgnoreWrapForOkRequests { get; set; } = false;
        public bool LogRequestDataOnException { get; set; } = true;
        public bool LogResponseDataOnException { get; set; } = true;
        public bool ShouldLogRequestData { get; set; } = true;
        public bool ShouldLogResponseData { get; set; } = false;
        public IList<string> MaskedProperties { get; } =
            new List<string>() {"*password*", "*token*", "*clientsecret*", "*bearer*", "*authorization*", "*client-secret*","*otp"};
        public string MaskFormat { get; set; } = "*** MASKED ***";
        public int ResponseBodyTextLengthLogLimit { get; set; } = 4000;
        public int RequestBodyTextLengthLogLimit { get; set; } = 4000;
        public ILogger Logger { get; set; }
    }
}
