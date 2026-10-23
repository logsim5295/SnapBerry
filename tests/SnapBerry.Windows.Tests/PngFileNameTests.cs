using System.IO;
using System.Windows;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 실제 저장 대화상자를 열지 않고 기본 파일명과 활성 이력 이름 전달을 확인한다.
public class PngFileNameTests
{
    private static readonly DateTime Timestamp=new(2026,10,20,14,5,6);
    private static string Suggest(string name)=>ExportPathPicker.SuggestFileName(name,Timestamp);
    [Theory]
    [InlineData("업무 결과","업무 결과.png")]
    [InlineData("  작업 초안  ","작업 초안.png")]
    [InlineData("결과.png","결과.png")]
    [InlineData("결과.PNG","결과.png")]
    [InlineData("초안:수정/완료?","초안_수정_완료_.png")]
    [InlineData("결과... ","결과.png")]
    [InlineData("CON","_CON.png")]
    [InlineData("nul.txt","_nul.txt.png")]
    [InlineData("COM1","_COM1.png")]
    [InlineData("LPT9","_LPT9.png")]
    [InlineData("COM¹","_COM¹.png")]
    public void NamedHistoryProducesAValidPngFileName(string name,string expected)
    {var actual=Suggest(name);Assert.Equal(expected,actual);Assert.DoesNotContain(actual,c=>Path.GetInvalidFileNameChars().Contains(c));}
    [Theory] [InlineData("")] [InlineData("   ")] [InlineData("...")] [InlineData(".png")]
    public void MissingOrEmptyFileNameUsesTheExistingTimestampPattern(string name)
        =>Assert.Equal("Capture_20261020_140506.png",Suggest(name));

    private sealed class Picker : IExportPathPicker
    {
        public List<string> Names {get;}=[];
        public string? Pick(Window owner,string directory,string suggestedFileName){Names.Add(suggestedFileName);return null;}
    }
    private static void Pump(Task task)
    {var dispatcher=Dispatcher.CurrentDispatcher;var frame=new DispatcherFrame();task.ContinueWith(_=>dispatcher.BeginInvoke(()=>frame.Continue=false));Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();}
    [Fact] public void ExportUsesTheLatestNameWithoutChangingHistoryOrTheDocument()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-export-name-"+Guid.NewGuid().ToString("N"));
        var picker=new Picker();var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path),picker);
        try
        {
            var record=new CaptureRecord{Name="처음 이름",Width=40,Height=30,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(40,30))};
            window.Model.Open(record);var document=window.Model.Document;var history=window.Model.History;
            Pump(window.SaveAsync());window.Model.SetCaptureName("최종:결과");Pump(window.SaveAsync());
            Assert.Equal(new[]{"처음 이름.png","최종_결과.png"},picker.Names);
            Assert.Equal("최종:결과",window.Model.Snapshot().Name);
            Assert.Same(document,window.Model.Document);Assert.Same(history,window.Model.History);Assert.False(window.IsBusy);Assert.False(window.IsVisible);
            Pump(window.FlushAsync());
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
