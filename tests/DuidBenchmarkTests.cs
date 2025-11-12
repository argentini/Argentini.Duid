using Xunit.Abstractions;

namespace Argentini.Duid.Tests;

public class DuidBenchmarkTests(ITestOutputHelper testOutputHelper) : SharedTestBase(testOutputHelper)
{
    private SharedBenchmarkCode Shared { get; } = new(testOutputHelper);

    [Fact]
    public void NewDuid()
    {
        Shared.IterationSetup();
        Shared.BenchmarkMethod("NewDuid",
            () =>
            {
                _ = Duid.NewDuid();
            });

        Shared.OutputTotalTime();
    }
    
    [Fact]
    public void NewGuid()
    {
        Shared.IterationSetup();
        Shared.BenchmarkMethod("NewGuid",
            () =>
            {
                _ = Guid.NewGuid();
            });

        Shared.OutputTotalTime();
    }
    
    [Fact]
    public void DuidToString()
    {
        Shared.IterationSetup();
        
        var duid = Duid.NewDuid();

        Shared.BenchmarkMethod("DuidToString",
            () =>
            {
                _ = duid.ToString();
            });

        Shared.OutputTotalTime();
    }
    
    [Fact]
    public void GuidToString()
    {
        Shared.IterationSetup();
        
        var guid = Guid.NewGuid();
        
        Shared.BenchmarkMethod("GuidToString",
            () =>
            {
                _ = guid.ToString();
            });

        Shared.OutputTotalTime();
    }
}
