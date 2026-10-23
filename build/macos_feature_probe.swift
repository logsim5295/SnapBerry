// Real OS interoperability tests for the installed app. GitHub macOS runners only.
// Uses public Accessibility/CGEvent/NSPasteboard APIs. Never edits TCC or security settings.
import AppKit
import ApplicationServices
import Foundation

struct CheckError: Error, CustomStringConvertible { let description: String }
func check(_ value: Bool, _ message: String) throws {
    if !value { throw CheckError(description: message) }
}
func wait(_ seconds: Double, _ predicate: () -> Bool) -> Bool {
    let deadline = Date().addingTimeInterval(seconds)
    repeat {
        if predicate() { return true }
        while let event = NSApplication.shared.nextEvent(matching: .any, until: Date(), inMode: .default, dequeue: true) {
            NSApplication.shared.sendEvent(event)
        }
        _ = RunLoop.current.run(mode: .default, before: Date().addingTimeInterval(0.1))
    } while Date() < deadline
    return predicate()
}
func pause(_ seconds: Double) { _ = wait(seconds) { false } }
func command(_ path: String, _ args: [String]) throws -> Data {
    let task = Process(), pipe = Pipe()
    task.executableURL = URL(fileURLWithPath: path); task.arguments = args
    task.standardOutput = pipe; task.standardError = FileHandle.nullDevice
    try task.run()
    let data = pipe.fileHandleForReading.readDataToEndOfFile()
    task.waitUntilExit()
    try check(task.terminationStatus == 0, "Command failed: \(path) \(args)")
    return data
}
func attribute(_ element: AXUIElement, _ name: String) -> CFTypeRef? {
    var value: CFTypeRef?
    return AXUIElementCopyAttributeValue(element, name as CFString, &value) == .success ? value : nil
}
func elements(_ root: AXUIElement, depth: Int = 0) -> [AXUIElement] {
    if depth > 25 { return [] }
    let children = attribute(root, kAXChildrenAttribute) as? [AXUIElement] ?? []
    return [root] + children.flatMap { elements($0, depth: depth + 1) }
}
func keyboard(_ key: CGKeyCode, _ flags: CGEventFlags = []) throws {
    try check(CGPreflightPostEventAccess(), "OS input permission unavailable (PostEvent)")
    let modifiers: [(CGEventFlags, CGKeyCode)] = [(.maskControl, 59), (.maskShift, 56), (.maskAlternate, 58), (.maskCommand, 55)]
        .filter { flags.contains($0.0) }
    var held: CGEventFlags = []
    func modifier(_ code: CGKeyCode, down: Bool) {
        let event = CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: down)!
        event.type = .flagsChanged; event.flags = held; event.post(tap: .cghidEventTap); pause(0.05)
    }
    for (flag, code) in modifiers { held.insert(flag); modifier(code, down: true) }
    for down in [true, false] {
        guard let event = CGEvent(keyboardEventSource: nil, virtualKey: key, keyDown: down) else {
            throw CheckError(description: "Cannot create keyboard event")
        }
        event.flags = flags; event.post(tap: .cghidEventTap)
        pause(0.06)
    }
    // Release modifiers as a real user would before dragging/clicking the picker.
    // Holding Control makes macOS capture into the clipboard instead of the file.
    for (flag, code) in modifiers.reversed() { held.remove(flag); modifier(code, down: false) }
}
func mouse(_ type: CGEventType, _ point: CGPoint) {
    let event = CGEvent(mouseEventSource: nil, mouseType: type, mouseCursorPosition: point, mouseButton: .left)
    event?.flags = []; event?.post(tap: .cghidEventTap)
    pause(0.08)
}
let rgb: [[UInt8]] = [[255, 0, 255], [0, 255, 255], [255, 255, 0], [0, 255, 0]]
let colors = rgb.map { NSColor(srgbRed: Double($0[0])/255, green: Double($0[1])/255, blue: Double($0[2])/255, alpha: 1) }
final class PatternView: NSView {
    override func draw(_ dirty: NSRect) {
        for i in 0..<4 {
            colors[i].setFill()
            NSRect(x: CGFloat(i % 2) * bounds.width / 2, y: CGFloat(i / 2) * bounds.height / 2,
                   width: bounds.width / 2, height: bounds.height / 2).fill()
        }
    }
}
func hasPattern(_ image: NSBitmapImageRep) -> Bool {
    guard let pixels = rgba(image) else { return false }
    var found = [Int](repeating: 0, count: 4)
    for y in stride(from: 0, to: image.pixelsHigh, by: 4) {
        for x in stride(from: 0, to: image.pixelsWide, by: 4) {
            let offset = (y*image.pixelsWide+x)*4
            for (i, expected) in rgb.enumerated() {
                if (0..<3).allSatisfy({ abs(Int(pixels[offset+$0])-Int(expected[$0])) < 30 }) {
                    found[i] += 1
                }
            }
        }
    }
    let minimum = min(100, max(1, image.pixelsWide*image.pixelsHigh/64-1))
    return found.allSatisfy { $0 >= minimum }
}
func rgba(_ bitmap: NSBitmapImageRep) -> [UInt8]? {
    guard let image = bitmap.cgImage else { return nil }
    var pixels = [UInt8](repeating: 0, count: image.width*image.height*4)
    let valid = pixels.withUnsafeMutableBytes { bytes -> Bool in
        guard let context = CGContext(data: bytes.baseAddress, width: image.width, height: image.height,
            bitsPerComponent: 8, bytesPerRow: image.width*4, space: CGColorSpace(name: CGColorSpace.sRGB)!,
            bitmapInfo: CGBitmapInfo.byteOrder32Big.rawValue | CGImageAlphaInfo.premultipliedLast.rawValue) else { return false }
        context.draw(image, in: CGRect(x: 0, y: 0, width: image.width, height: image.height))
        return true
    }
    return valid ? pixels : nil
}
func fixture(width: Int = 96, height: Int = 64) -> NSBitmapImageRep {
    var pixels = [UInt8](repeating: 255, count: width*height*4)
    for y in 0..<height { for x in 0..<width {
        let color = rgb[(x < width/2 ? 0 : 1) + (y < height/2 ? 0 : 2)], offset = (y*width+x)*4
        for channel in 0..<3 { pixels[offset+channel] = color[channel] }
    } }
    let provider = CGDataProvider(data: Data(pixels) as CFData)!
    let image = CGImage(width: width, height: height, bitsPerComponent: 8, bitsPerPixel: 32, bytesPerRow: width*4,
        space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: CGBitmapInfo(rawValue: CGImageAlphaInfo.premultipliedLast.rawValue),
        provider: provider, decode: nil, shouldInterpolate: false, intent: .defaultIntent)!
    return NSBitmapImageRep(cgImage: image)
}
func sameFixture(_ image: NSBitmapImageRep, width: Int = 96, height: Int = 64) -> Bool {
    guard image.pixelsWide == width && image.pixelsHigh == height else { return false }
    guard let actual = rgba(image), let expected = rgba(fixture(width: width, height: height)) else { return false }
    for y in stride(from: 4, to: height, by: 8) { for x in stride(from: 4, to: width, by: 8) {
        let offset = (y*width+x)*4
        if !(0..<4).allSatisfy({ abs(Int(actual[offset+$0])-Int(expected[offset+$0])) < 8 }) { return false }
    } }
    return true
}

