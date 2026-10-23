using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SnapBerry.App.Editor;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 삭제 확인창의 기본 삭제 동작과 취소 경로, 브랜드 외형·대상 개수 표시를 검증한다.
public class DeleteCaptureDialogTests
{
    [Fact] public void DeleteIsTheDefaultAndInitialFocusTargetWithoutShowingWindow()=>Sta.Run(()=>
    {
        var dialog=new DeleteCaptureDialog(1);
        try
        {
            var delete=(Button)dialog.FindName("ConfirmDeleteButton");var cancel=(Button)dialog.FindName("CancelDeleteButton");
            Assert.True(delete.IsDefault);Assert.False(cancel.IsDefault);Assert.True(cancel.IsCancel);
            Assert.Same(delete,FocusManager.GetFocusedElement(dialog));Assert.False(dialog.IsVisible);Assert.Null(dialog.DialogResult);
        }
        finally{dialog.Close();}
    });

    [Theory]
    [InlineData("Enter",true)] [InlineData("Escape",false)]
    [InlineData("Delete",true)] [InlineData("Cancel",false)] [InlineData("Close",false)]
    public void EnterAndDeleteConfirmWhileOtherActionsCancel(string action,bool expected)=>Sta.Run(()=>
    {
        var dialog=new DeleteCaptureDialog(3);
        dialog.Loaded+=(_,_)=>dialog.Dispatcher.BeginInvoke(()=>
        {
            var cancel=(Button)dialog.FindName("CancelDeleteButton");var delete=(Button)dialog.FindName("ConfirmDeleteButton");
            Assert.False(cancel.IsDefault);Assert.True(cancel.IsCancel);Assert.True(delete.IsDefault);Assert.True(delete.IsKeyboardFocused);
            Assert.Contains("3개",((TextBlock)dialog.FindName("DeleteMessage")).Text);
            Assert.Contains("복구",((TextBlock)dialog.FindName("DeleteMessage")).Text);
            var buttons=(Panel)cancel.Parent;Assert.Equal(cancel,buttons.Children[0]);Assert.Equal(delete,buttons.Children[1]);
            Assert.Equal("#FF923C83",delete.Background.ToString());
            Assert.NotNull(((Image)dialog.FindName("DeleteDialogIcon")).Source);
            var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
            if(action=="Cancel"&&artifacts is not null)
            {
                System.IO.Directory.CreateDirectory(artifacts);var content=(FrameworkElement)dialog.Content;content.UpdateLayout();
                var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(content);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(artifacts,"delete-capture-dialog.png"),SnapBerry.App.Export.PngFileWriter.Encode(image));
            }
            if(action=="Enter")AccessKeyManager.ProcessKey(dialog,"\r",false);
            else if(action=="Escape")
            {
                dialog.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(dialog),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});
            }
            else if(action=="Close")dialog.Close();
            else (action=="Delete"?delete:cancel).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        });
        Assert.Equal(expected,dialog.ShowDialog()==true);
    });

    [Fact] public void RejectsEmptyDeletionCount()=>Sta.Run(()=>Assert.Throws<ArgumentOutOfRangeException>(()=>new DeleteCaptureDialog(0)));
}
