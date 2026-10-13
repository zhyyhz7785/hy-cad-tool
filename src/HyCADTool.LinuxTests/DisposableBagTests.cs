using System;
using HyCADTool.Shared.AutoCAD.Interaction;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public class DisposableBagTests
    {
        private sealed class Probe : IDisposable
        {
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }

        [Fact]
        public void Dispose_releases_remaining_items()
        {
            var kept = new Probe();
            var dropped = new Probe();
            using (var bag = new DisposableBag())
            {
                bag.Add(kept);
                bag.Add(dropped);
                bag.Detach(kept);
            }

            Assert.False(kept.Disposed);
            Assert.True(dropped.Disposed);
        }

        [Fact]
        public void Dispose_is_idempotent()
        {
            var probe = new Probe();
            var bag = new DisposableBag();
            bag.Add(probe);
            bag.Dispose();
            bag.Dispose();
            Assert.True(probe.Disposed);
        }
    }
}