let args = CommandLine.arguments, env = ProcessInfo.processInfo.environment
guard env["GITHUB_ACTIONS"] == "true", env["RUNNER_OS"] == "macOS", args.count == 4,
      let pid = Int32(args[1]), let app = NSRunningApplication(processIdentifier: pid),
      app.bundleURL?.resolvingSymlinksInPath().path == "/Applications/SnapBerry.app" else {
    fputs("Native feature probe requires the owned GitHub macOS installation.\n", stderr); exit(1)
}
let library = URL(fileURLWithPath: args[2], isDirectory: true)
let output = URL(fileURLWithPath: args[3], isDirectory: true)
let axApp = AXUIElementCreateApplication(pid)
var cases: [String: [String: Any]] = [:]
var capturedPixels = false
let permissions: [String: Any] = ["accessibility": AXIsProcessTrusted(),
    "postEvent": CGPreflightPostEventAccess(), "probeScreenCapture": CGPreflightScreenCaptureAccess()]
func writeReport() {
    let report: [String: Any] = ["success": cases.count == 9 && cases.values.allSatisfy { $0["status"] as? String == "passed" },
        "pid": pid, "permissions": permissions, "cases": cases, "permissionSettingsModified": false,
        "pixelCapturePerformed": capturedPixels]
    try? JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted, .sortedKeys])
        .write(to: output.appendingPathComponent("native-features.json"), options: .atomic)
}
func test(_ name: String, _ body: () throws -> [String: Any]) {
    do { var result = try body(); result["status"] = "passed"; cases[name] = result; print("PASS \(name)") }
    catch { cases[name] = ["status": "failed", "error": String(describing: error)]; print("FAIL \(name): \(error)") }
    fflush(stdout); writeReport()
}
func press(_ title: String) throws {
    try check(AXIsProcessTrusted(), "Accessibility permission unavailable")
    var button: AXUIElement?
    try check(wait(10) {
        button = elements(axApp).first { element in
            let label = attribute(element, kAXTitleAttribute) as? String ?? attribute(element, kAXDescriptionAttribute) as? String ?? ""
            return attribute(element, kAXRoleAttribute) as? String == kAXButtonRole && label == title
                && (attribute(element, kAXEnabledAttribute) as? Bool ?? true)
        }
        return button != nil
    }, "No enabled Accessibility button: \(title)")
    try check(AXUIElementPerformAction(button!, kAXPressAction as CFString) == .success, "Cannot press \(title)")
    pause(0.3)
}
func archives() -> [URL] {
    (try? FileManager.default.contentsOfDirectory(at: library, includingPropertiesForKeys: nil))?.filter { $0.pathExtension == "qcapture" } ?? []
}
func manifest(_ archive: URL) throws -> [String: Any] {
    try JSONSerialization.jsonObject(with: command("/usr/bin/unzip", ["-p", archive.path, "document.json"])) as? [String: Any] ?? [:]
}
func annotationCount(_ archive: URL) -> Int {
    guard let record = try? manifest(archive), let annotations = record["Annotations"] as? [[String: Any]] else { return -1 }
    return annotations.count
}
func bitmap(_ archive: URL, _ entry: String = "original.png") throws -> NSBitmapImageRep {
    guard let bitmap = NSBitmapImageRep(data: try command("/usr/bin/unzip", ["-p", archive.path, entry])) else {
        throw CheckError(description: "Invalid PNG in \(entry)")
    }
    return bitmap
}
func newArchive(_ before: Set<String>) throws -> URL {
    var result: URL?
    try check(wait(20) {
        result = archives().first { !before.contains($0.path) && (try? manifest($0)) != nil }
        return result != nil
    }, "App did not persist a new capture within 20 seconds")
    return result!
}
func activateApp() throws {
    try check(wait(20) {
        if app.isActive { return true }
        _ = app.activate(options: [.activateIgnoringOtherApps])
        return app.isActive
    }, "SnapBerry did not activate")
}

