using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Library;
using SnapBerry.App.Settings;
using SnapBerry.App.Export;
using SnapBerry.App.Capture;
using SnapBerry.App.Stickers;
using SnapBerry.Core;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 실제 편집 창과 네이티브 데스크톱 캡처의 기본 동작을 검증한다.
public class WindowTests
{
    [Fact] public void EditorWindowRendersAndReopensAnnotations()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));EditorWindow? window=null;
        try
        {
            var library=new CaptureLibraryStore(Path.Combine(path,"Library"));window=new(library,new SettingsService(path));window.Show();
            var record=new CaptureRecord{Width=960,Height=560,OriginalPng=PngFileWriter.Encode(Sample())};window.Model.Open(record);
            window.Model.History!.Add(new(){Kind=AnnotationKind.Arrow,X=650,Y=200,Width=-130,Height=95,Style=new(){Color="#E83F56",Thickness=5}});
            window.Model.History.InsertStep(new(520,130),new());window.Model.History.InsertStep(new(120,360),new(){Fill="#139887"});
            window.Model.InsertSticker(StickerCatalog.Load().First(x=>x.Id=="check-circle"),new(770,65));
            window.Model.InsertSticker(StickerCatalog.Load().First(x=>x.IsKeycap),new(730,425));
            window.UpdateLayout();var frame=new DispatcherFrame();window.Dispatcher.BeginInvoke(DispatcherPriority.Background,()=>frame.Continue=false);Dispatcher.PushFrame(frame);
            var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var bytes=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(bytes,bitmap.PixelWidth*4,0);Assert.True(bytes.Distinct().Count()>100);
            var folder=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");if(!string.IsNullOrEmpty(folder)){Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,"editor-desktop.png"),PngFileWriter.Encode(bitmap));}
            window.Width=760;window.Height=560;window.UpdateLayout();Assert.True(window.ActualWidth>=720);
            if(!string.IsNullOrEmpty(folder)){var compact=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);compact.Render(window);File.WriteAllBytes(Path.Combine(folder,"editor-compact.png"),PngFileWriter.Encode(compact));}
            var snapshot=window.Model.Snapshot();library.SaveAsync(snapshot).GetAwaiter().GetResult();var reopened=library.LoadAsync(record.Id).GetAwaiter().GetResult();Assert.Equal(5,reopened.Annotations.Length);Assert.Equal(3,reopened.Sequence.NextNumeric);
        }
        finally{window?.CloseForExit();if(Directory.Exists(path))Directory.Delete(path,true);}
    });
    [Fact] public void NativeDesktopCaptureHasPositiveDimensions()=>Sta.Run(()=>{var capture=new CaptureService().CaptureDesktop();Assert.True(capture.Image.PixelWidth>0);Assert.True(capture.Image.PixelHeight>0);Assert.Equal(capture.DesktopBounds.Width,capture.Image.PixelWidth);});
    private static BitmapSource Sample()
    {
        var v=new DrawingVisual();using(var dc=v.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White,null,new Rect(0,0,960,560));dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(243,246,249)),null,new Rect(0,0,960,60));
            StickerCatalog.DrawText(dc,"PROJECT OVERVIEW",Brushes.Black,22,new Rect(40,10,300,40));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(234,242,239)),null,new Rect(35,100,890,100));StickerCatalog.DrawText(dc,"Release checklist",Brushes.Teal,30,new Rect(50,110,320,75));
            string[] lines=["Capture workflow","Annotation tools","History and export"];
            for(var i=0;i<3;i++){dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(236,239,243)),null,new Rect(45,245+i*85,860,1));StickerCatalog.DrawText(dc,lines[i],Brushes.DarkSlateGray,23,new Rect(55,253+i*85,320,65));dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(227,245,239)),null,new Rect(725,265+i*85,135,35),4,4);StickerCatalog.DrawText(dc,"Ready",Brushes.Teal,18,new Rect(725,265+i*85,135,35));}
        }
        var b=new RenderTargetBitmap(960,560,96,96,PixelFormats.Pbgra32);b.Render(v);b.Freeze();return b;
    }
}
