using System;
using System.Collections.Generic;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Events.Road;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Infrastructure.Road
{
    /// <summary>
    /// P1：RoadProfileService 行为测试（GetOrCreate / Replace / Delete + 事件总线）。
    ///
    /// 不依赖 AutoCAD：通过 <see cref="RoadDesignRegistry.Replace"/> 直接挂载预构造的 RoadDesign。
    /// </summary>
    public class RoadProfileServiceTests
    {
        private static (RoadProfileService svc, RoadDesignRegistry reg, RoadEventBus bus, string docName, Guid alnId)
            BuildSut(string docName = "C:\\prj\\test.dwg")
        {
            var bus = new RoadEventBus();
            var reg = new RoadDesignRegistry(bus);
            var design = reg.GetOrCreate(docName);
            var alignment = new Alignment { Id = Guid.NewGuid(), Name = "K0-K1" };
            design.Alignments.Add(alignment);
            return (new RoadProfileService(reg, bus), reg, bus, docName, alignment.Id);
        }

        [Fact]
        public void GetOrCreateDesignProfile_FirstCall_CreatesAndPublishesCreatedEvent()
        {
            var (svc, _, bus, doc, alnId) = BuildSut();
            ProfileChangedEvent received = null;
            using (bus.Subscribe<ProfileChangedEvent>(e => received = e))
            {
                var (profile, created) = svc.GetOrCreateDesignProfile(doc, alnId);

                created.Should().BeTrue();
                profile.Should().NotBeNull();
                profile.IsDesignProfile.Should().BeTrue();
                profile.DesignSpeed.Should().Be(60);
                received.Should().NotBeNull();
                received.Kind.Should().Be(RoadChangeKind.Created);
                received.AlignmentId.Should().Be(alnId);
                received.ProfileId.Should().Be(profile.Id);
            }
        }

        [Fact]
        public void GetOrCreateDesignProfile_Subsequent_ReturnsExistingNoEvent()
        {
            var (svc, _, bus, doc, alnId) = BuildSut();
            var (first, _) = svc.GetOrCreateDesignProfile(doc, alnId);

            int eventCount = 0;
            using (bus.Subscribe<ProfileChangedEvent>(_ => eventCount++))
            {
                var (second, created) = svc.GetOrCreateDesignProfile(doc, alnId);

                created.Should().BeFalse();
                second.Should().BeSameAs(first);
                eventCount.Should().Be(0);
            }
        }

        [Fact]
        public void ReplaceVertices_OverwritesAndPublishesUpdatedEvent()
        {
            var (svc, _, bus, doc, alnId) = BuildSut();
            var (profile, _) = svc.GetOrCreateDesignProfile(doc, alnId);
            var newVerts = new List<ProfileVertex>
            {
                new ProfileVertex { Station = 0, Elevation = 100 },
                new ProfileVertex { Station = 200, Elevation = 110, CurveRadius = 2000 },
                new ProfileVertex { Station = 400, Elevation = 100 },
            };

            ProfileChangedEvent received = null;
            using (bus.Subscribe<ProfileChangedEvent>(e => received = e))
            {
                bool ok = svc.ReplaceVertices(doc, alnId, profile.Id, newVerts);

                ok.Should().BeTrue();
                profile.Vertices.Should().HaveCount(3);
                profile.Vertices[1].Elevation.Should().Be(110);
                profile.LastModifiedUtc.Should().BeAfter(DateTime.UtcNow.AddSeconds(-2));
                received.Kind.Should().Be(RoadChangeKind.Updated);
            }
        }

        [Fact]
        public void ReplaceVertices_DoesNotShareReferencesWithCaller()
        {
            var (svc, _, _, doc, alnId) = BuildSut();
            var (profile, _) = svc.GetOrCreateDesignProfile(doc, alnId);
            var v = new ProfileVertex { Station = 0, Elevation = 100 };

            svc.ReplaceVertices(doc, alnId, profile.Id, new List<ProfileVertex> { v });

            // 调用方修改原对象不应影响内部存储
            v.Elevation = 999;
            profile.Vertices[0].Elevation.Should().Be(100);
        }

        [Fact]
        public void ReplaceVertices_UnknownProfile_ReturnsFalseNoEvent()
        {
            var (svc, _, bus, doc, alnId) = BuildSut();
            int events = 0;
            using (bus.Subscribe<ProfileChangedEvent>(_ => events++))
            {
                bool ok = svc.ReplaceVertices(doc, alnId, Guid.NewGuid(), new List<ProfileVertex>());
                ok.Should().BeFalse();
                events.Should().Be(0);
            }
        }

        [Fact]
        public void Delete_ExistingProfile_RemovesAndPublishesDeletedEvent()
        {
            var (svc, reg, bus, doc, alnId) = BuildSut();
            var (profile, _) = svc.GetOrCreateDesignProfile(doc, alnId);

            ProfileChangedEvent received = null;
            using (bus.Subscribe<ProfileChangedEvent>(e => received = e))
            {
                bool ok = svc.Delete(doc, alnId, profile.Id);

                ok.Should().BeTrue();
                received.Kind.Should().Be(RoadChangeKind.Deleted);
                reg.TryGet(doc, out var design).Should().BeTrue();
                design.Alignments[0].Profiles.Should().BeEmpty();
            }
        }

        [Fact]
        public void Delete_UnknownProfile_ReturnsFalseNoEvent()
        {
            var (svc, _, bus, doc, alnId) = BuildSut();
            int events = 0;
            using (bus.Subscribe<ProfileChangedEvent>(_ => events++))
            {
                svc.Delete(doc, alnId, Guid.NewGuid()).Should().BeFalse();
                events.Should().Be(0);
            }
        }

        [Fact]
        public void GetOrCreateDesignProfile_NoRoadDesign_Throws()
        {
            var bus = new RoadEventBus();
            var reg = new RoadDesignRegistry(bus);
            var svc = new RoadProfileService(reg, bus);

            Action act = () => svc.GetOrCreateDesignProfile("ghost.dwg", Guid.NewGuid());
            act.Should().Throw<InvalidOperationException>().WithMessage("*未初始化*");
        }

        [Fact]
        public void GetOrCreateDesignProfile_UnknownAlignment_Throws()
        {
            var (svc, _, _, doc, _) = BuildSut();

            Action act = () => svc.GetOrCreateDesignProfile(doc, Guid.NewGuid());
            act.Should().Throw<ArgumentException>().WithMessage("*Alignment*");
        }

        [Fact]
        public void ReplaceVertices_NullList_Throws()
        {
            var (svc, _, _, doc, alnId) = BuildSut();
            var (profile, _) = svc.GetOrCreateDesignProfile(doc, alnId);

            Action act = () => svc.ReplaceVertices(doc, alnId, profile.Id, null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetOrCreateDesignProfile_PrefersExistingDesignProfileEvenIfGroundProfileExists()
        {
            var (svc, reg, bus, doc, alnId) = BuildSut();
            // 假装已经有一个地面线 + 一个空设计 Profile
            reg.TryGet(doc, out var design).Should().BeTrue();
            var aln = design.Alignments[0];
            aln.Profiles.Add(new Profile { Name = "地面线", IsDesignProfile = false });
            var existing = new Profile { Name = "设计标高", IsDesignProfile = true };
            aln.Profiles.Add(existing);

            int events = 0;
            using (bus.Subscribe<ProfileChangedEvent>(_ => events++))
            {
                var (got, created) = svc.GetOrCreateDesignProfile(doc, alnId);

                created.Should().BeFalse();
                got.Should().BeSameAs(existing);
                events.Should().Be(0);
            }
        }
    }
}
