using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Text.Json;

// 앱을 실행하지 않고 실제 단일 EXE의 어셈블리와 저장 호환성을 검사한다.
var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var exe = File.ReadAllBytes(Path.Combine(root, "dist/portable-win-x64/SnapBerry.exe"));
byte[] signature = Convert.FromHexString("8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae");
var marker = exe.AsSpan().IndexOf(signature);
Require(marker >= 8, "단일 파일 번들 서명이 없습니다.");
using var stream = new MemoryStream(exe);
using var reader = new BinaryReader(stream);
stream.Position = BitConverter.ToInt64(exe, marker - 8);
Require(reader.ReadUInt32() == 6, "검증기가 지원하는 번들 형식은 v6입니다.");
reader.ReadUInt32();
var count = reader.ReadInt32();
reader.ReadString();
stream.Position += 40;
var assemblies = new Dictionary<string, byte[]>();
var bundleFiles = new HashSet<string>();
for (var i = 0; i < count; i++)
{
    var offset = reader.ReadInt64();
    var size = reader.ReadInt64();
    var compressed = reader.ReadInt64();
    reader.ReadByte();
    var name = reader.ReadString();
    bundleFiles.Add(name);
    if (name is not ("SnapBerry.dll" or "SnapBerry.Core.dll")) continue;
    Require(compressed == 0, "압축된 번들은 별도 검증이 필요합니다.");
    assemblies.Add(name, exe.AsSpan(checked((int)offset), checked((int)size)).ToArray());
}
foreach (var name in new[] { "SkiaSharp.dll", "libSkiaSharp.dll" })
    Require(bundleFiles.Contains(name), $"WebP 저장에 필요한 {name}이 단일 EXE에 없습니다.");
Console.WriteLine("WebP 관리·네이티브 라이브러리의 단일 EXE 포함 확인");
foreach (var name in new[] { "SnapBerry.dll", "SnapBerry.Core.dll" })
{
    Require(assemblies.ContainsKey(name), $"번들에 {name}이 없습니다.");
    var originalPath = Path.Combine(root, name == "SnapBerry.dll" ? "src/SnapBerry.App" : "src/SnapBerry.Core", "bin/Release", name == "SnapBerry.dll" ? "net10.0-windows/win-x64" : "net10.0", name);
    var original = Inspect(File.ReadAllBytes(originalPath));
    var bundled = Inspect(assemblies[name]);
    Require(original.PublicNames.SetEquals(bundled.PublicNames), $"{name}: 공개 API 이름이 변경되었습니다.");
    Require(original.NativeImports.SetEquals(bundled.NativeImports), $"{name}: 네이티브 진입점이 변경되었습니다.");
    Require(original.Resources.Count == bundled.Resources.Count && original.Resources.All(resource =>
        bundled.Resources.TryGetValue(resource.Key, out var data) && resource.Value.SequenceEqual(data)), $"{name}: BAML 또는 내장 자원이 변경되었습니다.");
    var removed = original.PrivateNames.Except(bundled.PrivateNames).Count();
    Require(removed > 0, $"{name}: 비공개 이름 난독화가 적용되지 않았습니다.");
    Require(!bundled.HasSymbols, $"{name}: 디버그 정보가 포함되었습니다.");
    Console.WriteLine($"{name}: 비공개 이름 {removed}개 변경, 공개 API 보존, 디버그 정보 없음");
}
var context = new AssemblyLoadContext("portable-verification", isCollectible: true);
var core = context.LoadFromStream(new MemoryStream(assemblies["SnapBerry.Core.dll"]));
context.Resolving += (_, name) => name.Name == "SnapBerry.Core" ? core : null;
var app = context.LoadFromStream(new MemoryStream(assemblies["SnapBerry.dll"]));
var formatter = core.GetType("SnapBerry.Core.StepLabelFormatter", true)!;
var mode = core.GetType("SnapBerry.Core.StepSequenceMode", true)!;
Require((string)formatter.GetMethod("Format")!.Invoke(null, [27L, Enum.ToObject(mode, 1)])! == "aa", "난독화 Core 실행 결과가 다릅니다.");
foreach (var (assembly, typeName, json) in new[] {
    (core, "SnapBerry.Core.Annotation", "{\"Text\":\"호환성\",\"X\":12,\"Style\":{\"Color\":\"#123456\"}}"),
    (app, "SnapBerry.App.Settings.AppSettings", "{\"RegionKey\":\"Ctrl+Alt+7\",\"HistoryPanelWidth\":321,\"LastSaveFormat\":2}"),
    (app, "SnapBerry.App.Library.CaptureRecord", "{\"Name\":\"업무 결과 Project\",\"Width\":640,\"Height\":480}"),
    (app, "SnapBerry.App.Library.CaptureRecord", "{\"Width\":640,\"Height\":480,\"ResizeSize\":{\"Width\":320,\"Height\":240},\"Annotations\":[{\"Text\":\"test\"}]}" ) })
{
    var type = assembly.GetType(typeName, true)!;
    var value = JsonSerializer.Deserialize(json, type)!;
    using var actual = JsonDocument.Parse(JsonSerializer.Serialize(value, type));
    using var expected = JsonDocument.Parse(json);
    Require(ContainsJson(actual.RootElement, expected.RootElement), $"{typeName}: JSON 호환성이 깨졌습니다.");
}
Console.WriteLine("번들 내부 Core 실행 및 기존 JSON 계약 왕복 검증 통과 (UI 실행 없음)");

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidDataException(message);
}

