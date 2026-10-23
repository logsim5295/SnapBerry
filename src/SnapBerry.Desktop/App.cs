using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using SnapBerry.Desktop.Shell;

namespace SnapBerry.Desktop;

public sealed class App : Application
{
    private TrayIcon? tray;
    public override void Initialize()=>Styles.Add(new PurpleTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var main=new Views.MainWindow();desktop.MainWindow=main;desktop.ShutdownMode=ShutdownMode.OnExplicitShutdown;
            using var icon=typeof(App).Assembly.GetManifestResourceStream("SnapBerry.Desktop.ico")!;
            tray=new TrayIcon{Icon=new WindowIcon(icon),ToolTipText="SnapBerry",Menu=DesktopMenu.Create(main)};TrayIcon.SetIcons(this,new TrayIcons{tray});
            NativeMenu.SetMenu(this,DesktopMenu.Create(main));
            main.ExitRequested+=()=>{tray?.Dispose();tray=null;desktop.Shutdown();};
            desktop.ShutdownRequested+=(_,args)=>{if(!main.CanExit){args.Cancel=true;_=main.RequestExitAsync();}};
            desktop.Exit+=(_,_)=>{main.Dispose();tray?.Dispose();tray=null;};
        }
        base.OnFrameworkInitializationCompleted();
    }
}
