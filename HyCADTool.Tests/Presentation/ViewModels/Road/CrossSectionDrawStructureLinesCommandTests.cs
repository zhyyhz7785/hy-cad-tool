using System;
using FluentAssertions;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Presentation.ViewModels.Road;
using Xunit;

namespace HyCADTool.Tests.Presentation.ViewModels.Road
{
    /// <summary>
    /// v2.1「向 CAD 绘制结构线」按钮 ViewModel 行为测试。
    ///
    /// <para>核心契约：</para>
    /// <list type="number">
    ///   <item><see cref="CrossSectionDesignerViewModel.DrawStructureLinesCommand"/> 总是可执行；无订阅者时执行不抛异常。</item>
    ///   <item>订阅者收到的 <see cref="CrossSectionDesignerResult"/> 具备完整 Template/Figure/Layout。</item>
    ///   <item>同一窗口会话的多次点击共享同一 Template.Id（否则 Clear 无法清掉上次绘制的实体，会导致重复堆叠）。</item>
    ///   <item>「绘制结构线」产生的 Template.Id 与 ConfirmCommand（"确定并出图"）产生的 Id 必须一致，
    ///       保证最终出图能干净接替临时结构线。</item>
    /// </list>
    ///
    /// <para>这里直接使用父类 <see cref="CrossSectionDesignerViewModel"/>，因为子类
    /// <see cref="CrossSectionDrawViewModel"/> 构造路径会 touch <c>AcApp.DocumentManager</c>，
    /// 单元测试 stub 下会抛 <see cref="InvalidProgramException"/>。命令/事件已上提到父类，测试意图完全等价。</para>
    /// </summary>
    public class CrossSectionDrawStructureLinesCommandTests
    {
        private static CrossSectionDesignerViewModel NewVm()
            => new CrossSectionDesignerViewModel(CrossSectionPresets.CreateCjj37UrbanArterial());

        [Fact]
        public void Command_Exists_AndAlwaysExecutable()
        {
            var vm = NewVm();

            vm.DrawStructureLinesCommand.Should().NotBeNull();
            vm.DrawStructureLinesCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void Command_WithoutSubscriber_DoesNotThrow()
        {
            var vm = NewVm();

            Action act = () => vm.DrawStructureLinesCommand.Execute(null);

            act.Should().NotThrow();
        }

        [Fact]
        public void Command_DeliversFullResult_ToSubscriber()
        {
            var vm = NewVm();
            CrossSectionDesignerResult received = null;
            vm.DrawStructureLinesRequested += (_, r) => received = r;

            vm.DrawStructureLinesCommand.Execute(null);

            received.Should().NotBeNull();
            received.Template.Should().NotBeNull();
            received.Template.Id.Should().NotBe(Guid.Empty);
            received.Figure.Should().NotBeNull();
            received.Layout.Should().NotBeNull();
        }

        [Fact]
        public void Command_ExecutedTwice_ReusesSameTemplateId()
        {
            var vm = NewVm();
            Guid? first = null;
            Guid? second = null;
            vm.DrawStructureLinesRequested += (_, r) =>
            {
                if (first == null) first = r.Template.Id;
                else second = r.Template.Id;
            };

            vm.DrawStructureLinesCommand.Execute(null);
            vm.DrawStructureLinesCommand.Execute(null);

            first.Should().NotBeNull();
            second.Should().NotBeNull();
            second.Should().Be(first, "同一窗口会话的多次绘制必须复用同一 Template.Id，否则 Clear 无法清掉上次实体。");
        }

        [Fact]
        public void Draw_ThenConfirm_ShareSameTemplateId()
        {
            var vm = NewVm();

            Guid? drawId = null;
            Guid? confirmId = null;
            vm.DrawStructureLinesRequested += (_, r) => drawId = r.Template.Id;
            vm.Confirmed += (_, r) => confirmId = r.Template.Id;
            vm.NonCompliantConfirm = _ => true;

            vm.DrawStructureLinesCommand.Execute(null);
            vm.ConfirmCommand.Execute(null);

            drawId.Should().NotBeNull();
            confirmId.Should().NotBeNull();
            confirmId.Should().Be(drawId, "先绘制再确定出图必须沿用同一 Id，让最终出图 Clear 掉临时结构线。");
        }

        [Fact]
        public void WhenExistingTemplateIdProvided_DrawUsesProvidedId()
        {
            var id = Guid.NewGuid();
            var vm = new CrossSectionDesignerViewModel(
                CrossSectionPresets.CreateCjj37UrbanArterial(),
                existingTemplateId: id);

            Guid? drawId = null;
            vm.DrawStructureLinesRequested += (_, r) => drawId = r.Template.Id;

            vm.DrawStructureLinesCommand.Execute(null);

            drawId.Should().Be(id);
        }
    }
}
