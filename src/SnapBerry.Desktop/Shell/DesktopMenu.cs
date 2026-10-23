using System.Windows.Input;
using Avalonia.Controls;
using SnapBerry.Desktop.Capture;
using SnapBerry.Desktop.Views;

namespace SnapBerry.Desktop.Shell;

public static class DesktopMenu
{
    public static NativeMenu Create(MainWindow window)
    {
        var menu=new NativeMenu();
        NativeMenuItem Item(string header,Func<Task> action)
        {var item=new NativeMenuItem{Header=header,Command=new MenuCommand(action,()=>!window.IsBusy&&!window.CanExit)};menu.Items.Add(item);return item;}
        Item("편집기 열기",()=>{window.ShowEditor();return Task.CompletedTask;});menu.Items.Add(new NativeMenuItemSeparator());
        Item("영역 캡처",()=>window.RequestCaptureAsync(DesktopCaptureMode.Region));Item("창 캡처",()=>window.RequestCaptureAsync(DesktopCaptureMode.Window));Item("전체 화면",()=>window.RequestCaptureAsync(DesktopCaptureMode.Display));
        menu.Items.Add(new NativeMenuItemSeparator());Item("단축키 설정",window.RequestHotkeySettingsAsync);Item("종료",async ()=>{await window.RequestExitAsync();});
        menu.Opening+=(_,_)=>{foreach(var item in menu.Items.OfType<NativeMenuItem>())item.IsEnabled=!window.IsBusy&&!window.CanExit;};return menu;
    }
    private sealed class MenuCommand(Func<Task> action,Func<bool> allowed) : ICommand
    {
        public bool CanExecute(object? parameter)=>allowed();
        public async void Execute(object? parameter){if(CanExecute(parameter))await action();}
        public event EventHandler? CanExecuteChanged {add{} remove{}}
    }
}
