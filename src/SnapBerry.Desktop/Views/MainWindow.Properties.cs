using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SkiaSharp;
using SnapBerry.Core;
using SnapBerry.Desktop.Editing;
using SnapBerry.Desktop.Stickers;
using PixelSize=SnapBerry.Core.PixelSize;

namespace SnapBerry.Desktop.Views;

public sealed partial class MainWindow
{
    private int mosaicSize=12;
    private CommandHistory? propertyEditHistory;
    private bool selectingTool;
    private bool EditsSelection=>editor.Selected is {} a&&(editor.Tool==DesktopTool.Select||editor.Tool switch
    {
        DesktopTool.Arrow=>a.Kind==AnnotationKind.Arrow,DesktopTool.Rectangle=>a.Kind==AnnotationKind.Rectangle,
        DesktopTool.Text=>a.Kind==AnnotationKind.Text,DesktopTool.Number=>a.Kind==AnnotationKind.StepMarker,
        DesktopTool.Mosaic=>a.Kind==AnnotationKind.Mosaic,_=>false
    });
    private bool IsSelectedKeycap=>editor.Selected is {} a&&DesktopStickerCatalog.Load().Any(d=>d.Id==a.StickerId&&d.IsKeycap);
    private void InitializeParityFeatures()
    {
        Wire("ColorPickerButton",PickColorAsync);Wire("ImageSizeButton",ResizeImageAsync);Wire("EditTextButton",EditSelectionTextAsync);
        Wire("AboutButton",ShowAboutAsync);Wire("SettingsButton",ShowSettingsAsync);
        Wire("EmptyCaptureButton",()=>CaptureAsync(Capture.DesktopCaptureMode.Region));
        Wire("ViewZoomInButton",()=>{surface.Zoom*=1.25;UpdateCommands();return Task.CompletedTask;});
        Wire("ViewZoomOutButton",()=>{surface.Zoom/=1.25;UpdateCommands();return Task.CompletedTask;});
        Wire("ViewFitButton",()=>{Fit();return Task.CompletedTask;});
        Wire("ViewHistoryButton",()=>{SetHistoryPanelVisible(!preferences.HistoryVisible);return Task.CompletedTask;});
        foreach(var button in Control<WrapPanel>("ToolButtons").Children.OfType<Button>().Where(b=>b.Tag is not null))
            button.Click+=(_,_)=>SelectTool((DesktopTool)int.Parse((string)button.Tag!));
        Control<ComboBox>("ToolBox").SelectionChanged+=(_,_)=>{if(!selectingTool)SelectTool((DesktopTool)Control<ComboBox>("ToolBox").SelectedIndex);};
        Control<TextBox>("TextEntry").MaxLength=AnnotationLimits.MaxTextLength;
        Control<TextBox>("TextEntry").TextChanged+=(_,_)=>surface.Gesture.Text=Control<TextBox>("TextEntry").Text??"";
        Control<TextBox>("ColorBox").LostFocus+=(_,_)=>TryProperty(()=>UpdateStyle("color"));
        Control<NumericUpDown>("ThicknessBox").ValueChanged+=(_,_)=>TryProperty(()=>UpdateStyle("thickness"));
        Control<NumericUpDown>("FontBox").ValueChanged+=(_,_)=>TryProperty(()=>UpdateStyle("font"));
        Control<ComboBox>("LineStyleBox").SelectionChanged+=(_,_)=>{if(!syncingProperties)TryProperty(()=>ApplyStyle(s=>s with{LineStyle=(StrokePattern)Control<ComboBox>("LineStyleBox").SelectedIndex}));};
        Control<Slider>("MosaicSlider").ValueChanged+=(_,_)=>
        {
            if(syncingProperties||busy)return;mosaicSize=(int)Math.Round(Control<Slider>("MosaicSlider").Value);surface.Gesture.MosaicSize=mosaicSize;
            Control<TextBlock>("MosaicValueText").Text=$"{mosaicSize} px";
            if(EditsSelection&&editor.Selected is {Kind:AnnotationKind.Mosaic} a&&a.MosaicBlockSize!=mosaicSize)editor.ReplaceSelected(a with{MosaicBlockSize=mosaicSize});
        };
        var mosaicSlider=Control<Slider>("MosaicSlider");
        mosaicSlider.AddHandler(PointerPressedEvent,(_,_)=>
        {
            if(busy||!EditsSelection||editor.Selected?.Kind!=AnnotationKind.Mosaic)return;
            EndPropertyEditGroup();propertyEditHistory=editor.History;propertyEditHistory?.BeginGroup();
        },RoutingStrategies.Tunnel);
        mosaicSlider.AddHandler(PointerReleasedEvent,(_,_)=>EndPropertyEditGroup(),RoutingStrategies.Tunnel);
        mosaicSlider.PointerCaptureLost+=(_,_)=>EndPropertyEditGroup();Deactivated+=(_,_)=>EndPropertyEditGroup();
        Control<ComboBox>("NumberModeBox").SelectionChanged+=(_,_)=>{if(!syncingProperties&&!busy)TryProperty(()=>editor.SetSequenceMode((StepSequenceMode)Control<ComboBox>("NumberModeBox").SelectedIndex));};
        void CommitNext(){if(!syncingProperties&&!busy)TryProperty(()=>editor.SetNextSequence(Control<TextBox>("NextNumberBox").Text??""));}
        Control<TextBox>("NextNumberBox").LostFocus+=(_,_)=>CommitNext();
        Control<TextBox>("NextNumberBox").KeyDown+=(_,e)=>{if(e.Key==Key.Enter){CommitNext();e.Handled=true;}};
        Wire("NumberTextColorButton",()=>{ApplyStyle(s=>s with{TextColor=s.TextColor=="#FFFFFF"?"#252C33":"#FFFFFF"});return Task.CompletedTask;});
        surface.TextEditRequested+=()=>_=RunAsync(EditSelectionTextAsync);
        surface.PointerWheelChanged+=(_,e)=>
        {
            if(busy||editor.Document is null||e.Delta.Y==0)return;var scroll=Control<ScrollViewer>("ImageScroll");var point=e.GetPosition(scroll);var before=surface.Zoom;
            surface.Zoom*=e.Delta.Y>0?1.1:1/1.1;scroll.UpdateLayout();var factor=surface.Zoom/before;
            scroll.Offset=new(Math.Max(0,(scroll.Offset.X+point.X)*factor-point.X),Math.Max(0,(scroll.Offset.Y+point.Y)*factor-point.Y));UpdateCommands();e.Handled=true;
        };
        RefreshSelection();
    }
    private void SelectTool(DesktopTool tool)
    {
        if(busy||!Enum.IsDefined(tool))return;EndPropertyEditGroup();surface.CancelGesture();editor.ClearMagicWand();editor.Tool=tool;if(tool==DesktopTool.MagicWand)editor.SelectedId=null;
        selectingTool=true;try{Control<ComboBox>("ToolBox").SelectedIndex=(int)tool;}finally{selectingTool=false;}
        if(tool==DesktopTool.Image)_=RunAsync(()=>OpenImageAsync(true));RefreshSelection();
    }
    private void EndPropertyEditGroup(){var history=propertyEditHistory;propertyEditHistory=null;history?.EndGroup();}
    private void TryProperty(Action action)
    {
        if(syncingProperties||busy)return;
        try{action();}catch(Exception error)when(error is ArgumentException or FormatException or OverflowException or InvalidOperationException){Status(error.Message);}
    }
    private void ApplyStyle(Func<AnnotationStyle,AnnotationStyle> change)
    {
        var previous=EditsSelection?editor.Selected!.Style:editor.Style;var next=change(previous);
        if(next==previous)return;editor.Style=next;
        if(EditsSelection&&editor.Selected is {} selected)editor.ReplaceSelected(selected with{Style=next});else RefreshSelection();
    }
    private void UpdateStyle(string property)
    {
        if(syncingProperties||busy)return;
        if(property=="color")
        {
            var text=Control<TextBox>("ColorBox").Text??"";if(text==colorBaseline)return;
            if(!SKColor.TryParse(text,out var color))throw new ArgumentException("색상은 #RRGGBB 또는 #AARRGGBB 형식으로 입력해 주세요.");
            var value=color.Alpha==255?$"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}":$"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
            ApplyStyle(s=>s with{Color=value,Fill=value});
        }
        else if(property=="thickness")ApplyStyle(s=>s with{Thickness=(double)(Control<NumericUpDown>("ThicknessBox").Value??3)});
        else if(property=="font")ApplyStyle(s=>s with{FontSize=(double)(Control<NumericUpDown>("FontBox").Value??24)});
    }
    public void RefreshSelection()
    {
        syncingProperties=true;
        try
        {
            Control<WrapPanel>("MagicWandControls").IsVisible=editor.Tool==DesktopTool.MagicWand;
            var style=EditsSelection?editor.Selected!.Style:editor.Style;
            if(EditsSelection)editor.Style=style;
            Control<TextBox>("ColorBox").Text=style.Color;colorBaseline=style.Color;
            Control<NumericUpDown>("ThicknessBox").Value=(decimal)style.Thickness;Control<NumericUpDown>("FontBox").Value=(decimal)style.FontSize;
            Control<ComboBox>("LineStyleBox").SelectedIndex=(int)style.LineStyle;
            if(editor.Selected is {Kind:AnnotationKind.Text} text||IsSelectedKeycap)Control<TextBox>("TextEntry").Text=editor.Selected!.Text;
            var size=EditsSelection&&editor.Selected is {Kind:AnnotationKind.Mosaic} mosaic?mosaic.MosaicBlockSize:mosaicSize;
            Control<Slider>("MosaicSlider").Value=Math.Clamp(size,4,64);Control<TextBlock>("MosaicValueText").Text=$"{size} px";surface.Gesture.MosaicSize=mosaicSize;
            if(editor.Document is {} document)
            {Control<ComboBox>("NumberModeBox").SelectedIndex=(int)document.Sequence.Mode;Control<TextBox>("NextNumberBox").Text=StepLabelFormatter.Format(document.Sequence.GetNextValue(),document.Sequence.Mode);}
            Control<Button>("NumberTextColorButton").Content=style.TextColor=="#FFFFFF"?"글자색: 흰색":"글자색: 검정";
        }
        finally{syncingProperties=false;}
        UpdateCommands();
    }
    private void UpdateParityCommands()
    {
        var has=editor.Document is not null;var tool=editor.Tool;var kind=EditsSelection?editor.Selected?.Kind:null;var keycap=tool==DesktopTool.Select&&IsSelectedKeycap;
        bool Show(string name,bool condition){Control<WrapPanel>(name).IsVisible=has&&condition;return has&&condition;}
        var color=Show("ColorControls",tool is DesktopTool.Arrow or DesktopTool.Rectangle or DesktopTool.Text or DesktopTool.Number||kind is AnnotationKind.Arrow or AnnotationKind.Rectangle or AnnotationKind.Text or AnnotationKind.StepMarker);
        var stroke=Show("ThicknessControls",tool is DesktopTool.Arrow or DesktopTool.Rectangle||kind is AnnotationKind.Arrow or AnnotationKind.Rectangle);
        Show("LineStyleControls",stroke);var font=Show("FontControls",tool==DesktopTool.Text||kind==AnnotationKind.Text);
        Show("TextControls",font||keycap);Show("KeycapControls",keycap);
        var mosaic=Show("MosaicControls",tool==DesktopTool.Mosaic||kind==AnnotationKind.Mosaic);
        var number=Show("NumberControls",tool==DesktopTool.Number||kind==AnnotationKind.StepMarker);
        Control<Border>("PropertiesBorder").IsVisible=color||stroke||font||mosaic||number||keycap||has&&tool==DesktopTool.MagicWand;
        Control<Button>("ApplyTextButton").IsVisible=!keycap;Control<Button>("ApplyTextButton").IsEnabled=!busy&&editor.Selected?.Kind==AnnotationKind.Text;
        foreach(var button in Control<WrapPanel>("ToolButtons").Children.OfType<Button>().Where(b=>b.Tag is not null))
        {button.IsEnabled=!busy&&has;button.Classes.Set("active",(string)button.Tag! == ((int)tool).ToString());}
        Control<Button>("EditTextButton").IsEnabled=!busy&&(editor.Selected?.Kind is AnnotationKind.Text or AnnotationKind.StepMarker||IsSelectedKeycap);
        Control<Button>("ImageSizeButton").IsEnabled=!busy&&has;
        Control<Button>("ImageSizeButton").Content=editor.Document is {} doc?$"{doc.OutputSize.Width} × {doc.OutputSize.Height} px":"이미지 없음";
        foreach(var name in new[]{"ViewFitButton","ViewZoomInButton","ViewZoomOutButton"})Control<Button>(name).IsEnabled=!busy&&has;
    }
    private async Task PickColorAsync()
    {
        var previous=EditsSelection?editor.Selected!.Style:editor.Style;var original=SKColor.Parse(previous.Color);
        var dialog=new ColorPickerWindow($"#{original.Red:X2}{original.Green:X2}{original.Blue:X2}"){RequestedThemeVariant=ActualThemeVariant};
        var selected=await dialog.ShowDialog<string?>(this);
        if(selected is not null){var color=original.Alpha==255?selected:$"#{original.Alpha:X2}{selected.TrimStart('#')}";var style=previous with{Color=color,Fill=color};editor.Style=style;if(EditsSelection&&editor.Selected is {} a)editor.ReplaceSelected(a with{Style=style});else RefreshSelection();}
    }
    private async Task ResizeImageAsync()
    {
        if(editor.Document is not {} doc)return;var dialog=new ImageResizeWindow(new(doc.Width,doc.Height),doc.OutputSize){RequestedThemeVariant=ActualThemeVariant};
        var size=await dialog.ShowDialog<PixelSize?>(this);if(size is PixelSize output){editor.Resize(output);Fit();}
    }
    private async Task EditSelectionTextAsync()
    {
        if(editor.Selected is not {} selected)return;var multiline=selected.Kind==AnnotationKind.Text;
        var dialog=new TextEditWindow(selected.Text,multiline?"텍스트 편집":selected.Kind==AnnotationKind.StepMarker?"번호 편집":"키 라벨 편집",multiline,multiline?AnnotationLimits.MaxTextLength:128){RequestedThemeVariant=ActualThemeVariant};
        var text=await dialog.ShowDialog<string?>(this);if(text is not null&&editor.SelectedId==selected.Id)editor.EditSelectedText(text);
    }
    private async Task<IStorageFolder?> GetExportDirectoryAsync()
    {
        if(string.IsNullOrWhiteSpace(preferences.LastSaveDirectory)||!Directory.Exists(preferences.LastSaveDirectory))return null;
        try{return await StorageProvider.TryGetFolderFromPathAsync(preferences.LastSaveDirectory);}catch(IOException){return null;}
    }
    private void RememberExportDirectory(string directory)
    {
        try{var next=preferences with{LastSaveDirectory=directory};next.Save(dataDirectory);preferences=next;}
        catch(Exception error)when(error is IOException or UnauthorizedAccessException){Status("PNG 저장됨 · 저장 위치 기억 실패: "+error.Message);}
    }
}
