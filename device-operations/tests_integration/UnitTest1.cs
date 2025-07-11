using Xunit;

namespace DeviceOperations.Tests;

public class SimpleTests
{
    [Fact]
    public void Test_FrameworkIsWorking()
    {
        // Simple test to verify the test framework itself is working
        var result = 2 + 2;
        Assert.Equal(4, result);
    }

    [Fact]
    public void Test_BasicAssertions()
    {
        // Test basic assertions work
        Assert.True(true);
        Assert.False(false);
        Assert.NotNull("hello");
    }
}
