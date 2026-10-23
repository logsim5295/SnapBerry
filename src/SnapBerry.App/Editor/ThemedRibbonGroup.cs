using System.Windows.Controls.Ribbon;

namespace SnapBerry.App.Editor;

// 그룹은 탭 선택·좁은 창의 접힘에 따라 템플릿을 새로 만든다. 새 부품에도 현재 테마를 적용한다.
public sealed class ThemedRibbonGroup : RibbonGroup
{
    public override void OnApplyTemplate(){base.OnApplyTemplate();RibbonThemeChrome.ApplyTemplateColors(this);}
}
