using TagBites.ComponentModel.Composition.Tests.Models;
using Xunit;

namespace TagBites.ComponentModel.Composition.Tests;

public class ContractDefinitionTests
{
    [Fact]
    public void EqualityComparison()
    {
        var left = new ContractDefinition("name", typeof(ITestService));
        var right = new ContractDefinition("name", typeof(ITestService));

        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.False(left == new ContractDefinition(null, typeof(ITestService)));
        Assert.False(left == new ContractDefinition("name", typeof(IDuplicateService)));
    }

    [Fact]
    public void NullContractType()
    {
        Assert.Throws<ArgumentNullException>(() => new ContractDefinition(null, null!));
    }
}
