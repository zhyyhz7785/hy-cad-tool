using System.Linq;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Presentation.ViewModels.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Presentation.ViewModels.Road
{
    /// <summary>M7.3 StructureLayerViewModel + 相关 Domain 圆环测试。</summary>
    public class StructureLayerViewModelTests
    {
        private static StructureLayerScheme MakeScheme(string name = "示例", int layerCount = 2)
        {
            var s = new StructureLayerScheme { Name = name };
            for (int i = 1; i <= layerCount; i++)
            {
                s.Layers.Add(new StructureLayer
                {
                    Name = $"层{i}",
                    LayerKind = i == 1 ? StructureLayerKind.Surface : StructureLayerKind.Base,
                    ThicknessCm = i * 5.0,
                });
            }
            return s;
        }

        [Fact]
        public void Ctor_EmptyByDefault()
        {
            var vm = new StructureLayerViewModel();
            vm.Schemes.Should().BeEmpty();
            vm.SelectedScheme.Should().BeNull();
            vm.SelectedLayer.Should().BeNull();
            vm.HasSelectedScheme.Should().BeFalse();
            vm.HasSelectedLayer.Should().BeFalse();
        }

        [Fact]
        public void Ctor_LoadsInitialSchemes()
        {
            var vm = new StructureLayerViewModel(new[] { MakeScheme("主干路"), MakeScheme("支路") });
            vm.Schemes.Should().HaveCount(2);
            vm.Schemes[0].Name.Should().Be("主干路");
            vm.Schemes[0].Layers.Should().HaveCount(2);
        }

        [Fact]
        public void AddScheme_AppendsAndSelects()
        {
            var vm = new StructureLayerViewModel();
            vm.AddSchemeCommand.Execute(null);
            vm.Schemes.Should().HaveCount(1);
            vm.SelectedScheme.Should().NotBeNull();
        }

        [Fact]
        public void AddLayer_AddsToSelectedScheme()
        {
            var vm = new StructureLayerViewModel();
            vm.AddSchemeCommand.Execute(null);
            int before = vm.SelectedScheme.Layers.Count;
            vm.AddLayerCommand.Execute(null);
            vm.SelectedScheme.Layers.Count.Should().Be(before + 1);
            vm.SelectedLayer.Should().NotBeNull();
        }

        [Fact]
        public void DeleteLayer_ReducesAndSelectsScheme()
        {
            var vm = new StructureLayerViewModel(new[] { MakeScheme() });
            vm.SelectedNode = vm.Schemes[0].Layers[0];
            vm.SelectedLayer.Should().NotBeNull();

            vm.DeleteLayerCommand.Execute(null);
            vm.Schemes[0].Layers.Should().HaveCount(1);
            vm.SelectedScheme.Should().NotBeNull();
            vm.SelectedLayer.Should().BeNull();
        }

        [Fact]
        public void DeleteScheme_RefusesWhenBuiltIn()
        {
            var scheme = MakeScheme();
            scheme.IsBuiltIn = true;
            var vm = new StructureLayerViewModel(new[] { scheme });
            vm.SelectedNode = vm.Schemes[0];

            vm.DeleteSchemeCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public void MoveLayerUp_RearrangesOrder()
        {
            var scheme = MakeScheme(layerCount: 3);
            var vm = new StructureLayerViewModel(new[] { scheme });
            vm.SelectedNode = vm.Schemes[0].Layers[2];

            vm.MoveLayerUpCommand.Execute(null);

            vm.Schemes[0].Layers[1].Name.Should().Be("层3");
            vm.Schemes[0].Layers[2].Name.Should().Be("层2");
        }

        [Fact]
        public void MoveLayerDown_RearrangesOrder()
        {
            var scheme = MakeScheme(layerCount: 3);
            var vm = new StructureLayerViewModel(new[] { scheme });
            vm.SelectedNode = vm.Schemes[0].Layers[0];

            vm.MoveLayerDownCommand.Execute(null);

            vm.Schemes[0].Layers[0].Name.Should().Be("层2");
            vm.Schemes[0].Layers[1].Name.Should().Be("层1");
        }

        [Fact]
        public void Confirm_InvokesConfirmedWithModels()
        {
            var vm = new StructureLayerViewModel(new[] { MakeScheme("主干") });
            System.Collections.Generic.List<StructureLayerScheme> captured = null;
            vm.Confirmed += (_, lst) => captured = lst;

            vm.ConfirmCommand.Execute(null);

            captured.Should().NotBeNull();
            captured.Should().HaveCount(1);
            captured[0].Name.Should().Be("主干");
            captured[0].Layers.Should().HaveCount(2);
            captured[0].Layers[0].LayerKind.Should().Be(StructureLayerKind.Surface);
        }

        [Fact]
        public void Cancel_InvokesCancelled()
        {
            var vm = new StructureLayerViewModel();
            bool cancelled = false;
            vm.Cancelled += (_, __) => cancelled = true;
            vm.CancelCommand.Execute(null);
            cancelled.Should().BeTrue();
        }

        [Fact]
        public void StructureLayerNode_RoundTripsAllFields()
        {
            var model = new StructureLayer
            {
                Name = "细粒式沥青",
                Description = "描述",
                LayerKind = StructureLayerKind.Surface,
                ThicknessCm = 4,
                LeftWidenCm = 5,
                RightWidenCm = 10,
                LeftSlope = 1.5,
                RightSlope = 2.0,
                FillMaterial = "沥青",
                PatternName = "ANSI31",
            };
            var node = StructureLayerNode.From(model);
            var back = node.ToModel();
            back.Name.Should().Be("细粒式沥青");
            back.LayerKind.Should().Be(StructureLayerKind.Surface);
            back.ThicknessCm.Should().Be(4);
            back.LeftWidenCm.Should().Be(5);
            back.RightSlope.Should().Be(2.0);
            back.PatternName.Should().Be("ANSI31");
        }

        [Fact]
        public void SelectedNode_SwitchToLayer_BothSchemeAndLayerReported()
        {
            var scheme = MakeScheme();
            var vm = new StructureLayerViewModel(new[] { scheme });
            vm.SelectedNode = vm.Schemes[0].Layers[0];
            vm.SelectedLayer.Should().NotBeNull();
            vm.SelectedScheme.Should().NotBeNull();
            vm.SelectedScheme.Name.Should().Be("示例");
        }
    }
}
