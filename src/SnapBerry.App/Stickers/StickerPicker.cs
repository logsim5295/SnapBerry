using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Shell;
using SnapBerry.App.Export;
using SnapBerry.App.Editor;
namespace SnapBerry.App.Stickers;
// 편집기를 막지 않는 비모달 도구창이다. 선택 이벤트만 전달하므로 다른 스티커도 연속 삽입할 수 있다.
public sealed class StickerPicker : Window
{
    public StickerDefinition? Selected {get;private set;}
    public event Action<StickerDefinition>? StickerSelected;
    public StickerPicker(StickerDefinition? selected=null)
    {
        NameScope.SetNameScope(this,new NameScope());
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SnapBerry;component/Editor/ButtonStyles.xaml",UriKind.Relative)});
        ThemeResources.Initialize(this);
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SnapBerry;component/Editor/ScrollBarStyles.xaml",UriKind.Relative)});
        Selected=selected;Title="스티커";Width=370;Height=360;MinWidth=270;MinHeight=300;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResize;AllowsTransparency=true;ShowInTaskbar=false;ShowActivated=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Brushes.Transparent;FontFamily=new FontFamily("Segoe UI");FontSize=13;
        // 비모달 도구창의 크기 조절은 유지하고 버튼을 누르는 영역은 제목 드래그에서 분리한다.
        WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=0,CornerRadius=new CornerRadius(8),ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),UseAeroCaptionButtons=false});
        var grid=new Grid();grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        var header=new Grid();header.ColumnDefinitions.Add(new());header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        // 바깥 Margin 대신 입력을 받는 Padding으로 제목 위·옆 여백도 드래그 영역에 포함한다.
        var headerDragArea=new Border{Background=Brushes.Transparent,Padding=new Thickness(16),Child=header};RegisterName("StickerHeaderDragArea",headerDragArea);
        headerDragArea.MouseLeftButtonDown+=(_,e)=>{if(e.ButtonState==MouseButtonState.Pressed){e.Handled=true;DragMove();}};
        grid.Children.Add(headerDragArea);
        var title=new StackPanel{Orientation=Orientation.Horizontal,Background=Brushes.Transparent};var icon=new Image{Source=RibbonIcons.Create("Sticker"),Width=28,Height=28,Margin=new Thickness(0,0,10,0)};RegisterName("StickerHeaderIcon",icon);title.Children.Add(icon);
        title.Children.Add(new TextBlock{Text=Title,FontSize=20,FontWeight=FontWeights.SemiBold,Foreground=ThemeResources.Get(this,"Text"),VerticalAlignment=VerticalAlignment.Center});header.Children.Add(title);
        var close=new Button{Content=new TextBlock{Text="\uE8BB",FontFamily=new FontFamily("Segoe Fluent Icons"),FontSize=12},Width=32,Height=32,ToolTip="닫기",Focusable=false,Foreground=ThemeResources.Get(this,"Accent")};RegisterName("CloseStickerPickerButton",close);System.Windows.Automation.AutomationProperties.SetName(close,"스티커 창 닫기");close.Click+=(_,_)=>Close();Grid.SetColumn(close,1);header.Children.Add(close);
        var dock=new DockPanel{Margin=new Thickness(16,0,16,16)};RegisterName("StickerContent",dock);Grid.SetRow(dock,1);grid.Children.Add(dock);
        var categoryNames=new Dictionary<string,string>{{"Emoji","이모지"},{"Keyboard","키보드"},{"Check / X","체크 / 엑스"},{"Symbols","기호"},{"Directions","방향"},{"Work","업무"}};
        var categories=new ComboBox{Style=(Style)FindResource("PurpleComboBoxStyle"),DisplayMemberPath="Value",SelectedValuePath="Key",ItemsSource=StickerCatalog.Load().Select(x=>x.Category).Distinct().Select(category=>new KeyValuePair<string,string>(category,categoryNames.GetValueOrDefault(category,category))).ToArray(),SelectedValue=selected?.Category??"Emoji",Height=32,Margin=new Thickness(0,0,0,12)};RegisterName("StickerCategories",categories);DockPanel.SetDock(categories,Dock.Top);dock.Children.Add(categories);
        var wrap=new WrapPanel();var scroll=new ScrollViewer{Content=wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};RegisterName("StickerScroll",scroll);dock.Children.Add(scroll);
        var buttons=new List<(Button Button,StickerDefinition Sticker)>();
        void HighlightSelection()
        {
            foreach(var (button,definition) in buttons)
            {
                if(definition.Id==Selected?.Id){button.Background=ThemeResources.Get(this,"Selected");button.BorderBrush=ThemeResources.Get(this,"Accent");}
                else{button.ClearValue(Button.BackgroundProperty);button.ClearValue(Button.BorderBrushProperty);}
            }
        }
        void Populate()
        {
            // 카테고리가 바뀔 때 표시 버튼을 다시 만들고 현재 선택한 스티커의 강조를 복원한다.
            wrap.Children.Clear();buttons.Clear();foreach(var definition in StickerCatalog.Load().Where(x=>x.Category==(string)categories.SelectedValue))
            {
                var stack=new StackPanel();stack.Children.Add(new Image{Source=PngFileWriter.Decode(StickerCatalog.CreatePng(definition)),Width=72,Height=72,Stretch=Stretch.Uniform});stack.Children.Add(new TextBlock{Text=definition.Name,FontSize=11,TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis});
                var button=new Button{Style=(Style)FindResource("PurpleStickerButtonStyle"),Content=stack,Width=96,Height=108,Margin=new Thickness(4),ToolTip=definition.Name};button.Click+=(_,_)=>{Selected=definition;HighlightSelection();StickerSelected?.Invoke(definition);};wrap.Children.Add(button);buttons.Add((button,definition));
            }HighlightSelection();
        }
        categories.SelectionChanged+=(_,_)=>Populate();Content=new Border{Background=ThemeResources.Get(this,"Surface"),BorderBrush=ThemeResources.Get(this,"Selected"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Child=grid};Populate();
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;Close();}};
    }
}