_ = NSApplication.shared.setActivationPolicy(.regular)
NSApplication.shared.finishLaunching()
let marker = NSWindow(contentRect: NSRect(x: 80, y: 100, width: 480, height: 320),
    styleMask: [.titled], backing: .buffered, defer: false)
marker.title = "SnapBerry CI capture fixture"
marker.isReleasedWhenClosed = false
marker.contentView = PatternView(frame: NSRect(x: 0, y: 0, width: 480, height: 320))
marker.makeKeyAndOrderFront(nil)
marker.display()
pause(0.5)
do {
    let path = output.appendingPathComponent("fixture-screen.png")
    _ = try command("/usr/sbin/screencapture", ["-x", "-m", path.path])
    print("Fixture baseline visible: \(NSBitmapImageRep(data: try Data(contentsOf: path)).map(hasPattern) ?? false)")
} catch { print("Fixture baseline failed: \(error)") }
let pasteboard = NSPasteboard.general
pasteboard.clearContents()
pasteboard.setData(fixture().representation(using: .png, properties: [:])!, forType: .png)
var clipboardArchive: URL?
test("clipboard-paste") {
    try check(hasPattern(fixture()), "Native PNG fixture is missing its known RGB colors")
    try fixture().representation(using: .png, properties: [:])!.write(to: output.appendingPathComponent("clipboard-fixture.png"))
    try activateApp()
    let before = Set(archives().map(\.path))
    try press("붙여넣기")
    let archive = try newArchive(before)
    clipboardArchive = archive
    try check(try manifest(archive)["Mode"] as? Int == 4, "Paste did not create a Clipboard document")
    try check(sameFixture(try bitmap(archive)), "External PNG pixels/dimensions changed during paste")
    try FileManager.default.copyItem(at: archive, to: output.appendingPathComponent("clipboard.qcapture"))
    return ["nativePasteboardType": "public.png", "width": 96, "height": 64, "pixelsVerified": true]
}
test("clipboard-copy") {
    try activateApp()
    let previous = pasteboard.changeCount
    try press("이미지 복사")
    try check(wait(10) { pasteboard.changeCount != previous }, "App did not update OS pasteboard")
    var external: NSBitmapImageRep?
    for type in [NSPasteboard.PasteboardType.png, .tiff] {
        if let data = pasteboard.data(forType: type), let image = NSBitmapImageRep(data: data) {
            try data.write(to: output.appendingPathComponent(type == .png ? "clipboard-copy.png" : "clipboard-copy.tiff"))
            external = image; break
        }
    }
    try check(external.map { sameFixture($0) } == true, "External native client cannot read matching app bitmap")
    return ["pixelsVerified": true, "nativeTypes": (pasteboard.types ?? []).map(\.rawValue)]
}
test("clipboard-duplicate") {
    guard let archive = clipboardArchive else { throw CheckError(description: "Initial clipboard paste failed") }
    try activateApp()
    // A smaller overlay distinguishes selected-object copy from flattened-document copy.
    pasteboard.clearContents()
    pasteboard.setData(fixture(width: 48, height: 32).representation(using: .png, properties: [:])!, forType: .png)
    // Button focus keeps text fields out of the Command+C/V route.
    try press("붙여넣기")
    try check(wait(10) { annotationCount(archive) == 1 }, "First image object was not saved")
    let change = pasteboard.changeCount
    try keyboard(8, .maskCommand) // Command+C copies selected pasted image.
    try check(wait(10) { pasteboard.changeCount != change }, "Command+C did not copy the selected image")
    let copied = pasteboard.data(forType: .png) ?? pasteboard.data(forType: .tiff) ?? Data()
    try check(NSBitmapImageRep(data: copied).map { sameFixture($0, width: 48, height: 32) } == true,
              "Command+C copied the document instead of the selected 48x32 image")
    try keyboard(9, .maskCommand) // Command+V duplicates it on the current document.
    try check(wait(10) { annotationCount(archive) == 2 }, "Command+V did not duplicate the image object")
    let annotations = try manifest(archive)["Annotations"] as? [[String: Any]] ?? []
    for annotation in annotations {
        let hash = annotation["AssetHash"] as? String ?? ""
        try check(sameFixture(try bitmap(archive, "assets/\(hash).png"), width: 48, height: 32), "Duplicated asset pixels changed")
    }
    return ["savedImageObjects": annotations.count, "commandC": true, "commandV": true, "pixelsVerified": true]
}

