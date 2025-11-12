using Xunit.Abstractions;

namespace Argentini.Duid.Tests;

public class SharedTestBase
{
    protected ITestOutputHelper? TestOutputHelper { get; }
    protected bool IsRelease { get; }

    protected SharedTestBase(ITestOutputHelper testOutputHelper)
    {
        TestOutputHelper = testOutputHelper;
        
#if RELEASE
        IsRelease = true;
#else
        IsRelease = false;
#endif
    }
}