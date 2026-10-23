// Real AppKit/WindowServer UI evidence; only for disposable GitHub macOS runners.
import AppKit
import ApplicationServices
import Foundation

struct UiError: Error, CustomStringConvertible { let description: String }
func require(_ condition: Bool, _ message: String) throws { if !condition { throw UiError(description: message) } }
func wait(_ seconds: Double, _ predicate: () -> Bool) -> Bool {
    let end = Date().addingTimeInterval(seconds)
    repeat {
        if predicate() { return true }
        _ = RunLoop.current.run(mode: .default, before: Date().addingTimeInterval(0.05))
    } while Date() < end
    return predicate()
}
func pause(_ seconds: Double) { _ = wait(seconds) { false } }
func attribute(_ element: AXUIElement, _ name: String) -> CFTypeRef? {
    var value: CFTypeRef?
    return AXUIElementCopyAttributeValue(element, name as CFString, &value) == .success ? value : nil
}
func nodes(_ root: AXUIElement, _ depth: Int = 0) -> [AXUIElement] {
    if depth > 30 { return [] }
    return [root] + (attribute(root, kAXChildrenAttribute) as? [AXUIElement] ?? []).flatMap { nodes($0, depth + 1) }
}
func name(_ element: AXUIElement) -> String {
    for key in [kAXTitleAttribute, kAXDescriptionAttribute, kAXHelpAttribute] {
        if let text = attribute(element, key) as? String, !text.isEmpty { return text }
    }
    return ""
}
func rect(_ element: AXUIElement) -> CGRect? {
    guard let point = attribute(element, kAXPositionAttribute), let size = attribute(element, kAXSizeAttribute),
          CFGetTypeID(point) == AXValueGetTypeID(), CFGetTypeID(size) == AXValueGetTypeID() else { return nil }
    var p = CGPoint.zero, s = CGSize.zero
    guard AXValueGetValue(point as! AXValue, .cgPoint, &p), AXValueGetValue(size as! AXValue, .cgSize, &s) else { return nil }
    return CGRect(origin: p, size: s)
}
func tree(_ root: AXUIElement) -> [[String: Any]] {
    nodes(root).map { node in
        var item: [String: Any] = ["role": attribute(node, kAXRoleAttribute) as? String ?? "", "name": name(node),
                                   "enabled": attribute(node, kAXEnabledAttribute) as? Bool ?? true]
        if let value = attribute(node, kAXValueAttribute) as? String { item["value"] = value }
        if let box = rect(node) { item["frame"] = ["x":box.minX, "y":box.minY, "width":box.width, "height":box.height] }
        return item
    }
}
func write(_ value: Any, _ url: URL) throws {
    try JSONSerialization.data(withJSONObject: value, options: [.prettyPrinted, .sortedKeys]).write(to: url, options: .atomic)
}
func button(_ root: AXUIElement, _ title: String) -> AXUIElement? {
    nodes(root).first { attribute($0, kAXRoleAttribute) as? String == kAXButtonRole && name($0) == title
        && (attribute($0, kAXEnabledAttribute) as? Bool ?? true) }
}
func press(_ root: AXUIElement, _ title: String) throws {
    var target: AXUIElement?
    try require(wait(12) { target = button(root, title); return target != nil }, "No enabled native button: \(title)")
    try require(AXUIElementPerformAction(target!, kAXPressAction as CFString) == .success, "Cannot press \(title)")
    pause(0.5)
}
func containsButtons(_ root: AXUIElement, _ titles: [String], withinWindow: Bool = false) throws {
    let all = nodes(root)
    for title in titles {
        guard let element = all.first(where: { attribute($0, kAXRoleAttribute) as? String == kAXButtonRole && name($0) == title }) else {
            throw UiError(description: "Missing native control: \(title)")
        }
        if withinWindow {
            guard let window = rect(root), let box = rect(element) else { throw UiError(description:"Missing native bounds for \(title)") }
            try require(box.width > 0 && box.height > 0 && window.insetBy(dx: -2, dy: -2).contains(box), "Control clipped outside window: \(title) \(box)")
        }
    }
}
func run(_ path: String, _ args: [String]) throws {
    let process = Process(); process.executableURL = URL(fileURLWithPath: path); process.arguments = args
    try process.run(); process.waitUntilExit()
    try require(process.terminationStatus == 0, "Command failed: \(path)")
}
func sampleImage() -> NSImage {
    let image = NSImage(size: NSSize(width: 720, height: 400)); image.lockFocus()
    NSColor(calibratedRed: 0.95, green: 0.95, blue: 0.98, alpha: 1).setFill(); NSRect(x:0,y:0,width:720,height:400).fill()
    NSColor.white.setFill(); NSBezierPath(roundedRect: NSRect(x:32,y:32,width:656,height:336), xRadius:16, yRadius:16).fill()
    ("SnapBerry · macOS" as NSString).draw(at: NSPoint(x:64,y:306), withAttributes: [.font:NSFont.systemFont(ofSize:30, weight:.semibold), .foregroundColor:NSColor.labelColor])
    ("Capture. Annotate. Share." as NSString).draw(at: NSPoint(x:64,y:270), withAttributes: [.font:NSFont.systemFont(ofSize:17), .foregroundColor:NSColor.secondaryLabelColor])
    for (i, color) in [NSColor.systemPurple, .systemBlue, .systemTeal].enumerated() {
        color.withAlphaComponent(0.14).setFill(); NSBezierPath(roundedRect:NSRect(x:CGFloat(64+i*196),y:88,width:180,height:138),xRadius:12,yRadius:12).fill()
        color.setFill(); NSBezierPath(roundedRect:NSRect(x:CGFloat(84+i*196),y:184,width:100,height:10),xRadius:5,yRadius:5).fill()
        ("0\(i+1)" as NSString).draw(at:NSPoint(x:CGFloat(84+i*196),y:112),withAttributes:[.font:NSFont.monospacedDigitSystemFont(ofSize:32,weight:.medium),.foregroundColor:color])
    }
    image.unlockFocus(); return image
}

