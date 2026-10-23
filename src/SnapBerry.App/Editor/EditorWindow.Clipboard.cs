using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Import;
using SnapBerry.App.Rendering;
using SnapBerry.Core;

namespace SnapBerry.App.Editor;

public partial class EditorWindow
{
    private bool IsHistoryCopySource(DependencyObject? source)
    {
        for(var node=source;node is not null;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))
            if(node==HistoryList)return true;
        return false;
    }
    private void CanCopyImageCommand(object sender,CanExecuteRoutedEventArgs e)
    {
        e.CanExecute=!busy&&!IsTextInput(e.OriginalSource as DependencyObject)&&(IsHistoryCopySource(e.OriginalSource as DependencyObject)
            ?HistoryList.SelectedItems.Count==1&&HistoryList.SelectedItem is CaptureEntry{Item.Damaged:false}
            :Model.Document is not null);e.Handled=true;
    }
    private async void CopyImageCommandExecuted(object sender,ExecutedRoutedEventArgs e)
    {
        e.Handled=true;var fromHistory=IsHistoryCopySource(e.OriginalSource as DependencyObject);
        await SafeAsync(()=>fromHistory?CopySelectedHistoryAsync():CopySelectedImageOrDocumentAsync());
    }
    private async Task CopySelectedImageOrDocumentAsync()
    {
        if(busy)return;
        if(Model.Selected is {Kind:AnnotationKind.Rectangle or AnnotationKind.Arrow or AnnotationKind.Sticker} annotation
            &&annotation.StickerId!="pasted-image")
        {
            SetBusy(true);
            try
            {
                canvas.CancelGesture();
                var image=annotation.Kind==AnnotationKind.Sticker?Model.Assets.GetBitmap(annotation.AssetHash):RenderClipboardAnnotation(annotation);
                await WriteClipboardImageAsync(image,annotation:annotation);StatusText.Text="선택한 개체 복사됨";
            }
            finally{SetBusy(false);}
            return;
        }
        if(Model.Document is not {} document||Model.Selected is not {Kind:AnnotationKind.Sticker,StickerId:"pasted-image"} selected)
        {await CopyAsync();return;}
        SetBusy(true);
        try
        {
            canvas.CancelGesture();var output=document.OutputSize;
            // 원본 픽셀과 표시 크기를 함께 보관해 반복 복제로 화질·소수 크기가 달라지지 않게 한다.
            var size=new ClipboardImageSize(selected.Width*output.Width/document.Width,selected.Height*output.Height/document.Height);
            var image=Model.Assets.GetBitmap(selected.AssetHash);
            await WriteClipboardImageAsync(image,size);StatusText.Text="선택한 이미지 복사됨";
        }
        finally{SetBusy(false);}
    }
    private async Task CopySelectedHistoryAsync()
    {
        if(busy||HistoryList.SelectedItems.Count!=1||HistoryList.SelectedItem is not CaptureEntry{Item.Damaged:false} entry)return;
        SetBusy(true);
        try
        {
            // 현재 편집기의 상태를 바꾸지 않고 선택 이력의 최신 저장 내용과 주석을 합성한다.
            await FlushAsync();var active=Model.CaptureId==entry.Item.Id;
            var source=Model;
            if(!active){source=new EditorViewModel();source.Open(await store.LoadAsync(entry.Item.Id));}
            var image=source.Composite()!;var token=source.History!.CurrentStateToken;
            await WriteClipboardImageAsync(image);
            if(active)source.History.MarkExported(token);
            StatusText.Text="선택한 이력 이미지 복사됨";
        }
        finally{SetBusy(false);}
    }
    private BitmapSource RenderClipboardAnnotation(Annotation annotation)
    {
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())CompositeRenderer.DrawAnnotation(dc,annotation,Model.Assets);
        var bounds=visual.ContentBounds;var width=Math.Max(1,(int)Math.Ceiling(bounds.Width));var height=Math.Max(1,(int)Math.Ceiling(bounds.Height));
        CompositeRenderer.GuardSize(width,height);
        var translated=new DrawingVisual();using(var dc=translated.RenderOpen())
        {dc.PushTransform(new TranslateTransform(-bounds.X,-bounds.Y));dc.DrawDrawing(visual.Drawing);dc.Pop();}
        var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(translated);image.Freeze();return image;
    }
    private async Task WriteClipboardImageAsync(BitmapSource image,ClipboardImageSize? objectSize=null,Annotation? annotation=null)
    {
        for(var attempt=0;;attempt++)
        {
            try{writeClipboardImage(new(image,objectSize,annotation));return;}
            catch(ExternalException)when(attempt<2){await Task.Delay(100);}
        }
    }
}
