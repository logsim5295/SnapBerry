using System.Drawing;
using Forms=System.Windows.Forms;
using SnapBerry.App.Editor;
using SnapBerry.App.Settings;

namespace SnapBerry.App.Shell;

// WinForms의 선택·키보드·비활성 처리는 유지하고 WPF 이력 메뉴와 같은 브랜드 색상만 적용한다.
internal sealed class TrayMenuRenderer : Forms.ToolStripProfessionalRenderer
{
    private readonly AppTheme theme;
    internal TrayMenuRenderer(AppTheme theme=AppTheme.Light):base(new TrayMenuColors(theme)){this.theme=theme;RoundedEdges=true;}
    private static Color ThemeColor(AppTheme theme,string key)
    {var color=ThemeResources.ColorFor(theme,key);return Color.FromArgb(color.A,color.R,color.G,color.B);}

    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        if(e.Item is not Forms.ToolStripMenuItem){base.OnRenderMenuItemBackground(e);return;}
        if(!e.Item.Selected||!e.Item.Enabled||e.Item.Width<12||e.Item.Height<12)return;
        // 기본 렌더러의 OS 테마 분기를 우회하여 마우스·키보드 선택 모두 동일한 색으로 그린다.
        // 자동 레이아웃에서는 항목 너비가 메뉴의 실제 클라이언트 너비보다 클 수 있다.
        // 부모 메뉴 밖으로 잘리는 부분을 제외해 오른쪽 곡선도 보이는 영역 안에 그린다.
        var bounds=new Rectangle(Point.Empty,e.Item.Size);
        if(e.Item.GetCurrentParent() is {} parent)
        {
            var client=parent.ClientRectangle;client.Offset(-e.Item.Bounds.X,-e.Item.Bounds.Y);
            bounds=Rectangle.Intersect(bounds,client);
        }
        if(bounds.Width<12||bounds.Height<12)return;
        var scale=e.Graphics.DpiX/96f;var diameter=Math.Min(8*scale,bounds.Height-2);
        var rect=new RectangleF(bounds.Left+2,bounds.Top,bounds.Width-5,bounds.Height-2);
        using var path=new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(rect.Left,rect.Top,diameter,diameter,180,90);
        path.AddArc(rect.Right-diameter,rect.Top,diameter,diameter,270,90);
        path.AddArc(rect.Right-diameter,rect.Bottom-diameter,diameter,diameter,0,90);
        path.AddArc(rect.Left,rect.Bottom-diameter,diameter,diameter,90,90);path.CloseFigure();
        var state=e.Graphics.Save();
        try
        {
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var fill=new SolidBrush(ColorTable.MenuItemSelected);using var border=new Pen(ColorTable.MenuItemBorder);
            e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(border,path);
        }
        finally{e.Graphics.Restore(state);}
    }

    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor=ThemeColor(theme,!e.Item.Enabled?"MutedText":e.Item.Selected?"AccentText":"Text");
        if(e.Item is Forms.ToolStripMenuItem)
        {
            // 기본 텍스트 영역 대신 강조 배경의 전체 높이에 맞춰 세로 중앙에 정렬한다.
            var text=e.TextRectangle;text.Y=0;text.Height=e.Item.Height-2;e.TextRectangle=text;
            e.TextFormat=(e.TextFormat&~(Forms.TextFormatFlags.Bottom|Forms.TextFormatFlags.WordBreak))|Forms.TextFormatFlags.VerticalCenter|Forms.TextFormatFlags.SingleLine;
        }
        base.OnRenderItemText(e);
    }

    private sealed class TrayMenuColors(AppTheme theme) : Forms.ProfessionalColorTable
    {
        private Color Selection=>ThemeColor(theme,"Selected");private Color Berry=>ThemeColor(theme,"Accent");
        private Color Surface=>theme==AppTheme.Light?Color.White:ThemeColor(theme,"Surface");
        public override Color MenuItemSelected=>Selection;
        public override Color MenuItemBorder=>Berry;
        // 그라데이션의 양 끝도 같은 색으로 지정하여 OS 테마의 파란색이 섞이지 않게 한다.
        public override Color MenuItemSelectedGradientBegin=>Selection;
        public override Color MenuItemSelectedGradientEnd=>Selection;
        public override Color MenuItemPressedGradientBegin=>Selection;
        public override Color MenuItemPressedGradientMiddle=>Selection;
        public override Color MenuItemPressedGradientEnd=>Selection;
        public override Color ToolStripDropDownBackground=>Surface;
        public override Color ImageMarginGradientBegin=>Surface;
        public override Color ImageMarginGradientMiddle=>Surface;
        public override Color ImageMarginGradientEnd=>Surface;
        public override Color MenuBorder=>Selection;
        public override Color SeparatorDark=>Selection;
        public override Color SeparatorLight=>Surface;
    }
}
