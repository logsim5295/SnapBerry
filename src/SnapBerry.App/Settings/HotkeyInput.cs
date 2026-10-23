using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace SnapBerry.App.Settings;
// 문자열을 직접 편집하는 대신 실제 키 조합을 캡처하는 설정 입력란이다.
public sealed class HotkeyInput : TextBox
{
    private string original="";
    private HotkeyInputHook? printScreenHook;
    private Window? hookOwner;private object? captureScope;
    internal bool IsPrintScreenHookActive=>printScreenHook?.IsInstalled==true;
    public string? CaptureError {get;private set;}
    public event Action? CaptureStatusChanged;
    public HotkeyInput(){IsReadOnly=true;IsReadOnlyCaretVisible=false;ContextMenu=null;Cursor=Cursors.Hand;Unloaded+=(_,_)=>ReleaseHook();}
    public void SetBindingText(string value){CaptureError=null;Text=value;CaptureStatusChanged?.Invoke();}
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        original=Text;base.OnGotKeyboardFocus(e);SelectAll();ReleaseHook();var window=Window.GetWindow(this);if(window is null)return;var hwnd=new WindowInteropHelper(window).Handle;var scope=new object();captureScope=scope;hookOwner=window;window.Closed+=OwnerClosed;
        try
        {
            printScreenHook=new(()=>IsKeyboardFocusWithin&&GetForegroundWindow()==hwnd,(key,modifiers)=>
            {
                // 검증 중 네이티브 API를 호출할 수 있으므로 전역 후크에서 즉시 반환하고 UI 큐로 넘긴다.
                Dispatcher.BeginInvoke(()=>{if(ReferenceEquals(captureScope,scope)&&IsKeyboardFocusWithin&&window.IsActive)SetBindingText(FormatGesture(key,modifiers));});
            });
        }
        catch(System.ComponentModel.Win32Exception){CaptureError="단축키 입력을 준비하지 못했습니다. 입력란을 다시 클릭해 주세요.";CaptureStatusChanged?.Invoke();}
    }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e){ReleaseHook();base.OnLostKeyboardFocus(e);}
    private void OwnerClosed(object? sender,EventArgs e)=>ReleaseHook();
    private void ReleaseHook(){captureScope=null;printScreenHook?.Dispose();printScreenHook=null;if(hookOwner is not null)hookOwner.Closed-=OwnerClosed;hookOwner=null;}
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Tab은 포커스 이동, Esc는 입력 전 값 복원에 사용한다. 수정키만 누른 중간 상태는 저장하지 않는다.
        var key=e.Key==Key.System?e.SystemKey:e.Key;var modifiers=Keyboard.Modifiers;
        if(key==Key.Tab&&(modifiers is ModifierKeys.None or ModifierKeys.Shift)){base.OnPreviewKeyDown(e);return;}
        e.Handled=true;
        if(key==Key.Escape){SetBindingText(original);return;}
        if(IsModifier(key))return;
        try{SetBindingText(FormatGesture(key,modifiers));}catch(ArgumentException ex){CaptureError=ex.Message;CaptureStatusChanged?.Invoke();}
    }
    private static bool IsModifier(Key key)=>key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
    public static string FormatGesture(Key key,ModifierKeys modifiers)
    {
        if((modifiers==ModifierKeys.None&&key!=Key.Snapshot)||IsModifier(key)||key is Key.None or Key.System or Key.Escape)throw new ArgumentException("Ctrl, Alt, Shift, Win 중 하나와 다른 키를 함께 눌러 주세요.");
        var parts=new List<string>();if(modifiers.HasFlag(ModifierKeys.Control))parts.Add("Ctrl");if(modifiers.HasFlag(ModifierKeys.Alt))parts.Add("Alt");if(modifiers.HasFlag(ModifierKeys.Shift))parts.Add("Shift");if(modifiers.HasFlag(ModifierKeys.Windows))parts.Add("Win");
        parts.Add(key==Key.Snapshot?"PrintScreen":key>=Key.D0&&key<=Key.D9?((int)key-(int)Key.D0).ToString():key.ToString());var value=string.Join("+",parts);HotkeyService.Parse(value);return value;
    }
}
