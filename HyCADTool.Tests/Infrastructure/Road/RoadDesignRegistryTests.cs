using System;
using FluentAssertions;
using HyCADTool.Domain.Events.Road;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using Xunit;

namespace HyCADTool.Tests.Infrastructure.Road
{
    /// <summary>
    /// P1：<see cref="RoadDesignRegistry"/> 行为测试。
    ///
    /// 覆盖：
    /// - GetOrCreate 幂等 + 文档名大小写不敏感；
    /// - TryGet 无副作用；
    /// - Replace 发布 <see cref="RoadDesignReloadedEvent"/>；
    /// - Remove / Snapshot 的基本语义。
    ///
    /// 不依赖 AutoCAD 程序集，属纯 Infrastructure 层单元测试。
    /// </summary>
    public class RoadDesignRegistryTests
    {
        [Fact]
        public void GetOrCreate_SameDocumentName_ReturnsSameInstance()
        {
            var registry = new RoadDesignRegistry(new RoadEventBus());

            var a = registry.GetOrCreate("C:\\prj\\demo.dwg");
            var b = registry.GetOrCreate("C:\\prj\\demo.dwg");

            a.Should().BeSameAs(b);
        }

        [Fact]
        public void GetOrCreate_IsCaseInsensitive()
        {
            var registry = new RoadDesignRegistry(new RoadEventBus());

            var lower = registry.GetOrCreate("c:\\prj\\demo.dwg");
            var upper = registry.GetOrCreate("C:\\PRJ\\DEMO.DWG");

            upper.Should().BeSameAs(lower,
                "AutoCAD MdiActiveDocument.Name 在不同会话 / 用户路径下可能大小写不一致");
        }

        [Fact]
        public void GetOrCreate_UsesFileNameAsProjectName()
        {
            var registry = new RoadDesignRegistry(new RoadEventBus());

            var design = registry.GetOrCreate("C:\\work\\city-road-3.dwg");

            design.ProjectName.Should().Be("city-road-3");
        }

        [Fact]
        public void GetOrCreate_WithEmptyName_UsesDefault()
        {
            var registry = new RoadDesignRegistry(new RoadEventBus());

            var a = registry.GetOrCreate(null);
            var b = registry.GetOrCreate("");

            a.Should().BeSameAs(b, "空名字都映射到 'default' 槽位");
        }

        [Fact]
        public void TryGet_BeforeCreate_ReturnsFalseAndNull()
        {
            var registry = new RoadDesignRegistry(new RoadEventBus());

            var ok = registry.TryGet("never-created.dwg", out var design);

            ok.Should().BeFalse();
            design.Should().BeNull();
        }

        [Fact]
        public void Replace_PublishesRoadDesignReloadedEvent()
        {
            var bus = new RoadEventBus();
            RoadDesignReloadedEvent received = null;
            bus.Subscribe<RoadDesignReloadedEvent>(e => received = e);

            var registry = new RoadDesignRegistry(bus);
            var fresh = new RoadDesign { ProjectName = "imported" };
            registry.Replace("demo.dwg", fresh);

            received.Should().NotBeNull();
            received.RoadDesignId.Should().Be(fresh.Id);
        }

        [Fact]
        public void Replace_WithNullDesign_Throws()
        {
            var registry = new RoadDesignRegistry(new RoadEventBus());
            Action act = () => registry.Replace("demo.dwg", null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Remove_RemovesOnlyTargetEntry()
        {
            var registry = new RoadDesignRegistry(new RoadEventBus());
            registry.GetOrCreate("a.dwg");
            registry.GetOrCreate("b.dwg");

            registry.Remove("a.dwg");

            registry.TryGet("a.dwg", out _).Should().BeFalse();
            registry.TryGet("b.dwg", out _).Should().BeTrue();
        }

        [Fact]
        public void Snapshot_IsDetachedCopy_DoesNotMutateOriginal()
        {
            var registry = new RoadDesignRegistry(new RoadEventBus());
            registry.GetOrCreate("a.dwg");

            var snapshot = registry.Snapshot();
            registry.GetOrCreate("b.dwg");

            snapshot.Count.Should().Be(1, "Snapshot 必须是独立快照，后续新增不应反映到旧快照");
        }

        [Fact]
        public void Rekey_MovesDesign_AndPreservesIdentity()
        {
            var registry = new RoadDesignRegistry(new RoadEventBus());
            var original = registry.GetOrCreate("C:\\prj\\Drawing1.dwg");

            var ok = registry.Rekey("C:\\prj\\Drawing1.dwg", "E:\\out\\Drawing3.dwg");

            ok.Should().BeTrue();
            registry.TryGet("C:\\prj\\Drawing1.dwg", out _).Should().BeFalse();
            registry.TryGet("E:\\out\\Drawing3.dwg", out var moved).Should().BeTrue();
            moved.Should().BeSameAs(original, "Rekey 只改 key 不改 value 引用");
        }

        [Fact]
        public void Rekey_WhenOldKeyMissing_ReturnsFalse()
        {
            var registry = new RoadDesignRegistry(new RoadEventBus());

            var ok = registry.Rekey("ghost.dwg", "new.dwg");

            ok.Should().BeFalse();
        }

        [Fact]
        public void Rekey_WhenNewKeyAlreadyExists_ReturnsFalseWithoutMutation()
        {
            var registry = new RoadDesignRegistry(new RoadEventBus());
            var old = registry.GetOrCreate("old.dwg");
            var existing = registry.GetOrCreate("new.dwg");

            var ok = registry.Rekey("old.dwg", "new.dwg");

            ok.Should().BeFalse("policy: 不允许静默覆盖目标槽位，由调用方决定策略");
            registry.TryGet("old.dwg", out var stillOld).Should().BeTrue();
            registry.TryGet("new.dwg", out var stillNew).Should().BeTrue();
            stillOld.Should().BeSameAs(old);
            stillNew.Should().BeSameAs(existing);
        }

        [Fact]
        public void Rekey_SameKey_IsNoop()
        {
            var registry = new RoadDesignRegistry(new RoadEventBus());
            registry.GetOrCreate("SAME.dwg");

            var ok = registry.Rekey("same.dwg", "SAME.dwg");

            ok.Should().BeFalse("大小写不敏感相同 → 视作 no-op");
        }

        [Fact]
        public void Rekey_DoesNotPublishReloadedEvent()
        {
            var bus = new RoadEventBus();
            bool called = false;
            bus.Subscribe<RoadDesignReloadedEvent>(_ => called = true);

            var registry = new RoadDesignRegistry(bus);
            registry.GetOrCreate("a.dwg");

            registry.Rekey("a.dwg", "b.dwg");

            called.Should().BeFalse("Rekey 是基础设施级迁移，刻意不发事件；触发写盘的语义留给调用方");
        }
    }
}
