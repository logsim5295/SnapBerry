using System.Drawing;
using System.IO;
using SnapBerry.App.Shell;
using SnapBerry.App.Settings;
using Xunit;
using Forms=System.Windows.Forms;

namespace SnapBerry.Windows.Tests;

// WinForms 트레이 메뉴의 실제 렌더링 색과 기존 선택·비활성 동작을 검증한다.
public class TrayMenuAppearanceTests
{
    [Theory]
    [InlineData(0,AppTheme.Light)] [InlineData(2,AppTheme.Light)] [InlineData(5,AppTheme.Light)]
    [InlineData(0,AppTheme.Dark)] [InlineData(2,AppTheme.Dark)] [InlineData(5,AppTheme.Dark)]
    public void MenuIconsAndVisibleTextAreSizedAndCenteredForTheLargerFont(int selectedIndex,AppTheme theme)=>Sta.Run(()=>
    {
        using var menu=new TrayContextMenu{Renderer=new TrayMenuRenderer(theme)};
        foreach(var title in new[]{"편집기 열기","영역 캡처","창 캡처","전체 화면 캡처","설정","종료"})menu.Items.Add(title);
        TrayMenuIcons.Apply(menu);menu.PerformLayout();var item=menu.Items[selectedIndex];item.Select();
        Rectangle textRectangle=default,imageRectangle=default;Color textColor=default;
        menu.Renderer.RenderItemText+=(_,e)=>{if(e.Item==item){textRectangle=e.TextRectangle;textColor=e.TextColor;}};
        menu.Renderer.RenderItemImage+=(_,e)=>{if(e.Item==item)imageRectangle=e.ImageRectangle;};
        using var bitmap=new Bitmap(menu.Width,menu.Height);menu.DrawToBitmap(bitmap,menu.ClientRectangle);
        var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
        if(artifacts is not null){Directory.CreateDirectory(artifacts);bitmap.Save(Path.Combine(artifacts,$"tray-menu-alignment-{theme}-{selectedIndex}.png"));}
        textRectangle.Offset(item.Bounds.Location);
        var inkRows=Enumerable.Range(textRectangle.Top,textRectangle.Height).Where(y=>Enumerable.Range(textRectangle.Left,Math.Min(textRectangle.Width,bitmap.Width-textRectangle.Left)).Any(x=>bitmap.GetPixel(x,y).ToArgb()==textColor.ToArgb())).ToArray();
        Assert.NotEmpty(inkRows);
        var centerDifference=Math.Abs((inkRows[0]+inkRows[^1])/2d-(item.Bounds.Top+(item.Height-2)/2d));
        Assert.True(centerDifference<=2,$"글자가 항목 중앙에서 {centerDifference}px 벗어났습니다: ink={inkRows[0]}..{inkRows[^1]}, text={textRectangle}, item={item.Bounds}, image={imageRectangle}, dpi={menu.DeviceDpi}");
        Assert.True(imageRectangle.Height>=item.Font.Height,$"아이콘({imageRectangle.Size})이 글꼴 높이({item.Font.Height}px)에 비해 작습니다.");
        Assert.True(imageRectangle.Right+item.Bounds.Left<textRectangle.Left,"확대한 아이콘과 글자가 겹칩니다.");
        Assert.False(menu.Visible);
    });

