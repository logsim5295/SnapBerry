using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.App.Settings;
using SnapBerry.App.Stickers;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 우클릭 대상의 삭제·복제와 편집 데이터 보존 및 손상 항목의 복제 차단을 검증한다.
public class HistoryContextMenuTests
{
    // 실제 팝업 템플릿의 강조 상태와 아이콘을 검사하여 기본 파란색 테마의 재등장을 막는다.
    [Fact]
    public void ContextMenuUsesPurpleHighlightAndRibbonIcons()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));EditorWindow? w=null;
        try
        {
            var store=new CaptureLibraryStore(Path.Combine(path,"Library"));
            store.SaveAsync(new(){Width=40,Height=30,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(40,30))}).GetAwaiter().GetResult();
            w=new(store,new SettingsService(path));w.Show();w.Activate();var list=(ListBox)w.FindName("HistoryList");Pump(()=>list.Items.Count==1);list.UpdateLayout();
            var container=(ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);var menu=container.ContextMenu;menu.PlacementTarget=container;menu.IsOpen=true;Pump(()=>menu.IsLoaded&&menu.IsVisible);menu.UpdateLayout();
            var chrome=Assert.IsType<Border>(menu.Template.FindName("HistoryMenuChrome",menu));Assert.Equal(6,chrome.CornerRadius.TopLeft);
            foreach(var item in menu.Items.OfType<MenuItem>())
            {
                var icon=Assert.IsType<Image>(item.Icon);Assert.NotNull(icon.Source);Assert.Equal(18,icon.Width);
                Assert.Equal((string)item.Header switch{"삭제"=>"Delete","이름 변경"=>"Edit",_=>"Copy"},icon.Tag);
                System.Windows.Input.Keyboard.Focus(item);menu.UpdateLayout();Assert.True(item.IsHighlighted);
                var itemChrome=Assert.IsType<Border>(item.Template.FindName("HistoryMenuItemChrome",item));
                Assert.Equal("#FFEBD5E6",itemChrome.Background.ToString());Assert.Equal("#FF923C83",itemChrome.BorderBrush.ToString());
            }
            var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
            if(artifacts is not null)
            {
                Directory.CreateDirectory(artifacts);var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth),(int)Math.Ceiling(menu.ActualHeight),96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(menu);
                File.WriteAllBytes(Path.Combine(artifacts,"history-context-menu.png"),PngFileWriter.Encode(image));
            }
            var disabled=menu.Items.OfType<MenuItem>().Last();disabled.IsEnabled=false;menu.UpdateLayout();
            Assert.Equal(.45,((Border)disabled.Template.FindName("HistoryMenuItemChrome",disabled)).Opacity);menu.IsOpen=false;
        }
        finally{w?.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    private static void Pump(Func<bool> done)
    {
        var frame=new DispatcherFrame();var end=DateTime.UtcNow.AddSeconds(10);var timeout=false;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};
        timer.Tick+=(_,_)=>{if(done()||(timeout=DateTime.UtcNow>end))frame.Continue=false;};
        timer.Start();Dispatcher.PushFrame(frame);timer.Stop();Assert.False(timeout);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void DuplicateTargetsClickedItemAndPreservesEditableData(bool active)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));EditorWindow? w=null;
        try
        {
            var store=new CaptureLibraryStore(Path.Combine(path,"Library"));
            var source=new CaptureRecord{Width=80,Height=60,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(80,60)),CropBounds=new(5,5,60,40),Sequence=new(){NextUppercaseAlphabetic=8},Annotations=[new(){Kind=AnnotationKind.Text,Text="original",Style=new(){FontSize=37}}]};
            var assets=new StickerAssetStore();var hash=assets.AddPng(StickerCatalog.CreatePng(StickerCatalog.Load()[0]));
            source=source with{Annotations=[..source.Annotations,new(){Kind=AnnotationKind.Sticker,AssetHash=hash,StickerId="test"}],StickerAssets=new(){{hash,assets.Data[hash]}}};
            var other=source with{Id=Guid.NewGuid(),Annotations=[]};
            store.SaveAsync(source).GetAwaiter().GetResult();store.SaveAsync(other).GetAwaiter().GetResult();
            w=new(store,new SettingsService(path));w.Show();var list=(ListBox)w.FindName("HistoryList");Pump(()=>list.Items.Count==2);
            w.Model.Open(active?source:other);
            if(active)w.Model.UpdateText(source.Annotations[0].Id,"latest");
            list.SelectedItem=list.Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==other.Id);list.UpdateLayout();
            var entry=list.Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==source.Id);
            var container=(ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(entry);
            var menu=container.ContextMenu;Assert.NotNull(menu);menu.PlacementTarget=container;menu.IsOpen=true;
            var duplicate=menu.Items.OfType<MenuItem>().Single(x=>(string)x.Header=="복제");
            duplicate.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));menu.IsOpen=false;
            Pump(()=>!w.IsBusy&&list.Items.Count==3);
            var id=w.Model.CaptureId!.Value;Assert.NotEqual(source.Id,id);Assert.NotEqual(other.Id,id);
            var copy=store.LoadAsync(id).GetAwaiter().GetResult();Assert.Equal(source.OriginalPng,copy.OriginalPng);
            Assert.Equal(source.CropBounds,copy.CropBounds);Assert.Equal(8,copy.Sequence.NextUppercaseAlphabetic);
            Assert.Equal(active?"latest":"original",copy.Annotations[0].Text);Assert.Equal(37,copy.Annotations[0].Style.FontSize);
            Assert.Equal(assets.Data[hash],copy.StickerAssets[hash]);
            Assert.Equal(id,((CaptureEntry)list.SelectedItem).Item.Id);
            w.Model.UpdateText(copy.Annotations[0].Id,"copy only");
            Assert.NotEqual("copy only",store.LoadAsync(source.Id).GetAwaiter().GetResult().Annotations[0].Text);
        }
        finally{w?.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Theory] [InlineData(true,true)] [InlineData(true,false)] [InlineData(false,true)] [InlineData(false,false)]
    public void DeleteTargetsContextItemWithConfirmation(bool confirmed,bool active)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));EditorWindow? w=null;
        try
        {
            var store=new CaptureLibraryStore(Path.Combine(path,"Library"));var source=new CaptureRecord{Width=40,Height=30,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(40,30))};var other=source with{Id=Guid.NewGuid()};
            store.SaveAsync(source).GetAwaiter().GetResult();store.SaveAsync(other).GetAwaiter().GetResult();
            var confirmations=0;w=new(store,new SettingsService(path),confirmDelete:n=>{Assert.Equal(1,n);confirmations++;return confirmed;});
            w.Show();var list=(ListBox)w.FindName("HistoryList");Pump(()=>list.Items.Count==2&&!w.IsBusy&&w.Model.CaptureId is not null);w.Model.Open(active?source:other);
            list.SelectedItem=list.Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==other.Id);list.UpdateLayout();
            var container=(ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(list.Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==source.Id));
            var menu=container.ContextMenu;Assert.NotNull(menu);menu.PlacementTarget=container;menu.IsOpen=true;
            menu.Items.OfType<MenuItem>().Single(x=>(string)x.Header=="삭제").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));menu.IsOpen=false;
            Pump(()=>!w.IsBusy&&confirmations==1&&list.Items.Count==(confirmed?1:2));
            Assert.Equal(confirmed?other.Id:(active?source.Id:other.Id),w.Model.CaptureId);
            Assert.Contains(store.ListAsync(0,50).GetAwaiter().GetResult(),x=>x.Id==other.Id);
        }
        finally{w?.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Fact]
    public void DamagedHistoryCanBeDeletedButNotDuplicated()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));EditorWindow? w=null;
        try
        {
            var library=Path.Combine(path,"Library");Directory.CreateDirectory(library);
            File.WriteAllText(Path.Combine(library,Guid.NewGuid().ToString("N")+".qcapture"),"damaged");
            w=new(new CaptureLibraryStore(library),new SettingsService(path));w.Show();var list=(ListBox)w.FindName("HistoryList");Pump(()=>list.Items.Count==1);list.UpdateLayout();
            var container=(ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);var menu=container.ContextMenu;menu.PlacementTarget=container;menu.IsOpen=true;
            Assert.True(menu.Items.OfType<MenuItem>().Single(x=>(string)x.Header=="삭제").IsEnabled);
            Assert.False(menu.Items.OfType<MenuItem>().Single(x=>(string)x.Header=="복제").IsEnabled);menu.IsOpen=false;
            Assert.False(menu.Items.OfType<MenuItem>().Single(x=>(string)x.Header=="이름 변경").IsEnabled);
        }
        finally{w?.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
