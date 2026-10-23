using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 가로·세로 스크롤바의 보라색 스타일과 드래그 가능한 템플릿을 검증한다.
public class ScrollBarAppearanceTests
{
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var descendant in Descendants(child))yield return descendant;}}
    [Fact] public void HistoryAndEditorScrollBarsUsePurpleAndKeepBothDirections()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            w.Model.Open(new(){Width=1600,Height=1200,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(1600,1200))});w.Show();w.UpdateLayout();
            var editor=(ScrollViewer)w.FindName("ImageScroll");var bars=Descendants(editor).OfType<ScrollBar>().ToArray();Assert.Contains(bars,x=>x.Orientation==Orientation.Horizontal);Assert.Contains(bars,x=>x.Orientation==Orientation.Vertical);
            var history=(ListBox)w.FindName("HistoryList");var historyBar=Descendants(history).OfType<ScrollBar>().First(x=>x.Orientation==Orientation.Vertical);
            foreach(var bar in bars.Append(historyBar))
            {
                bar.ApplyTemplate();var track=Assert.IsType<Track>(bar.Template.FindName("PART_Track",bar));track.Thumb.ApplyTemplate();Assert.Contains(Descendants(track.Thumb).OfType<Border>(),x=>x.Background?.ToString()=="#FF923C83");Assert.Equal(bar.Orientation==Orientation.Vertical,track.IsDirectionReversed);
                Assert.Equal(bar.Orientation==Orientation.Vertical?ScrollBar.PageDownCommand:ScrollBar.PageRightCommand,track.IncreaseRepeatButton.Command);
            }
            editor.ScrollToHorizontalOffset(100);editor.ScrollToVerticalOffset(100);w.UpdateLayout();Assert.True(editor.HorizontalOffset>0);Assert.True(editor.VerticalOffset>0);
            foreach(var bar in bars)
            {
                var before=bar.Orientation==Orientation.Vertical?editor.VerticalOffset:editor.HorizontalOffset;var arrow=(RepeatButton)bar.Template.FindName("LineIncrease",bar);((System.Windows.Input.RoutedCommand)arrow.Command).Execute(null,bar);w.UpdateLayout();Assert.True((bar.Orientation==Orientation.Vertical?editor.VerticalOffset:editor.HorizontalOffset)>before);
            }
            var folder=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(w);File.WriteAllBytes(Path.Combine(folder,"editor-purple-scrollbars.png"),PngFileWriter.Encode(bitmap));}
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
