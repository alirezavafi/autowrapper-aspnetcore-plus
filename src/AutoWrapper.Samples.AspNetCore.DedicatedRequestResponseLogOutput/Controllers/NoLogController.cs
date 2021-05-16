using AutoWrapper.Filters;
using Microsoft.AspNetCore.Mvc;

namespace AutoWrapper.Samples.AspNetCore.DedicatedRequestResponseLogOutput.Controllers
{
    [IgnoreLog]
    public class NoLogController : ControllerBase
    {
        public IActionResult Test()
        {
            return Ok("Test");
        }

        public IActionResult Test2()
        {
            return Ok(new
            {
                A = "123",
                B = 12,
            });
        }
    }
}