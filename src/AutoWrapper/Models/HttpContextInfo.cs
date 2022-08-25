using System.Collections.Generic;
using Serilog;

namespace AutoWrapper.Models
{
    /// <summary>
    /// HTTP request/response contextual properties
    /// </summary>
    public class HttpContextInfo
    {
        /// <summary>
        /// HTTP request information
        /// </summary>
        public HttpRequestInfo Request { get; set; }
        
        /// <summary>
        /// HTTP response information
        /// </summary>
        public HttpResponseInfo Response { get; set; }
        
        /// <summary>
        /// HTTP request additional properties from <see cref="IDiagnosticContext"/>.
        /// </summary>
        public Dictionary<string, string> Diagnostics { get; set; }

        public Dictionary<string,object> Properties { get; set; }
    }
}