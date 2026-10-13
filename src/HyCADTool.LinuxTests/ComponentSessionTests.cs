using System;
using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain.Components;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class ComponentSessionTests
    {
        [Fact]
        public void Sessions_are_isolated_by_document_key()
        {
            string key = "doc-a";
            try
            {
                var id = Guid.NewGuid();
                var region = new ComponentRegion
                {
                    Id = Guid.NewGuid(),
                    Type = ComponentType.Slab,
                    Polygon = new Polyline2D(new[]
                    {
                        new Point2D(0, 0),
                        new Point2D(1, 0),
                        new Point2D(0, 1)
                    }, true)
                };

                ComponentSession.SetDocumentKeyResolver(() => key);
                ComponentSession.Set(id, null, new[] { region });

                key = "doc-b";
                Assert.Equal(Guid.Empty, ComponentSession.CurrentSessionId);
                Assert.Empty(ComponentSession.Regions);

                key = "doc-a";
                Assert.Equal(id, ComponentSession.CurrentSessionId);
                Assert.Single(ComponentSession.Regions);

                ComponentSession.ForgetDocument("doc-a");
                Assert.Equal(Guid.Empty, ComponentSession.CurrentSessionId);
            }
            finally
            {
                ComponentSession.SetDocumentKeyResolver(null);
                ComponentSession.ForgetDocument("doc-a");
                ComponentSession.ForgetDocument("doc-b");
                ComponentSession.Clear();
            }
        }
    }
}
