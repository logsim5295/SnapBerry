using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SnapBerry.App.Stickers;
using SnapBerry.Desktop.Stickers;

namespace SnapBerry.Desktop.Views;

public sealed class StickerPickerWindow : Window,IDisposable
{
    private readonly WrapPanel stickers=new();private readonly List<Bitmap> images=[];
    private readonly ComboBox categories;private readonly TextBox search;private readonly Action<StickerDefinition> select;
    public StickerPickerWindow(Action<StickerDefinition> select)
    {
        var scope=new NameScope();NameScope.SetNameScope(this,scope);
        this.select=select;Title="스티커";Width=530;Height=480;MinWidth=320;MinHeight=300;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new Grid{RowDefinitions=new("Auto,Auto,*"),Margin=new Thickness(16)};
        root.Children.Add(new TextBlock{Text="스티커",FontSize=22,FontWeight=FontWeight.SemiBold});
        var filters=new Grid{ColumnDefinitions=new("150,*"),Margin=new Thickness(0,12)};Grid.SetRow(filters,1);root.Children.Add(filters);
        categories=new ComboBox{Name="StickerCategories",ItemsSource=new[]{"전체"}.Concat(DesktopStickerCatalog.Load().Select(x=>DesktopStickerCatalog.CategoryName(x.Category)).Distinct()).ToArray(),SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};filters.Children.Add(categories);
        search=new TextBox{Name="StickerSearch",PlaceholderText="스티커 검색",Margin=new Thickness(8,0,0,0)};Grid.SetColumn(search,1);filters.Children.Add(search);
        scope.Register(categories.Name!,categories);scope.Register(search.Name!,search);
        var scroll=new ScrollViewer{Content=stickers,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};Grid.SetRow(scroll,2);root.Children.Add(scroll);Content=root;
        categories.SelectionChanged+=(_,_)=>Populate();search.TextChanged+=(_,_)=>Populate();Closed+=(_,_)=>Dispose();Populate();
    }
    private void Populate()
    {
        stickers.Children.Clear();foreach(var image in images)image.Dispose();images.Clear();
        var selectedCategory=categories.SelectedItem as string;var category=DesktopStickerCatalog.Load().FirstOrDefault(x=>DesktopStickerCatalog.CategoryName(x.Category)==selectedCategory)?.Category;
        foreach(var definition in DesktopStickerCatalog.Search(category,search.Text??""))
        {
            var bitmap=new Bitmap(new MemoryStream(StickerPngRenderer.CreatePng(definition)));images.Add(bitmap);
            var stack=new StackPanel{Spacing=4};stack.Children.Add(new Image{Source=bitmap,Width=68,Height=68,Stretch=Stretch.Uniform});stack.Children.Add(new TextBlock{Text=definition.Name,FontSize=11,TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis});
            var button=new Button{Content=stack,Width=108,Height=108,Margin=new Thickness(4),Tag=definition,HorizontalContentAlignment=HorizontalAlignment.Stretch};ToolTip.SetTip(button,definition.Name);
            button.Click+=(_,_)=>{Selected=definition;select(definition);};stickers.Children.Add(button);
        }
    }
    public StickerDefinition? Selected {get;private set;}
    public void Dispose(){foreach(var image in images)image.Dispose();images.Clear();}
}
