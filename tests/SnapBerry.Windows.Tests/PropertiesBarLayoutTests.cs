using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 창 표시 없이 리본 통합 속성의 공간 회수와 도구 전환 시 편집 영역 위치를 검증한다.
public class PropertiesBarLayoutTests
{
    [Theory] [InlineData(1240)] [InlineData(760)] [InlineData(720)]
    public void IntegratedPropertiesReclaimTheBarSpaceAndKeepWorkspacePosition(int width)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-bar-"+Guid.NewGuid().ToString("N"));
        var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            window.Model.Open(new(){Width=80,Height=60,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(80,60))});
            var properties=(FrameworkElement)window.FindName("PropertiesPanel");
            var ribbon=(Ribbon)window.FindName("MainRibbon");
            var root=(FrameworkElement)window.Content;var workspace=(FrameworkElement)window.FindName("WorkspaceGrid");
            // 창 핸들을 만들지 않고 시각 트리를 독립적으로 배치해 실제 리본 내용도 렌더링한다.
            window.Content=null;
            root.Resources=window.Resources;
            root.SetValue(TextElement.FontFamilyProperty,window.FontFamily);root.SetValue(TextElement.FontSizeProperty,window.FontSize);
            ribbon.SelectedIndex=0;
            (double Height,double Top) Layout(EditorTool tool,Annotation? selected=null)
            {
                window.Model.Tool=tool;window.Model.SelectedId=selected?.Id;
                if(selected is not null)window.Model.History!.Add(selected);
                typeof(EditorWindow).GetMethod("UpdateProperties",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
                root.Measure(new Size(width,820));root.Arrange(new Rect(0,0,width,820));root.UpdateLayout();
                var frame=new DispatcherFrame();window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,()=>frame.Continue=false);Dispatcher.PushFrame(frame);root.UpdateLayout();
                Assert.False(window.IsVisible);
                var top=workspace.TranslatePoint(new Point(),root).Y;
                Assert.Equal(ribbon.TranslatePoint(new Point(0,ribbon.ActualHeight),root).Y,top,3);
                var group=(RibbonGroup)window.FindName("PropertiesGroup");
                if(group.Visibility==Visibility.Visible&&!group.IsCollapsed)
                {
                    var right=((Panel)properties).Children.OfType<FrameworkElement>().Where(child=>child.Visibility==Visibility.Visible)
                        .Max(child=>child.TranslatePoint(new Point(child.ActualWidth,0),properties).X+child.Margin.Right);
                    Assert.InRange(properties.ActualWidth-right,-0.01,1);
                    Assert.True(properties.ActualHeight<=group.ActualHeight,"속성의 너비를 줄여도 컨트롤이 세로로 잘리면 안 됩니다.");
                }
                return(ribbon.ActualHeight,top);
            }
            var expected=Layout(EditorTool.Arrow);Assert.True(expected.Height>=32);
            foreach(var tool in Enum.GetValues<EditorTool>())
            {
                var actual=Layout(tool);Assert.Equal(expected,actual);
                var group=Assert.IsAssignableFrom<RibbonGroup>(window.FindName("PropertiesGroup"));
                Assert.Equal(tool is EditorTool.Select or EditorTool.Crop?Visibility.Collapsed:Visibility.Visible,group.Visibility);
                if(group.Visibility==Visibility.Visible)Assert.True(group.ActualWidth>0,"리본 속성이 실제로 배치되어야 합니다.");
                if(width<=760&&group.Visibility==Visibility.Visible)Assert.True(group.IsCollapsed,"좁은 창에서는 속성 그룹을 접어 접근할 수 있어야 합니다.");
                if(tool is EditorTool.Select or EditorTool.Crop)
                    Assert.All(properties is Panel panel?panel.Children.Cast<FrameworkElement>():[],child=>Assert.Equal(Visibility.Collapsed,child.Visibility));
            }
            foreach(var kind in new[]{AnnotationKind.Arrow,AnnotationKind.Text,AnnotationKind.Rectangle,AnnotationKind.Mosaic,AnnotationKind.StepMarker})
                Assert.Equal(expected,Layout(EditorTool.Select,new(){Kind=kind,Text="sample"}));
            Assert.Equal(expected,Layout(EditorTool.Select));
            var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
            if(artifacts is not null)
            {
                Directory.CreateDirectory(artifacts);
                foreach(var tool in new[]{EditorTool.Select,EditorTool.Arrow,EditorTool.Mosaic,EditorTool.Number,EditorTool.Crop})
                {
                    Layout(tool);var visual=new DrawingVisual();
                    using(var dc=visual.RenderOpen())dc.DrawRectangle(new VisualBrush(ribbon),null,new Rect(0,0,ribbon.ActualWidth,ribbon.ActualHeight));
                    var bitmap=new RenderTargetBitmap(width,(int)Math.Ceiling(ribbon.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
                    File.WriteAllBytes(Path.Combine(artifacts,$"properties-{width}-{tool}.png"),PngFileWriter.Encode(bitmap));
                }
            }
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