let args = CommandLine.arguments, environment = ProcessInfo.processInfo.environment
guard environment["GITHUB_ACTIONS"] == "true", environment["RUNNER_OS"] == "macOS" else {
    fputs("Native UI probe is restricted to GitHub macOS CI.\n",stderr); exit(1)
}
if args.count == 3 && args[1] == "--cleanup" {
    do {
        let url = URL(fileURLWithPath:args[2]).resolvingSymlinksInPath()
        try require(url.lastPathComponent == "SnapBerry.app" && url.deletingLastPathComponent().lastPathComponent.hasPrefix("snapberry-native-ui-"),"Cleanup path is not an owned UI fixture")
        let apps = NSRunningApplication.runningApplications(withBundleIdentifier:"com.ynsim.snapberry").filter { $0.bundleURL?.resolvingSymlinksInPath() == url }
        for app in apps where !app.isTerminated {
            _ = app.forceTerminate()
            try require(wait(15) { app.isTerminated }, "Owned UI test app did not terminate; retaining files")
        }
        exit(0)
    } catch { fputs("Owned app cleanup failed: \(error)\n",stderr); exit(1) }
}
guard args.count == 4,
      ["light", "dark"].contains(args[3]) else { fputs("Native UI probe is restricted to GitHub macOS CI.\n", stderr); exit(1) }
let appURL = URL(fileURLWithPath: args[1]).resolvingSymlinksInPath(), output = URL(fileURLWithPath:args[2]), theme = args[3]
var report: [String: Any] = ["success":false, "headless":false, "nativeWindowServer":true, "theme":theme,
    "sourceSha":environment["GITHUB_SHA"] ?? "", "os":ProcessInfo.processInfo.operatingSystemVersionString,
    "arch":environment["UI_ARCH"] ?? "", "permissionSettingsModified":false, "normalTermination":false]