static bool ContainsJson(JsonElement actual, JsonElement expected) => expected.ValueKind switch
{
    JsonValueKind.Object => expected.EnumerateObject().All(p => actual.TryGetProperty(p.Name, out var value) && ContainsJson(value, p.Value)),
    JsonValueKind.Array => actual.GetArrayLength() == expected.GetArrayLength() && expected.EnumerateArray().Select((value, i) => ContainsJson(actual[i], value)).All(v => v),
    _ => actual.ToString() == expected.ToString()
};

static (HashSet<string> PublicNames, HashSet<string> PrivateNames, HashSet<string> NativeImports, Dictionary<string, byte[]> Resources, bool HasSymbols) Inspect(byte[] bytes)
{
    using var pe = new PEReader(new MemoryStream(bytes));
    var metadata = pe.GetMetadataReader();
    var publicNames = new HashSet<string>();
    var privateNames = new HashSet<string>();
    var nativeImports = new HashSet<string>();
    foreach (var handle in metadata.TypeDefinitions)
    {
        var type = metadata.GetTypeDefinition(handle);
        var name = metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);
        if ((type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public) publicNames.Add(name);
        foreach (var methodHandle in type.GetMethods())
        {
            var method = metadata.GetMethodDefinition(methodHandle);
            var methodName = name + "::" + metadata.GetString(method.Name);
            if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0)
            {
                var import = method.GetImport();
                nativeImports.Add(metadata.GetString(metadata.GetModuleReference(import.Module).Name) + "!" + metadata.GetString(import.Name));
            }
            if ((method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public && (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public) publicNames.Add(methodName);
            else if ((method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private) privateNames.Add(methodName);
        }
    }
    var directory = pe.PEHeaders.CorHeader!.ResourcesDirectory;
    // Cecil은 리소스 사이의 정렬 여백을 바꿀 수 있다. 이름과 실제 데이터는 바이트 단위로 보존되어야 한다.
    var resources = new Dictionary<string, byte[]>();
    foreach (var handle in metadata.ManifestResources)
    {
        var resource = metadata.GetManifestResource(handle);
        Require(resource.Implementation.IsNil, "외부 연결 리소스는 검증할 수 없습니다.");
        var offset = checked((int)resource.Offset);
        Require(offset >= 0 && (long)offset + sizeof(int) <= directory.Size, "리소스 길이 정보가 CLR 리소스 범위를 벗어났습니다.");
        var section = pe.GetSectionData(directory.RelativeVirtualAddress);
        var length = BitConverter.ToInt32(section.GetContent(offset, sizeof(int)).ToArray());
        Require(length >= 0 && (long)offset + sizeof(int) + length <= directory.Size, "리소스 데이터가 CLR 리소스 범위를 벗어났습니다.");
        resources.Add(metadata.GetString(resource.Name), section.GetContent(checked(offset + sizeof(int)), length).ToArray());
    }
    return (publicNames, privateNames, nativeImports, resources, pe.ReadDebugDirectory().Any(e => e.Type is DebugDirectoryEntryType.CodeView or DebugDirectoryEntryType.EmbeddedPortablePdb));
}
