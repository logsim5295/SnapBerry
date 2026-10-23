using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace SnapBerry.Desktop.Shell;

public interface ILoginStartupService
{
    bool IsEnabled {get;}
    void SetEnabled(bool enabled);
}

public sealed record LoginStartupProcessResult(int ExitCode,string Error,string Output="");

/// <summary>Installs only SnapBerry's per-user LaunchAgent. Changes apply at the next login.</summary>
public sealed class MacLoginStartupService : ILoginStartupService
{
    private const string Label="com.ynsim.snapberry";
    private readonly string homeDirectory,executablePath,plistPath,domain;
    private readonly Func<ProcessStartInfo,LoginStartupProcessResult> runner;
    [DllImport("libSystem.B.dylib")] private static extern uint getuid();

    public MacLoginStartupService()
    {
        if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("로그인 시 시작 설정은 macOS에서 사용할 수 있습니다.");
        homeDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        executablePath=Environment.ProcessPath??throw new IOException("앱 실행 경로를 찾을 수 없습니다.");
        plistPath=Path.Combine(homeDirectory,"Library","LaunchAgents",Label+".plist");domain="gui/"+getuid();runner=RunProcess;
    }
    public MacLoginStartupService(string homeDirectory,string executablePath,int userId,Func<ProcessStartInfo,LoginStartupProcessResult> runner)
    {
        if(!Path.IsPathFullyQualified(homeDirectory)||!Path.IsPathFullyQualified(executablePath))throw new ArgumentException("사용자 폴더와 앱 경로는 절대 경로여야 합니다.");
        if(userId<0)throw new ArgumentOutOfRangeException(nameof(userId));
        this.homeDirectory=Path.GetFullPath(homeDirectory);this.executablePath=executablePath;this.runner=runner??throw new ArgumentNullException(nameof(runner));
        plistPath=Path.Combine(this.homeDirectory,"Library","LaunchAgents",Label+".plist");domain="gui/"+userId.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    public bool IsEnabled
    {
        get
        {
            GuardPaths();
            if(!File.Exists(plistPath))return false;
            var values=ReadOwnedPlist();
            if(values is null||!values.TryGetValue("ProgramArguments",out var arguments)||arguments.Name!="array"||arguments.Elements().Count()!=1||arguments.Element("string")?.Value!=executablePath||!values.TryGetValue("RunAtLoad",out var atLoad)||atLoad.Name!="true")return false;
            var state=Run("print-disabled",domain);
            // launchctl's human-readable output is not a stable API: reject unknown formats.
            if(!Regex.IsMatch(state.Output,"\\Adisabled services\\s*=\\s*\\{\\s*(?:\"(?:\\\\.|[^\"\\\\])*\"\\s*=>\\s*(?:true|false)\\s*)*\\}\\s*\\z",RegexOptions.CultureInvariant))throw new IOException("macOS 로그인 시작 상태를 해석하지 못했습니다.");
            return !Regex.IsMatch(state.Output,"\""+Regex.Escape(Label)+"\"\\s*=>\\s*true\\b",RegexOptions.CultureInvariant);
        }
    }
    public void SetEnabled(bool enabled)
    {
        GuardPaths();
        var exists=File.Exists(plistPath);
        if(exists&&ReadOwnedPlist() is null)throw new IOException("다른 프로그램이 만든 로그인 시작 설정을 변경할 수 없습니다.");
        if(!enabled&&!exists)return;
        var previous=exists?File.ReadAllBytes(plistPath):null;
        if(enabled)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);GuardPaths();
            WriteAtomic(CreatePlist());
            try{Run("enable",domain+"/"+Label);}
            catch
            {
                if(previous is null)File.Delete(plistPath);else WriteAtomic(previous);
                throw;
            }
        }
        else
        {
            Run("disable",domain+"/"+Label);
            try{File.Delete(plistPath);}
            catch(Exception deleteError)
            {
                try{Run("enable",domain+"/"+Label);}
                catch(Exception rollbackError){throw new IOException("로그인 시작 설정을 삭제하지 못했고 이전 상태 복원도 실패했습니다.",new AggregateException(deleteError,rollbackError));}
                throw;
            }
        }
    }
    private byte[] CreatePlist()
    {
        var document=new XDocument(new XDeclaration("1.0","utf-8",null),new XElement("plist",new XAttribute("version","1.0"),new XElement("dict",
            new XElement("key","Label"),new XElement("string",Label),
            new XElement("key","ProgramArguments"),new XElement("array",new XElement("string",executablePath)),
            new XElement("key","RunAtLoad"),new XElement("true"),
            new XElement("key","SnapBerryManagedLoginItem"),new XElement("true"),
            new XElement("key","AssociatedBundleIdentifiers"),new XElement("array",new XElement("string",Label)))));
        using var stream=new MemoryStream();document.Save(stream);return stream.ToArray();
    }
    private Dictionary<string,XElement>? ReadOwnedPlist()
    {
        try
        {
            using var reader=XmlReader.Create(plistPath,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64*1024});
            var elements=XDocument.Load(reader).Element("plist")?.Element("dict")?.Elements().ToArray();
            if(elements is null||elements.Length%2!=0)return null;
            var values=new Dictionary<string,XElement>(StringComparer.Ordinal);
            for(var i=0;i<elements.Length;i+=2)if(elements[i].Name!="key"||!values.TryAdd(elements[i].Value,elements[i+1]))return null;
            return values.TryGetValue("Label",out var label)&&label.Name=="string"&&label.Value==Label&&values.TryGetValue("SnapBerryManagedLoginItem",out var owned)&&owned.Name=="true"?values:null;
        }
        catch(XmlException){return null;}
    }
    private void WriteAtomic(byte[] bytes)
    {
        var temporary=plistPath+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes);stream.Flush(true);}
            if(!OperatingSystem.IsWindows())File.SetUnixFileMode(temporary,UnixFileMode.UserRead|UnixFileMode.UserWrite);
            GuardPaths();File.Move(temporary,plistPath,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    private void GuardPaths()
    {
        foreach(var path in new[]{homeDirectory,Path.Combine(homeDirectory,"Library"),Path.GetDirectoryName(plistPath)!,plistPath})
        {
            try{if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("심볼릭 링크를 통한 로그인 시작 설정 변경은 허용되지 않습니다.");}
            catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
        }
    }
    private LoginStartupProcessResult Run(params string[] arguments)
    {
        var start=new ProcessStartInfo("/bin/launchctl"){UseShellExecute=false,RedirectStandardError=true,RedirectStandardOutput=true,CreateNoWindow=true};
        foreach(var argument in arguments)start.ArgumentList.Add(argument);
        var result=runner(start);
        if(result.ExitCode!=0)throw new IOException("로그인 시작 설정을 변경하거나 확인하지 못했습니다. "+result.Error.Trim());
        return result;
    }
    private static LoginStartupProcessResult RunProcess(ProcessStartInfo start)
    {
        if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("로그인 시작 설정은 macOS에서 사용할 수 있습니다.");
        using var process=Process.Start(start)??throw new IOException("macOS 로그인 시작 도구를 실행하지 못했습니다.");
        var error=process.StandardError.ReadToEndAsync();var output=process.StandardOutput.ReadToEndAsync();
        if(!process.WaitForExit(10000)){try{process.Kill();}catch(InvalidOperationException){}throw new IOException("로그인 시작 설정 도구의 응답 시간이 초과되었습니다.");}
        return new(process.ExitCode,error.GetAwaiter().GetResult(),output.GetAwaiter().GetResult());
    }
}
