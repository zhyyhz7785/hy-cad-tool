using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// M6.2 HistoryService：目录约定 / 序列化回环 / Checksum 校验 / 删除 / 索引损坏降级等。
    /// </summary>
    public class HistoryServiceTests : IDisposable
    {
        private readonly string _tempRoot;
        private readonly string _dwgPath;
        private readonly string _historyDir;

        public HistoryServiceTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "hycad-history-tests-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_tempRoot);
            _dwgPath = Path.Combine(_tempRoot, "project.dwg");
            File.WriteAllText(_dwgPath, "dummy"); // fake dwg placeholder
            _historyDir = HistoryService.GetHistoryDirectory(_dwgPath);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true); }
            catch { /* best-effort cleanup */ }
        }

        private static RoadDesign MakeDesignWithAlignment(string name = "主线")
        {
            var d = new RoadDesign { ProjectName = "TestProj" };
            d.Alignments.Add(new Alignment { Name = name, StartStation = 0 });
            return d;
        }

        [Fact]
        public void GetHistoryDirectory_AppendsSuffix_NextToDwg()
        {
            _historyDir.Should().EndWith(".roaddesign.history");
            _historyDir.Should().Contain("project");
            Path.GetDirectoryName(_historyDir).Should().Be(_tempRoot);
        }

        [Fact]
        public void GetHistoryDirectory_NullOrEmpty_ReturnsNull()
        {
            HistoryService.GetHistoryDirectory(null).Should().BeNull();
            HistoryService.GetHistoryDirectory("").Should().BeNull();
        }

        [Fact]
        public void LoadIndex_EmptyWhenNoDirectory()
        {
            var svc = new HistoryService();
            var idx = svc.LoadIndex(_historyDir);
            idx.Should().NotBeNull();
            idx.Entries.Should().BeEmpty();
            idx.NextSeqNo.Should().Be(1);
        }

        [Fact]
        public void CreateSnapshot_WritesFileAndIndexRow()
        {
            var svc = new HistoryService();
            var design = MakeDesignWithAlignment();
            var entry = svc.CreateSnapshot(_historyDir, design, "导线法新建路线_路线1");

            entry.Should().NotBeNull();
            entry.SeqNo.Should().Be(1);
            entry.Description.Should().Be("导线法新建路线_路线1");
            entry.Checksum.Should().NotBeNullOrEmpty();
            entry.SchemaVersion.Should().Be(SchemaVersion.Current, "快照里记录当前 schema；045 / M2 后升到 2.0");

            string snapPath = Path.Combine(_historyDir, entry.SnapshotFileName);
            File.Exists(snapPath).Should().BeTrue();

            string indexPath = HistoryService.GetIndexFilePath(_historyDir);
            File.Exists(indexPath).Should().BeTrue();
        }

        [Fact]
        public void CreateSnapshot_SequenceIncreases()
        {
            var svc = new HistoryService();
            svc.CreateSnapshot(_historyDir, MakeDesignWithAlignment("L1"), "第一");
            svc.CreateSnapshot(_historyDir, MakeDesignWithAlignment("L2"), "第二");
            var e3 = svc.CreateSnapshot(_historyDir, MakeDesignWithAlignment("L3"), "第三");

            e3.SeqNo.Should().Be(3);
            var idx = svc.LoadIndex(_historyDir);
            idx.Entries.Should().HaveCount(3);
            idx.Entries.Select(e => e.SeqNo).Should().Equal(1, 2, 3);
            idx.NextSeqNo.Should().Be(4);
        }

        [Fact]
        public void Restore_ReturnsDesignWithSameAlignmentName()
        {
            var svc = new HistoryService();
            var entry = svc.CreateSnapshot(_historyDir, MakeDesignWithAlignment("快速路"), "K快速路");
            var restored = svc.Restore(_historyDir, entry);

            restored.Should().NotBeNull();
            restored.Alignments.Should().HaveCount(1);
            restored.Alignments[0].Name.Should().Be("快速路");
        }

        [Fact]
        public void RestoreBySeqNo_Works()
        {
            var svc = new HistoryService();
            svc.CreateSnapshot(_historyDir, MakeDesignWithAlignment("L1"), "第一");
            svc.CreateSnapshot(_historyDir, MakeDesignWithAlignment("L2"), "第二");

            var r = svc.RestoreBySeqNo(_historyDir, 2);
            r.Should().NotBeNull();
            r.Alignments[0].Name.Should().Be("L2");
        }

        [Fact]
        public void Restore_DetectsCorruptedSnapshot()
        {
            var svc = new HistoryService();
            var entry = svc.CreateSnapshot(_historyDir, MakeDesignWithAlignment(), "X");
            // 破坏快照文件
            var path = Path.Combine(_historyDir, entry.SnapshotFileName);
            File.AppendAllText(path, "/*corrupted*/");

            Action act = () => svc.Restore(_historyDir, entry);
            act.Should().Throw<InvalidDataException>().WithMessage("*Checksum*");
        }

        [Fact]
        public void DeleteEntry_RemovesIndexRowAndFile()
        {
            var svc = new HistoryService();
            var e1 = svc.CreateSnapshot(_historyDir, MakeDesignWithAlignment("A"), "一");
            var e2 = svc.CreateSnapshot(_historyDir, MakeDesignWithAlignment("B"), "二");

            svc.DeleteEntry(_historyDir, e1.SeqNo).Should().BeTrue();
            var idx = svc.LoadIndex(_historyDir);
            idx.Entries.Should().HaveCount(1);
            idx.Entries[0].SeqNo.Should().Be(e2.SeqNo);

            File.Exists(Path.Combine(_historyDir, e1.SnapshotFileName)).Should().BeFalse();
            File.Exists(Path.Combine(_historyDir, e2.SnapshotFileName)).Should().BeTrue();
        }

        [Fact]
        public void DeleteEntry_NonExisting_IsIdempotent()
        {
            var svc = new HistoryService();
            svc.DeleteEntry(_historyDir, 9999).Should().BeTrue();
        }

        [Fact]
        public void CreateSnapshot_IgnoresNullInputs()
        {
            var svc = new HistoryService();
            svc.CreateSnapshot(null, MakeDesignWithAlignment(), "X").Should().BeNull();
            svc.CreateSnapshot(_historyDir, null, "X").Should().BeNull();
        }

        [Fact]
        public void LoadIndex_CorruptedIndex_ReturnsEmpty()
        {
            Directory.CreateDirectory(_historyDir);
            File.WriteAllText(HistoryService.GetIndexFilePath(_historyDir), "{not-json");
            var svc = new HistoryService();
            var idx = svc.LoadIndex(_historyDir);
            idx.Should().NotBeNull();
            idx.Entries.Should().BeEmpty();
            idx.NextSeqNo.Should().Be(1);
        }

        [Fact]
        public void ComputeSha256Hex_KnownValue()
        {
            string hello = HistoryService.ComputeSha256Hex(System.Text.Encoding.UTF8.GetBytes("hello"));
            // sha256("hello") 标准值
            hello.Should().Be("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
        }

        [Fact]
        public void MakeSnapshotFileName_FormatsCorrectly()
        {
            var t = new DateTime(2026, 4, 20, 14, 30, 15);
            HistoryService.MakeSnapshotFileName(5, t).Should().Be("snap-0005-20260420-143015.json");
        }
    }
}
