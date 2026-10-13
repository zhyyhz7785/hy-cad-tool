using System;
using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain.Components;
using HyCADTool.Shared.AutoCAD.Session;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class ComponentSessionTests
    {
        [Fact]
        public void Sessions_are_isolated_by_document_object()
        {
            var docA = new object();
            var docB = new object();
            object current = docA;
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

                ComponentSession.SetDocumentKeyResolver(() => current);
                ComponentSession.Set(id, null, new[] { region });

                current = docB;
                Assert.Equal(Guid.Empty, ComponentSession.CurrentSessionId);
                Assert.Empty(ComponentSession.Regions);

                current = docA;
                Assert.Equal(id, ComponentSession.CurrentSessionId);
                Assert.Single(ComponentSession.Regions);

                ComponentSession.ForgetDocument(docA);
                Assert.Equal(Guid.Empty, ComponentSession.CurrentSessionId);
            }
            finally
            {
                ComponentSession.SetDocumentKeyResolver(null);
                ComponentSession.ForgetDocument(docA);
                ComponentSession.ForgetDocument(docB);
                ComponentSession.Clear();
            }
        }

        [Fact]
        public void SaveAs_keeps_the_same_object_key()
        {
            var database = new object();
            try
            {
                ComponentSession.SetDocumentKeyResolver(() => database);
                var id = Guid.NewGuid();
                ComponentSession.Set(id, null, Array.Empty<ComponentRegion>());

                // 另存为只改文件名，Database 对象不变，槽位必须还在。
                Assert.Equal(id, ComponentSession.CurrentSessionId);
            }
            finally
            {
                ComponentSession.SetDocumentKeyResolver(null);
                ComponentSession.ForgetDocument(database);
                ComponentSession.Clear();
            }
        }

        [Fact]
        public void No_active_document_does_not_write_a_shared_slot()
        {
            var docA = new object();
            try
            {
                ComponentSession.SetDocumentKeyResolver(null);
                ComponentSession.Set(Guid.NewGuid(), null, new[]
                {
                    new ComponentRegion
                    {
                        Id = Guid.NewGuid(),
                        Type = ComponentType.Slab,
                        Polygon = new Polyline2D(new[]
                        {
                            new Point2D(0, 0),
                            new Point2D(1, 0),
                            new Point2D(0, 1)
                        }, true)
                    }
                });
                Assert.Equal(Guid.Empty, ComponentSession.CurrentSessionId);
                Assert.Empty(ComponentSession.Regions);

                ComponentSession.SetDocumentKeyResolver(() => docA);
                Assert.Equal(Guid.Empty, ComponentSession.CurrentSessionId);
                Assert.Empty(ComponentSession.Regions);
            }
            finally
            {
                ComponentSession.SetDocumentKeyResolver(null);
                ComponentSession.ForgetDocument(docA);
                ComponentSession.Clear();
            }
        }
    }

    public class DocumentSessionStoreTests
    {
        private sealed class CounterState
        {
            public int Value;
        }

        [Fact]
        public void Get_is_isolated_and_Forget_disposes_slot()
        {
            var a = new object();
            var b = new object();
            DocumentSessionStore.Get<CounterState>(a).Value = 3;
            DocumentSessionStore.Get<CounterState>(b).Value = 7;

            Assert.Equal(3, DocumentSessionStore.Get<CounterState>(a).Value);
            Assert.Equal(7, DocumentSessionStore.Get<CounterState>(b).Value);

            DocumentSessionStore.Forget(a);
            Assert.Equal(0, DocumentSessionStore.Get<CounterState>(a).Value);
            Assert.Equal(7, DocumentSessionStore.Get<CounterState>(b).Value);

            DocumentSessionStore.Forget(a);
            DocumentSessionStore.Forget(b);
        }

        private sealed class PendingGuidState
        {
            public Guid? Value;
        }

        [Fact]
        public void Pending_flags_do_not_cross_document_keys()
        {
            var a = new object();
            var b = new object();
            var id = Guid.NewGuid();
            DocumentSessionStore.Get<PendingGuidState>(a).Value = id;

            Assert.Equal(id, DocumentSessionStore.Get<PendingGuidState>(a).Value);
            Assert.Null(DocumentSessionStore.Get<PendingGuidState>(b).Value);

            DocumentSessionStore.Forget(a);
            Assert.Null(DocumentSessionStore.Get<PendingGuidState>(a).Value);

            DocumentSessionStore.Forget(a);
            DocumentSessionStore.Forget(b);
        }
    }
}
