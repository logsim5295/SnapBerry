using Avalonia.Controls;

namespace SnapBerry.Desktop.Views;

public sealed partial class MainWindow
{
    private void InitializeMagicWand()
    {
        void OptionsChanged()
        {
            if(busy||syncingProperties)return;
            editor.MagicWandTolerance=(int)Control<Slider>("MagicWandTolerance").Value;
            Control<TextBlock>("MagicWandToleranceValue").Text=$"{editor.MagicWandTolerance}%";
            editor.MagicWandGlobal=Control<CheckBox>("MagicWandGlobal").IsChecked==true;
            editor.UpdateMagicWand(editor.MagicWandTolerance,editor.MagicWandGlobal);
        }
        Control<Slider>("MagicWandTolerance").ValueChanged+=(_,_)=>OptionsChanged();
        Control<CheckBox>("MagicWandGlobal").IsCheckedChanged+=(_,_)=>OptionsChanged();
        Control<Button>("DeleteMagicWandButton").Click+=(_,_)=>
        {if(!busy)DeleteSelectionSafely();};
        editor.MagicWandChanged+=MagicWandChanged;
    }
    private void DeleteSelectionSafely()
    {
        try{editor.DeleteSelected();}
        catch(Exception error)when(error is IOException or InvalidDataException or ArgumentException or InvalidOperationException){Status("선택 영역 지우기 실패: "+error.Message);}
    }
    private void MagicWandChanged(object? sender,EventArgs args)
    {
        if(closed)return;RefreshSelection();
        if(editor.Tool==Editing.DesktopTool.MagicWand)Status(editor.HasPixelSelection?$"{editor.MagicWandCount:N0} 픽셀 선택됨 · Delete로 지우기 · Esc로 취소":"배경 지우개: 원본 이미지의 색상을 클릭하세요.");
    }
}