func capture(_ mode: String, global: Bool) throws -> [String: Any] {
    try activateApp()
    let before = Set(archives().map(\.path))
    marker.orderFrontRegardless(); marker.displayIfNeeded()
    if global {
        NSApplication.shared.activate(ignoringOtherApps: true); marker.makeKeyAndOrderFront(nil)
        try check(wait(5) { !app.isActive && NSWorkspace.shared.frontmostApplication?.processIdentifier == getpid() }, "Fixture did not become foreground; global shortcut cannot be proven")
        try keyboard(mode == "display" ? 20 : mode == "region" ? 18 : 19, [.maskControl, .maskShift])
    } else { try press(mode == "display" ? "전체 화면" : mode == "region" ? "영역 캡처" : "창 캡처") }
    if mode != "display" {
        try check(wait(10) { (try? command("/usr/bin/pgrep", ["-x", "screencapture"])) != nil }, "OS interactive capture picker did not start")
        pause(1)
        let frame = marker.frame, screen = NSScreen.screens[0].frame
        let left = frame.minX + 25, top = screen.maxY - frame.maxY + 50
        let start = CGPoint(x: left, y: top), end = CGPoint(x: left + 420, y: top + 240)
        if mode == "region" {
            mouse(.mouseMoved, start); mouse(.leftMouseDown, start)
            for step in 1...12 { mouse(.leftMouseDragged, CGPoint(x: start.x+(end.x-start.x)*Double(step)/12, y: start.y+(end.y-start.y)*Double(step)/12)) }
            mouse(.leftMouseUp, end)
        } else {
            let center = CGPoint(x: (start.x+end.x)/2, y: (start.y+end.y)/2)
            mouse(.mouseMoved, center); pause(0.3); mouse(.leftMouseDown, center); mouse(.leftMouseUp, center)
        }
    }
    let archive = try newArchive(before)
    let evidence = output.appendingPathComponent("\(global ? "global" : "capture")-\(mode).qcapture")
    try FileManager.default.copyItem(at: archive, to: evidence)
    let expectedMode = mode == "display" ? 2 : mode == "region" ? 0 : 1
    try check(try manifest(archive)["Mode"] as? Int == expectedMode, "Wrong capture mode persisted")
    let image = try bitmap(archive)
    capturedPixels = true
    let scale = NSScreen.screens[0].backingScaleFactor
    if mode == "region" {
        try check(abs(Double(image.pixelsWide)-420*scale) <= 2 && abs(Double(image.pixelsHigh)-240*scale) <= 2,
                  "Region capture extent differs from the dragged rectangle")
    } else if mode == "window" {
        try check(Double(image.pixelsWide) >= marker.frame.width*scale && Double(image.pixelsWide) <= (marker.frame.width+180)*scale
                  && Double(image.pixelsHigh) >= 320*scale && Double(image.pixelsHigh) <= (marker.frame.height+180)*scale,
                  "Window capture extent differs from the fixture window and its shadow")
    } else {
        try check(Double(image.pixelsWide) == NSScreen.screens[0].frame.width*scale
                  && Double(image.pixelsHigh) == NSScreen.screens[0].frame.height*scale, "Display capture dimensions differ from primary display")
    }
    try check(hasPattern(image), "Capture does not contain all four real fixture colors")
    return ["width": image.pixelsWide, "height": image.pixelsHigh, "fixturePixelsVerified": true,
            "snapBerryWasInactive": global, "input": global ? "CGEvent Control+Shift+\(mode == "display" ? 3 : mode == "region" ? 1 : 2)" : "Accessibility button"]
}
for global in [false, true] {
    for mode in ["display", "region", "window"] {
        test("\(global ? "global" : "capture")-\(mode)") { try capture(mode, global: global) }
        // Release an OS picker after a failed assertion, before the next case.
        if cases["\(global ? "global" : "capture")-\(mode)"]?["status"] as? String != "passed" {
            try? keyboard(53); pause(1)
        }
    }
}
let tree = elements(axApp).map { element -> [String: String] in
    var item: [String: String] = [:]
    for key in [kAXRoleAttribute, kAXTitleAttribute, kAXDescriptionAttribute, kAXValueAttribute] {
        if let value = attribute(element, key) { item[key] = String(describing: value) }
    }
    return item
}
try? JSONSerialization.data(withJSONObject: tree, options: [.prettyPrinted, .sortedKeys]).write(to: output.appendingPathComponent("accessibility-tree.json"))
marker.close(); pasteboard.clearContents(); writeReport()
exit(cases.count == 9 && cases.values.allSatisfy { $0["status"] as? String == "passed" } ? 0 : 1)
