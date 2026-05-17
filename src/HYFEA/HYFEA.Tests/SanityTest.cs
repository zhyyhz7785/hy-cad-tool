using Xunit;

namespace HYFEA.Tests;

public sealed class SanityTest
{
    [Fact]
    public void One_plus_one_is_two() => Assert.Equal(2, 1 + 1);
}
