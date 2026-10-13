using Xunit;

namespace HyCADTool.LinuxTests
{
    public sealed class CommandCatalogFileFixture
    {
    }

    [CollectionDefinition("CommandCatalogFile")]
    public sealed class CommandCatalogFileCollection : ICollectionFixture<CommandCatalogFileFixture>
    {
    }
}
