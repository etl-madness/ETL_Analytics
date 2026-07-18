using AutoSysJilBlazor.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoSysJilBlazor.Tests.Pages;

[TestFixture]
public class ErrorModelTests
{
    [Test]
    public void OnGet_UsesHttpContextTraceIdentifier_WhenNoCurrentActivity()
    {
        var model = new ErrorModel(NullLogger<ErrorModel>.Instance)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-123" } }
        };

        model.OnGet();

        Assert.That(model.RequestId, Is.EqualTo("trace-123"));
        Assert.That(model.ShowRequestId, Is.True);
    }

    [Test]
    public void ShowRequestId_ReturnsFalse_WhenRequestIdIsNullOrEmpty()
    {
        var model = new ErrorModel(NullLogger<ErrorModel>.Instance);

        model.RequestId = null;
        Assert.That(model.ShowRequestId, Is.False);

        model.RequestId = string.Empty;
        Assert.That(model.ShowRequestId, Is.False);
    }
}
