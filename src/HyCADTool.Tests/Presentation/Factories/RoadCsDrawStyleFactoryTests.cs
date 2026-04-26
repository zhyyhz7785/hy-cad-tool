using HyCADTool.Features.Road.CrossSection.Services;
using Xunit;

namespace HyCADTool.Tests.Presentation.Factories
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
