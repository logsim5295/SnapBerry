using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Interop;
using System.Xml.Linq;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.App.Rendering;
using SnapBerry.App.Stickers;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 대표 이미지와 편집 작업의 처리 비용을 측정해 성능 회귀를 확인한다.
public class PerformanceTests
{
    private const string ResourceWorker="SNAPBERRY_RENDER_RESOURCE_WORKER";
    [DllImport("user32.dll")]private static extern int GetGuiResources(IntPtr process,int flags);
    [Fact] public void Repeated4KRenderReleasesBuffers()
    {
        if(Environment.GetEnvironmentVariable(ResourceWorker)!="1"){RunResourceWorker();return;}
        Assert.NotEqual(Environment.ProcessId,int.Parse(Environment.GetEnvironmentVariable("SNAPBERRY_RENDER_PARENT_PID")!));
        Sta.Run(MeasureRenderResources);
    }
    private static ProcessStartInfo ResourceWorkerStart(string results)
    {
        var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add("vstest");start.ArgumentList.Add(typeof(PerformanceTests).Assembly.Location);
        start.ArgumentList.Add($"--TestCaseFilter:FullyQualifiedName={typeof(PerformanceTests).FullName}.{nameof(Repeated4KRenderReleasesBuffers)}");
        start.ArgumentList.Add("--logger:trx;LogFileName=render-worker.trx");start.ArgumentList.Add($"--ResultsDirectory:{results}");
        start.Environment[ResourceWorker]="1";start.Environment["SNAPBERRY_RENDER_PARENT_PID"]=Environment.ProcessId.ToString();return start;
    }
    private static void RunResourceWorker()
    {
        var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");var results=Path.Combine(artifacts??Path.GetTempPath(),"render-worker-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(results);
        try
        {
            using var worker=Process.Start(ResourceWorkerStart(results))!;var output=worker.StandardOutput.ReadToEndAsync();var error=worker.StandardError.ReadToEndAsync();
            if(!worker.WaitForExit(120000)){worker.Kill(entireProcessTree:true);worker.WaitForExit();Assert.Fail("The isolated render resource test timed out after 120 seconds.");}
            var log=output.GetAwaiter().GetResult()+error.GetAwaiter().GetResult();var report=Path.Combine(results,"render-worker.trx");
            Assert.True(File.Exists(report),$"The render worker did not produce a test result. {log}");
            var records=XDocument.Load(report).Descendants().Where(x=>x.Name.LocalName=="UnitTestResult").ToArray();Assert.Single(records);
            Assert.True(worker.ExitCode==0&&(string?)records[0].Attribute("outcome")=="Passed",$"The isolated render resource test failed. {records[0]} {log}");
        }
        finally{if(artifacts is null)TestFiles.DeleteDirectory(results);}
    }
    private static void MeasureRenderResources()
    {
        // 이 테스트 프로세스의 WPF 자원이 만들어지기 전에 실제 앱 시작과 같은 렌더링 모드를 설정한다.
        RenderOptions.ProcessRenderMode=RenderMode.SoftwareOnly;
        var source=RenderingTests.Image(3840,2160);var renderer=new CompositeRenderer();var assets=new StickerAssetStore();using var process=Process.GetCurrentProcess();
        int SampleResources(){CollectAndDrain();process.Refresh();return GetGuiResources(process.Handle,0);}
        // WPF 그래픽 캐시는 첫 렌더링뿐 아니라 여러 프레임에 걸쳐 지연 생성되므로 워밍업한다.
        var warmup=WarmUpResources(()=>renderer.Render(source,[],assets),SampleResources);
        var handlesBefore=warmup[^1];var before=process.PrivateMemorySize64;var samples=new List<long>();var handleSamples=new List<int>();
        var sw=Stopwatch.StartNew();for(var i=0;i<50;i++){Assert.Equal(3840,renderer.Render(source,[],assets).PixelWidth);if(i%10==9){handleSamples.Add(SampleResources());samples.Add(process.PrivateMemorySize64);}}
        sw.Stop();var delta=samples[^1]-before;var handleDelta=handleSamples[^1]-handlesBefore;
        Write("render-metrics.json",new{ProcessId=Environment.ProcessId,ParentProcessId=int.Parse(Environment.GetEnvironmentVariable("SNAPBERRY_RENDER_PARENT_PID")!),RenderMode=RenderOptions.ProcessRenderMode.ToString(),Iterations=50,Width=3840,Height=2160,ElapsedMilliseconds=sw.ElapsedMilliseconds,WarmupGdiHandles=warmup,PrivateMemoryBefore=before,PrivateMemorySamples=samples,PrivateMemoryDelta=delta,GdiHandlesBefore=handlesBefore,GdiHandleSamples=handleSamples,GdiHandleDelta=handleDelta,OS=Environment.OSVersion.VersionString,Processors=Environment.ProcessorCount});
        Assert.True(delta<256L*1024*1024,$"Private memory increased by {delta} bytes.");Assert.True(handleDelta<=4,$"GDI handles increased by {handleDelta}.");
    }
    [Fact] public void ResourceWorkerIsRestrictedToTheRenderTestAndASeparateProcess()
    {
        var start=ResourceWorkerStart("results with spaces");Assert.False(start.UseShellExecute);Assert.True(start.CreateNoWindow);Assert.Equal("1",start.Environment[ResourceWorker]);Assert.Equal(Environment.ProcessId.ToString(),start.Environment["SNAPBERRY_RENDER_PARENT_PID"]);Assert.Contains($"--TestCaseFilter:FullyQualifiedName={typeof(PerformanceTests).FullName}.{nameof(Repeated4KRenderReleasesBuffers)}",start.ArgumentList);
    }
    private static List<int> WarmUpResources(Action render,Func<int> sample)
    {
        var samples=new List<int>{sample()};var stable=0;
        for(var batch=0;batch<6;batch++)
        {
            for(var i=0;i<10;i++)render();
            var handles=sample();stable=handles==samples[^1]?stable+1:0;samples.Add(handles);
            if(batch>=2&&stable>=2)return samples;
        }
        Assert.Fail($"GDI resource initialization did not stabilize after 60 renders: {string.Join(", ",samples)}.");return samples;
    }
    private static void CollectAndDrain()
    {
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,()=>frame.Continue=false);Dispatcher.PushFrame(frame);
    }
    [Fact] public void ResourceWarmupWaitsForDelayedInitialization()
    {
        var counts=new Queue<int>([0,6,11,11,11]);var rendered=0;var samples=WarmUpResources(()=>rendered++,()=>counts.Dequeue());Assert.Equal(40,rendered);Assert.Equal(new[]{0,6,11,11,11},samples);
    }
    [Fact] public void ResourceWarmupRejectsContinuouslyGrowingHandles()
    {
        var rendered=0;Assert.ThrowsAny<Xunit.Sdk.XunitException>(()=>WarmUpResources(()=>rendered++,()=>rendered));Assert.Equal(60,rendered);
    }
    [Fact] public void ImageSelectionToEditorReadyCompletesWithinTarget()=>Sta.Run(()=>
    {
        var image=RenderingTests.Image(1920,1080);var values=new List<double>();
        for(var i=0;i<10;i++){var sw=Stopwatch.StartNew();var record=new CaptureRecord{Width=1920,Height=1080,OriginalPng=PngFileWriter.Encode(image)};var model=new EditorViewModel();model.Open(record);model.Composite();sw.Stop();values.Add(sw.Elapsed.TotalMilliseconds);}
        Assert.True(values.Max()<1000,$"Slowest editor preparation: {values.Max():F1}ms.");Write("editor-metrics.json",new{Iterations=10,Width=1920,Height=1080,Milliseconds=values,Maximum=values.Max(),Scope="Encoding selected image, loading editor model and first composite; excludes input selection and disk persistence."});
    });
    private static void Write(string name,object value)
    {var path=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");if(string.IsNullOrEmpty(path))return;Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,name),JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));}
}