    [Theory]
    [InlineData(0,AppTheme.Light)] [InlineData(2,AppTheme.Light)] [InlineData(5,AppTheme.Light)]
    [InlineData(0,AppTheme.Dark)] [InlineData(2,AppTheme.Dark)] [InlineData(5,AppTheme.Dark)]
    public void HoverHighlightKeepsAllFourCornersRoundedInTheActualMenu(int selectedIndex,AppTheme theme)=>Sta.Run(()=>
    {
        using var menu=new TrayContextMenu{Renderer=new TrayMenuRenderer(theme)};
        foreach(var title in new[]{"편집기 열기","영역 캡처","창 캡처","전체 화면 캡처","설정","종료"})menu.Items.Add(title);
        TrayMenuIcons.Apply(menu);menu.PerformLayout();menu.Items[selectedIndex].Select();
        using var bitmap=new Bitmap(menu.Width,menu.Height);menu.DrawToBitmap(bitmap,menu.ClientRectangle);
        var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
        if(artifacts is not null){Directory.CreateDirectory(artifacts);bitmap.Save(Path.Combine(artifacts,$"tray-menu-corners-{theme}-{selectedIndex}.png"));}
        var fill=((TrayMenuRenderer)menu.Renderer).ColorTable.MenuItemSelected.ToArgb();
        var bounds=Rectangle.Intersect(menu.Items[selectedIndex].Bounds,new Rectangle(1,1,bitmap.Width-2,bitmap.Height-2));
        var rows=Enumerable.Range(bounds.Top,bounds.Height).Select(y=>new{Y=y,X=Enumerable.Range(bounds.Left,bounds.Width).Where(x=>bitmap.GetPixel(x,y).ToArgb()==fill).ToArray()}).Where(row=>row.X.Length>0).ToArray();
        Assert.NotEmpty(rows);
        var middle=rows[rows.Length/2];
        foreach(var edge in new[]{rows[0],rows[^1]})
        {
            Assert.True(edge.X[0]-middle.X[0]>=2,$"왼쪽 모서리가 둥글지 않습니다: y={edge.Y}, left={edge.X[0]}, middle={middle.X[0]}");
            Assert.True(middle.X[^1]-edge.X[^1]>=2,$"오른쪽 모서리가 둥글지 않습니다: y={edge.Y}, right={edge.X[^1]}, middle={middle.X[^1]}, item={menu.Items[selectedIndex].Bounds}, menu={menu.ClientRectangle}");
        }
        Assert.False(menu.Visible);
    });

    [Theory] [InlineData(true,true)] [InlineData(false,true)] [InlineData(true,false)]
    public void SelectedMenuUsesSamePurpleBackgroundAndBorderAsHistory(bool select,bool enabled)=>Sta.Run(()=>
    {
        using var menu=new Forms.ContextMenuStrip{Renderer=new TrayMenuRenderer(),AutoSize=false,Size=new Size(180,100)};
        var item=new Forms.ToolStripMenuItem("캡처 이력"){AutoSize=false,Size=new Size(170,30),Enabled=enabled};menu.Items.Add(item);
        if(select)item.Select();Assert.Equal(select,item.Selected);
        using var bitmap=new Bitmap(180,40);using var graphics=Graphics.FromImage(bitmap);graphics.Clear(Color.White);
        menu.Renderer.DrawMenuItemBackground(new Forms.ToolStripItemRenderEventArgs(graphics,item));
        var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
        if(artifacts is not null&&select&&enabled){Directory.CreateDirectory(artifacts);bitmap.Save(Path.Combine(artifacts,"tray-menu-highlight.png"),System.Drawing.Imaging.ImageFormat.Png);}
        var colors=Enumerable.Range(0,bitmap.Width).SelectMany(x=>Enumerable.Range(0,bitmap.Height).Select(y=>bitmap.GetPixel(x,y).ToArgb())).ToArray();
        Assert.Equal(select&&enabled,colors.Contains(ColorTranslator.FromHtml("#EBD5E6").ToArgb()));
        Assert.Equal(select&&enabled,colors.Contains(ColorTranslator.FromHtml("#923C83").ToArgb()));
        var clicks=0;item.Click+=(_,_)=>clicks++;item.PerformClick();Assert.Equal(enabled?1:0,clicks);
    });

    [Fact] public void NativeMenuPaletteHasNoBlueOrGradientSelection()=>Sta.Run(()=>
    {
        var renderer=new TrayMenuRenderer();var colors=renderer.ColorTable;
        Assert.Equal(ColorTranslator.FromHtml("#EBD5E6"),colors.MenuItemSelected);
        Assert.Equal(colors.MenuItemSelected,colors.MenuItemSelectedGradientBegin);Assert.Equal(colors.MenuItemSelected,colors.MenuItemSelectedGradientEnd);
        Assert.Equal(ColorTranslator.FromHtml("#923C83"),colors.MenuItemBorder);
        Assert.Equal(Color.White,colors.ToolStripDropDownBackground);Assert.Equal(Color.White,colors.ImageMarginGradientBegin);
    });
}
