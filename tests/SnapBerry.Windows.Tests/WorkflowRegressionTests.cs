using System.IO;
using System.Windows;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 문서 열기·편집·복사·저장 등 사용자 작업 흐름을 통합 검증한다.
public class WorkflowRegressionTests
{
    private sealed class Picker(Func<Window,string?> select) : IExportPathPicker { public string? Pick(Window owner,string directory,string suggestedFileName)=>select(owner); }
    private static void Pump(Task task)
    {var dispatcher=Dispatcher.CurrentDispatcher;var frame=new DispatcherFrame();task.ContinueWith(_=>dispatcher.BeginInvoke(()=>frame.Continue=false));Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();}
    [Fact] public void UndoBeforeDebouncePersistsCurrentState()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var store=new CaptureLibraryStore(Path.Combine(path,"Library"));EditorWindow? window=null;
        try{var record=new CaptureRecord{Width=40,Height=30,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(40,30))};store.SaveAsync(record).GetAwaiter().GetResult();window=new(store,new SettingsService(path));window.Model.Open(record);window.Model.History!.Add(new(){Text="undone"});window.Model.History.Undo();Pump(window.FlushAsync());Assert.Empty(store.LoadAsync(record.Id).GetAwaiter().GetResult().Annotations);}finally{window?.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Fact] public void ModalExportBlocksDocumentSwitch()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));EditorWindow? window=null;
        try{var store=new CaptureLibraryStore(Path.Combine(path,"Library"));var record=new CaptureRecord{Width=40,Height=30,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(40,30))};var busy=false;var picker=new Picker(_=>{busy=window!.IsBusy;Pump(window.OpenRecordAsync(Guid.NewGuid()));return null;});window=new(store,new SettingsService(path),picker);window.Model.Open(record);Pump(window.SaveAsync());Assert.True(busy);Assert.Equal(record.Id,window.Model.CaptureId);Assert.False(window.IsBusy);Pump(window.FlushAsync());}finally{window?.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Fact] public void LibraryFailureDoesNotPreventExternalPngExport()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);var blocked=Path.Combine(path,"blocked");File.WriteAllText(blocked,"not a directory");var output=Path.Combine(path,"recovered.png");EditorWindow? window=null;
        try{var store=new CaptureLibraryStore(blocked);window=new(store,new SettingsService(path),new Picker(_=>output));window.Model.Open(new(){Width=40,Height=30,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(40,30))});window.Model.History!.Add(new(){Kind=AnnotationKind.Text,Text="recovered"});Pump(window.SaveAsync());Assert.True(File.Exists(output));Assert.True(window.Model.History.IsPersistenceDirty);}finally{window?.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