var running: NSRunningApplication?
var screenshots: [String: [String: Any]] = [:]
func reportFile() -> URL { output.appendingPathComponent("native-ui-\(theme).json") }
do {
    try FileManager.default.createDirectory(at:output, withIntermediateDirectories:true)
    try write(report, reportFile())
    try require(Bundle(url:appURL)?.bundleIdentifier == "com.ynsim.snapberry", "Unexpected bundle identity")
    try require(NSRunningApplication.runningApplications(withBundleIdentifier:"com.ynsim.snapberry").isEmpty, "An app instance already exists")
    try require(AXIsProcessTrusted(), "Runner Accessibility permission unavailable")
    try require(CGPreflightScreenCaptureAccess(), "Runner Screen Recording permission unavailable")
    _ = NSApplication.shared.setActivationPolicy(.prohibited)
    let configuration = NSWorkspace.OpenConfiguration(); configuration.activates = true
    configuration.addsToRecentItems = false; configuration.createsNewApplicationInstance = true
    configuration.promptsUserIfNeeded = false; configuration.allowsRunningApplicationSubstitution = false
    var childEnvironment = environment
    childEnvironment["DOTNET_ROOT"] = "/nonexistent/snapberry-no-shared-dotnet"
    childEnvironment["DOTNET_MULTILEVEL_LOOKUP"] = "0"
    configuration.environment = childEnvironment
    var completed = false, launchError: Error?
    NSWorkspace.shared.openApplication(at:appURL, configuration:configuration) { app, error in running=app; launchError=error; completed=true }
    try require(wait(30) { completed }, "Launch Services timed out")
    if let error = launchError { throw error }
    guard let app = running else { throw UiError(description:"Launch Services did not return the app") }
    try require(app.bundleURL?.resolvingSymlinksInPath() == appURL, "Launched bundle path mismatch")
    defer { if !app.isTerminated { _ = app.forceTerminate(); _ = wait(5) { app.isTerminated } } }
    report["pid"] = app.processIdentifier; report["launchMode"] = "LaunchServices"
    let axApp = AXUIElementCreateApplication(app.processIdentifier)
    func window(_ title: String) throws -> AXUIElement {
        var result: AXUIElement?
        try require(wait(20) { result = (attribute(axApp,kAXWindowsAttribute) as? [AXUIElement] ?? []).first { name($0) == title }; return result != nil }, "No native window: \(title)")
        return result!
    }
    func screenshot(_ scene: String, _ title: String) throws {
        pause(0.8)
        let nativeWindows = CGWindowListCopyWindowInfo([.optionOnScreenOnly,.excludeDesktopElements],kCGNullWindowID) as? [[String:Any]] ?? []
        guard let info = nativeWindows.first(where: {
            ($0[kCGWindowOwnerPID as String] as? NSNumber)?.int32Value == app.processIdentifier
            && ($0[kCGWindowLayer as String] as? NSNumber)?.intValue == 0
            && ($0[kCGWindowName as String] as? String) == title
        }), let id = info[kCGWindowNumber as String] as? NSNumber else {
            throw UiError(description:"No WindowServer window titled '\(title)'")
        }
        let filename = "\(scene)-\(theme).png", file = output.appendingPathComponent(filename)
        try run("/usr/sbin/screencapture", ["-x","-o","-l",id.stringValue,file.path])
        guard let bitmap = NSBitmapImageRep(data:try Data(contentsOf:file)) else { throw UiError(description:"Invalid native PNG") }
        var colors = Set<Int>()
        for y in stride(from:0,to:bitmap.pixelsHigh,by:max(1,bitmap.pixelsHigh/120)) {
            for x in stride(from:0,to:bitmap.pixelsWide,by:max(1,bitmap.pixelsWide/160)) {
                if let c = bitmap.colorAt(x:x,y:y)?.usingColorSpace(.sRGB) {
                    colors.insert((Int(c.redComponent*255)<<16) | (Int(c.greenComponent*255)<<8) | Int(c.blueComponent*255))
                }
            }
        }
        try require(colors.count >= 24, "Blank or incomplete native screenshot: \(scene)")
        var item: [String:Any] = ["file":filename,"windowId":id,"width":bitmap.pixelsWide,"height":bitmap.pixelsHigh,"uniqueColors":colors.count]
        if scene == "main" {
            guard let color = bitmap.colorAt(x:bitmap.pixelsWide-60,y:bitmap.pixelsHigh/2)?.usingColorSpace(.sRGB) else { throw UiError(description:"Cannot read native canvas color") }
            let luminance = (color.redComponent + color.greenComponent + color.blueComponent)/3
            item["canvasLuminance"] = luminance
            try require(theme == "dark" ? luminance < 0.4 : luminance > 0.65, "Rendered canvas does not match requested \(theme) theme")
        }
        screenshots[scene] = item; report["screenshots"] = screenshots
        try write(tree(axApp), output.appendingPathComponent("\(scene)-\(theme)-accessibility.json"))
        try write(report, reportFile()); print("Native screenshot: \(filename), \(bitmap.pixelsWide)×\(bitmap.pixelsHigh), \(colors.count) colors")
    }
    let main = try window("SnapBerry")
    try require((rect(main)?.width ?? 0) >= 820, "Main native window is too narrow")
    try containsButtons(main,["이미지 열기","붙여넣기","영역 캡처","창 캡처","전체 화면","이미지 복사","PNG 저장","보기","설정","단축키 설정","정보"],withinWindow:true)
    try screenshot("main","SnapBerry")
    try press(main,"정보"); let about = try window("SnapBerry 정보")
    try containsButtons(about,["진단 정보 복사","저장 폴더 열기","닫기"],withinWindow:true)
    try screenshot("about","SnapBerry 정보"); try press(about,"닫기")
    try press(main,"단축키 설정"); let hotkeys = try window("전역 캡처 단축키")
    try containsButtons(hotkeys,["기본값","취소","저장"],withinWindow:true)
    let values = nodes(hotkeys).compactMap { attribute($0,kAXValueAttribute) as? String }
    try require(["Ctrl+Shift+1","Ctrl+Shift+2","Ctrl+Shift+3"].allSatisfy { values.contains($0) },"Hotkey fields are missing")
    try screenshot("hotkeys","전역 캡처 단축키"); try press(hotkeys,"취소")
    try press(main,"설정"); let settings = try window("설정")
    try containsButtons(settings,["취소","저장"],withinWindow:true)
    try screenshot("settings","설정"); try press(settings,"취소")
    NSPasteboard.general.clearContents(); try require(NSPasteboard.general.writeObjects([sampleImage()]),"Cannot prepare clipboard fixture")
    try press(main,"붙여넣기")
    try require(wait(15) { button(main,"PNG 저장") != nil },"Image paste did not enable editing")
    try press(main,"텍스트")
    let editorTitle = name(main)
    try screenshot("editor",editorTitle)
    var size = CGSize(width:820,height:560)
    try require(AXUIElementSetAttributeValue(main,kAXSizeAttribute as CFString,AXValueCreate(.cgSize,&size)!) == .success,"Cannot resize native main window")
    pause(0.5)
    guard let compact = rect(main) else { throw UiError(description:"Missing compact window geometry") }
    try require(compact.width >= 820 && compact.width <= 850 && compact.height >= 560 && compact.height <= 620,
                "Native compact window ignored the requested size: \(compact)")
    try screenshot("compact",name(main))
    try require(app.terminate(),"Application rejected normal termination")
    try require(wait(15) { app.isTerminated },"Application did not terminate normally")
    report["normalTermination"] = true; report["success"] = true
    try write(report, reportFile())
} catch {
    report["error"] = String(describing:error); report["screenshots"] = screenshots
    try? write(report,reportFile()); fputs("Native UI failed: \(error)\n",stderr); exit(1)
}
