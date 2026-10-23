using System.Windows;

namespace SnapBerry.App.Editor;

public partial class EditorWindow
{
    private void MagicWandOptionsChanged(object sender,RoutedEventArgs e)
    {
        if(!ready||busy||updatingProperties)return;
        Model.MagicWandTolerance=(int)MagicWandTolerance.Value;Model.MagicWandGlobal=MagicWandGlobal.IsChecked==true;
        Model.UpdateMagicWand(Model.MagicWandTolerance,Model.MagicWandGlobal);
    }
    private void DeleteMagicWandClick(object sender,RoutedEventArgs e)
    {if(!busy)DeleteSelectionSafely();}
    private void DeleteSelectionSafely()
    {
        try{canvas.DeleteSelected();}
        catch(Exception error)when(error is IOException or InvalidDataException or ArgumentException or InvalidOperationException){StatusText.Text="선택 영역 지우기 실패: "+error.Message;}
    }
}
