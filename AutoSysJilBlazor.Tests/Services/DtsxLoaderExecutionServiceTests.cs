using AutoSysJilBlazor.Models;
using AutoSysJilBlazor.Services;
using AutoSysJilBlazor.Tests.TestDoubles;

namespace AutoSysJilBlazor.Tests.Services;

[TestFixture]
public class DtsxLoaderExecutionServiceTests
{
    [Test]
    public void RunAsync_WhenRepoRootCannotBeResolved_ThrowsInvalidOperationException()
    {
        var rootPath = Path.GetPathRoot(Path.GetTempPath()) ?? Path.GetTempPath();
        var env = new TestWebHostEnvironment { ContentRootPath = rootPath };
        var service = new DtsxLoaderExecutionService(env);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.RunAsync(new DtsxLoaderSettings(), _ => { }, CancellationToken.None));
    }

    [Test]
    public void RunAsync_WhenConsoleProjectDoesNotExist_ThrowsFileNotFoundException()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dtsx-exec-tests", Guid.NewGuid().ToString("N"));
        var contentRoot = Path.Combine(tempRoot, "AutoSysJilBlazor");

        Directory.CreateDirectory(contentRoot);

        try
        {
            var env = new TestWebHostEnvironment { ContentRootPath = contentRoot };
            var service = new DtsxLoaderExecutionService(env);

            Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await service.RunAsync(new DtsxLoaderSettings(), _ => { }, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
