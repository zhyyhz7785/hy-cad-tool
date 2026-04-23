using HyCADTool.Refactored.Presentation.Factories;
using Xunit;

namespace HyCADTool.Refactored.Tests.Presentation.Factories
{
    public class RoadCsDrawStyleFactoryTests
    {
        [Fact]
        public void Factory_CanBeConstructed()
        {
            _ = new RoadCsDrawStyleFactory();
        }
    }
}
