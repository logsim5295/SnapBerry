using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SnapBerry.App.Settings;
namespace SnapBerry.App.Editor;
// 동일한 32픽셀 도화지와 브랜드 색상으로 리본 아이콘을 그려 크기·톤을 일관되게 유지한다.
public static class RibbonIcons
{
    private static readonly Brush Ink=ColorBrush("#263B55"),Berry=ColorBrush("#923C83"),Yellow=ColorBrush("#F3C83D"),Paper=ColorBrush("#F7FAFC"),Rose=ColorBrush("#D66683");
    private static Brush ColorBrush(string value){var brush=(Brush)new BrushConverter().ConvertFromString(value)!;brush.Freeze();return brush;}
    public static ImageSource Create(string name)=>Create(name,ThemeResources.Current);
    public static ImageSource Create(string name,AppTheme theme)
    {
        var Ink=theme==AppTheme.Dark?new SolidColorBrush(ThemeResources.ColorFor(theme,"Text")):RibbonIcons.Ink;
        var Berry=theme==AppTheme.Dark?new SolidColorBrush(ThemeResources.ColorFor(theme,"Accent")):RibbonIcons.Berry;
        var Paper=theme==AppTheme.Dark?ColorBrush("#353B49"):RibbonIcons.Paper;
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,32,32));
            Pen Pen(Brush color,double width=2)=>new(color,width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
            void Line(double x,double y,double x2,double y2,Brush? color=null,double width=2)=>dc.DrawLine(Pen(color??Ink,width),new(x,y),new(x2,y2));
            void Shape(string data,Brush? fill=null,Brush? stroke=null,double width=2)=>dc.DrawGeometry(fill,Pen(stroke??Ink,width),Geometry.Parse(data));
            void Box(double x,double y,double w,double h,Brush fill,double radius=2)=>dc.DrawRoundedRectangle(fill,Pen(Ink),new Rect(x,y,w,h),radius,radius);
            void Circle(double x,double y,double radius,Brush? fill,Brush? stroke=null,double width=2)=>dc.DrawEllipse(fill,stroke is null?null:Pen(stroke,width),new(x,y),radius,radius);
            void Text(string text,double size,Rect bounds,Brush color)
            {var t=new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.Bold,FontStretches.Normal),size,color,1);dc.DrawText(t,new Point(bounds.X+(bounds.Width-t.Width)/2,bounds.Y+(bounds.Height-t.Height)/2));}
            switch(name)
            {
                case "NewBlank":
                    Shape("M6,2 L20,2 L27,9 L27,30 L6,30 Z",Paper);Shape("M20,2 L20,9 L27,9",Paper);
                    Line(11,20,23,20,Berry,3);Line(17,14,17,26,Berry,3);break;
                case "ImageFile":
                    Box(3,6,26,23,Yellow,2);dc.DrawRectangle(Paper,Pen(Ink),new Rect(8,3,20,20));
                    Circle(22,8,2,Berry);Shape("M9,21 L15,12 L20,17 L24,12 L27,21 Z",Berry);break;
                case "Paste":
                    Box(5,5,22,25,Paper,2);Box(11,2,10,6,Yellow,1);
                    dc.DrawRectangle(Berry,null,new Rect(9,12,14,13));Circle(19,15,1.5,Yellow);Shape("M10,24 L15,17 L19,22 L22,18 L23,24 Z",Yellow);break;
                case "Open":
                    Box(4,4,24,24,Paper,3);Line(4,10,28,10);Shape("M11,24 L11,18 L6,18 L16,8 L26,18 L21,18 L21,24 Z",Berry);break;
                case "Exit":
                    Shape("M9,7 A11,11 0 1 0 23,7",null,Berry,3);Line(16,3,16,16,Berry,3);break;
                case "Region":
                    foreach(var (x,y,sx,sy) in new[]{(5d,5d,1d,1d),(27d,5d,-1d,1d),(5d,27d,1d,-1d),(27d,27d,-1d,-1d)})
                    {Line(x,y,x+6*sx,y,Berry,3);Line(x,y,x,y+6*sy,Berry,3);}
                    Circle(16,16,5,null,Yellow,3);Circle(16,16,1.5,Yellow);break;
                case "Window":
                    Box(3,5,26,23,Paper,3);dc.DrawRoundedRectangle(Berry,null,new Rect(4,6,24,6),2,2);Line(4,13,28,13);
                    foreach(var x in new[]{7d,11d,15d})Circle(x,9,1,Paper);Circle(16,21,4,null,Yellow,3);break;
                case "Desktop":
                    Box(3,4,26,20,Ink);dc.DrawRectangle(Yellow,null,new Rect(6,7,20,14));dc.DrawRectangle(Ink,null,new Rect(8,9,16,10));Line(16,24,16,28,Ink,3);Line(10,29,22,29,Ink,3);break;
                case "Select":Shape("M6,3 L26,19 L18,21 L22,28 L17,30 L13,23 L6,28 Z",Berry);break;
                case "Arrow":Shape("M5,23 L20,8 L14,8 L14,3 L29,3 L29,18 L24,18 L24,12 L9,27 Z",Berry);break;
                case "Text":Shape("M4,5 L28,5 L28,11 L23,11 L23,9 L19,9 L19,26 L23,26 L23,30 L9,30 L9,26 L13,26 L13,9 L9,9 L9,11 L4,11 Z",Berry);break;
                case "Rectangle":
                    dc.DrawRectangle(null,Pen(Ink),new Rect(6,6,20,20));foreach(var (x,y) in new[]{(3d,3d),(23d,3d),(3d,23d),(23d,23d)})Box(x,y,6,6,Yellow,1);break;
                case "MagicWand":
                    Box(3,3,18,18,Paper,2);
                    var checker=ColorBrush(theme==AppTheme.Dark?"#596274":"#C1C8D2");
                    for(var row=0;row<3;row++)for(var column=0;column<3;column++)
                        if((row+column)%2==0)dc.DrawRectangle(checker,null,new Rect(4.5+column*5,4.5+row*5,5,5));
                    Shape("M11,21 L22,10 Q23,9 24,10 L30,16 Q31,17 30,18 L20,28 H16 L11,23 Q10,22 11,21 Z",Berry);
                    Shape("M11,21 L17,15 L25,23 L20,28 H16 L11,23 Q10,22 11,21 Z",Yellow);
                    Line(24,29,30,29);break;
                case "Crop":
                    dc.DrawRectangle(Yellow,Pen(Ink),new Rect(12,12,8,8));
                    Line(9,3,9,23,Berry,4);Line(9,23,29,23,Berry,4);
                    Line(3,9,23,9,Ink,3);Line(23,9,23,29,Ink,3);break;
                case "Mosaic":
                    Brush[] colors=[Berry,Yellow,Ink,Ink,Berry,Berry,Berry,Ink,Yellow];for(var i=0;i<9;i++)dc.DrawRoundedRectangle(colors[i],null,new Rect(3+i%3*9,3+i/3*9,7,7),1,1);break;
                case "Number":
                    Circle(14,15,12,Berry,Ink);Text("1",19,new Rect(4,3,20,24),RibbonIcons.Paper);Circle(25,25,5,Yellow,Ink);break;
                case "Sticker":
                    Shape("M6,3 L26,3 Q29,3 29,6 L29,22 L22,29 L6,29 Q3,29 3,26 L3,6 Q3,3 6,3 Z",Yellow);
                    Shape("M22,29 L22,24 Q22,22 24,22 L29,22 Z",Paper);Circle(11,12,1.3,RibbonIcons.Ink);Circle(21,12,1.3,RibbonIcons.Ink);Shape("M10,18 Q16,24 22,18",stroke:RibbonIcons.Ink);break;
                case "Undo":case "Redo":
                    if(name=="Redo")dc.PushTransform(new MatrixTransform(-1,0,0,1,32,0));
                    Shape("M13,4 L4,13 L13,21 L13,16 C23,15 27,21 23,28 C31,23 30,9 13,10 Z",Berry);
                    if(name=="Redo")dc.Pop();break;
                case "Delete":
                    Box(6,9,20,20,Rose,2);Box(12,3,8,5,Rose,1);Box(4,7,24,4,Rose,1);Line(12,15,12,24);Line(20,15,20,24);break;
                case "Edit":
                    Line(3,11,12,11);Line(3,18,9,18);Line(3,25,10,25);
                    Shape("M12,22 L24,5 Q26,2 29,5 L30,6 Q31,7 29,10 L17,27 L10,29 Z",Berry);
                    Shape("M10,29 L12,22 L17,27 Z",Yellow);Line(23,8,28,12,Yellow,3);break;
                case "Copy":
                    Shape("M12,3 L24,3 L29,8 L29,23 L12,23 Z",Yellow);Shape("M4,10 L17,10 L23,16 L23,29 L4,29 Z",Paper);Line(9,19,17,19);Line(9,24,17,24);break;
                case "Save":
                    Shape("M4,3 L20,3 L26,9 L26,27 L4,27 Z",Paper);Text("IMG",7,new Rect(5,11,19,11),Ink);
                    Shape("M23,18 L28,18 L28,24 L31,24 L25.5,30 L20,24 L23,24 Z",Yellow);break;
                case "ZoomOut":case "ZoomIn":
                    Line(23,23,29,29,Ink,5);Circle(14,14,11,Berry,Ink);Circle(14,14,7.5,Paper);Line(10,14,18,14,Yellow,3);if(name=="ZoomIn")Line(14,10,14,18,Yellow,3);break;
                case "Fit":
                    Box(3,4,26,24,Paper,2);
                    foreach(var (x,y,sx,sy) in new[]{(7d,8d,1d,1d),(25d,8d,-1d,1d),(7d,24d,1d,-1d),(25d,24d,-1d,-1d)})
                    {Line(x,y,x+5*sx,y+5*sy,Yellow,2.5);Line(x+5*sx,y+5*sy,x+2*sx,y+5*sy,Yellow,2.5);Line(x+5*sx,y+5*sy,x+5*sx,y+2*sy,Yellow,2.5);}break;
                case "History":
                    foreach(var y in new[]{6d,14d,22d}){Circle(4,y,1.5,Ink);Line(10,y,y==6?26:18,y,Ink,2.5);}Circle(24,23,7,Yellow,Ink);Line(24,19,24,23,RibbonIcons.Ink);Line(24,23,27,23,RibbonIcons.Ink);break;
                case "About":
                    Circle(16,16,13,Berry,Ink);Circle(16,8,2,Yellow);Line(16,14,16,24,Yellow,3);Line(13,24,19,24,Yellow,3);break;
                case "Properties":
                    Line(4,8,28,8);Line(4,16,28,16);Line(4,24,28,24);
                    Circle(11,8,3,Paper,Berry);Circle(22,16,3,Paper,Berry);Circle(15,24,3,Paper,Berry);break;
                case "Settings":
                    Shape("M12,3 L20,3 L21,7 L24,9 L28,8 L31,14 L28,17 L28,20 L30,24 L25,29 L21,27 L18,28 L16,31 L10,29 L10,25 L7,23 L3,23 L1,16 L5,14 L6,11 L5,7 L10,4 Z",Berry);
                    Circle(16,17,7,Yellow,Ink);Circle(16,17,3,Paper,Ink);break;
                default:throw new ArgumentOutOfRangeException(nameof(name),name,"Unknown ribbon icon.");
            }
        }
        var image=new DrawingImage(visual.Drawing);image.Freeze();return image;
    }
}
