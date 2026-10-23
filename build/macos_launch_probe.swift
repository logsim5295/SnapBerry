// Native Launch Services + WindowServer smoke probe. GitHub macOS runners only.
// Optionally runs the separately authorized native feature probe before quitting.
import AppKit
import CoreGraphics
import Foundation

struct ProbeError: Error, CustomStringConvertible { let description: String }
func require(_ condition: Bool, _ message: String) throws {
    if !condition { throw ProbeError(description: message) }
}
func spin(_ seconds: TimeInterval, until done: () -> Bool) -> Bool {
    let deadline = Date().addingTimeInterval(seconds)
    repeat {
        if done() { return true }
        _ = RunLoop.current.run(mode: .default, before: Date().addingTimeInterval(0.05))
    } while Date() < deadline
    return done()
}
func visibleWindow(_ pid: pid_t) -> [String: Any]? {
    let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] ?? []
    for window in windows {
        guard (window[kCGWindowOwnerPID as String] as? NSNumber)?.int32Value == pid,
              (window[kCGWindowLayer as String] as? NSNumber)?.intValue == 0,
              let bounds = window[kCGWindowBounds as String] as? [String: Any],
              let width = bounds["Width"] as? NSNumber, let height = bounds["Height"] as? NSNumber,
              width.doubleValue >= 800, height.doubleValue >= 500 else { continue }
        return ["id": window[kCGWindowNumber as String] as? NSNumber ?? 0,
                "width": width, "height": height]
    }
    return nil
}
func sameBundle(_ app: NSRunningApplication, _ url: URL) -> Bool {
    app.bundleURL?.resolvingSymlinksInPath().path == url.resolvingSymlinksInPath().path
}
func save(_ report: [String: Any], to url: URL) throws {
    try JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted, .sortedKeys]).write(to: url, options: .atomic)
}

let args = CommandLine.arguments
let environment = ProcessInfo.processInfo.environment
guard environment["GITHUB_ACTIONS"] == "true", environment["RUNNER_OS"] == "macOS" else {
    fputs("GUI probe is restricted to GitHub Actions macOS runners.\n", stderr); exit(1)
}
if args.count == 4 && args[1] == "--cleanup" {
    do {
        let url = URL(fileURLWithPath: args[2], isDirectory: true)
        let reportURL = URL(fileURLWithPath: args[3])
        let report = FileManager.default.fileExists(atPath: reportURL.path)
            ? try JSONSerialization.jsonObject(with: Data(contentsOf: reportURL)) as? [String: Any] ?? [:] : [:]
        let pid = (report["pid"] as? NSNumber)?.int32Value
        var owned = NSRunningApplication.runningApplications(withBundleIdentifier: "com.ynsim.snapberry").filter { sameBundle($0, url) && !$0.isTerminated }
        if let pid = pid, let app = NSRunningApplication(processIdentifier: pid), !app.isTerminated {
            try require(sameBundle(app, url), "Cleanup PID does not belong to the installed test app")
            if !owned.contains(where: { $0.processIdentifier == pid }) { owned.append(app) }
        }
        // A timed-out launch may not have delivered its PID yet. Recover by the
        // unique installed bundle path; if ambiguous, retain files and fail closed.
        try require(pid != nil || !owned.isEmpty, "Cannot confirm ownership after incomplete launch; retaining installation")
        for app in owned {
            _ = app.forceTerminate()
            try require(spin(10) { app.isTerminated }, "Test app cleanup timed out")
        }
        exit(0)
    } catch { fputs("Cleanup failed: \(error)\n", stderr); exit(1) }
}
guard [3, 6, 8].contains(args.count) else { fputs("Usage: probe APP REPORT.json [FEATURE_PROBE LIBRARY OUTPUT [CONSENT_PROBE PRIVATE_CREDENTIALS]]\n", stderr); exit(1) }
let appURL = URL(fileURLWithPath: args[1], isDirectory: true)
let reportURL = URL(fileURLWithPath: args[2])
var report: [String: Any] = ["installedApp": appURL.path, "phase": "launching", "success": false,
                            "browserGatekeeperTested": false, "pixelCapturePerformed": false]
var running: NSRunningApplication?
do {
    try save(report, to: reportURL)
    let bundleID = Bundle(url: appURL)?.bundleIdentifier ?? ""
    try require(bundleID == "com.ynsim.snapberry", "Unexpected installed bundle identity")
    try require(NSRunningApplication.runningApplications(withBundleIdentifier: bundleID).isEmpty, "A SnapBerry instance already exists")
    _ = NSApplication.shared.setActivationPolicy(.prohibited)
    let configuration = NSWorkspace.OpenConfiguration()
    configuration.activates = true
    configuration.addsToRecentItems = false
    configuration.createsNewApplicationInstance = true
    configuration.allowsRunningApplicationSubstitution = false
    configuration.promptsUserIfNeeded = false // Fail on OS rejection; never approve or bypass it.
    var childEnvironment = environment
    for key in ["DOTNET_ROOT", "DOTNET_ROOT_ARM64", "DOTNET_ROOT_X64", "DOTNET_ROOT_X86"] {
        childEnvironment[key] = "/nonexistent/snapberry-no-shared-dotnet"
    }
    childEnvironment["PATH"] = "/usr/bin:/bin:/usr/sbin:/sbin"
    childEnvironment["DOTNET_MULTILEVEL_LOOKUP"] = "0"
    configuration.environment = childEnvironment
    var completed = false
    var launchError: Error?
    NSWorkspace.shared.openApplication(at: appURL, configuration: configuration) { app, error in
        running = app; launchError = error; completed = true
    }
    try require(spin(30) { completed }, "Launch Services did not finish within 30 seconds")
    if let error = launchError { throw error }
    guard let app = running else { throw ProbeError(description: "Launch Services returned no application") }
    try require(sameBundle(app, appURL), "Launched application is outside the installation directory")
    defer { if !app.isTerminated { _ = app.forceTerminate(); _ = spin(5) { app.isTerminated } } }
    report["pid"] = app.processIdentifier
    report["bundleId"] = bundleID
    report["executable"] = app.executableURL?.path ?? ""
    report["phase"] = "waiting-for-window"
    try save(report, to: reportURL)
    try require(spin(30) { app.isTerminated || (app.isFinishedLaunching && visibleWindow(app.processIdentifier) != nil) }, "No visible main window within 30 seconds")
    try require(!app.isTerminated, "Application terminated before its main window became ready")
    try require(visibleWindow(app.processIdentifier) != nil, "Application has no visible main window")
    report["window"] = visibleWindow(app.processIdentifier)
    report["finishedLaunching"] = app.isFinishedLaunching
    report["phase"] = "observing"
    try save(report, to: reportURL)
    let stoppedEarly = spin(5) { app.isTerminated }
    try require(!stoppedEarly && visibleWindow(app.processIdentifier) != nil, "Application exited or lost its main window during observation")
    report["stableSeconds"] = 5
    if args.count == 8 {
        report["phase"] = "first-use-consent"; try save(report, to: reportURL)
        let consent = Process()
        consent.executableURL = URL(fileURLWithPath: args[6])
        consent.arguments = [String(app.processIdentifier), appURL.path, args[3], args[4], args[5], args[7]]
        try consent.run()
        defer { if consent.isRunning { consent.terminate(); _ = spin(5) { !consent.isRunning } } }
        try require(spin(720) { !consent.isRunning }, "First-use consent probe timed out")
        if let data = try? Data(contentsOf: URL(fileURLWithPath: args[5]).appendingPathComponent("first-use-consent.json")),
           let result = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
            report["pid"] = result["pid"]
            report["firstUseScreenRecordingConsentTested"] = result["success"] as? Bool ?? false
            report["normalTermination"] = result["originalNormalTermination"] as? Bool ?? false
            report["featureNormalTermination"] = result["approvedAppNormalTermination"] as? Bool ?? false
            report["featureLaunchMode"] = "LaunchServices-after-real-consent"
            report["nativeFeaturesPassed"] = result["nativeFeaturesPassed"] as? Bool ?? false
        }
        try require(consent.terminationStatus == 0, "First-use permission test failed; see first-use-consent.json")
        report["success"] = true; report["phase"] = "completed"; report["pixelCapturePerformed"] = true
        try save(report, to: reportURL)
        print("GUI / first-use consent / native features OK through Launch Services")
        exit(0)
    }
    try require(app.terminate(), "Application rejected normal termination")
    try require(spin(15) { app.isTerminated }, "Application did not terminate normally within 15 seconds")
    report["normalTermination"] = true
    var featuresPassed = true
    if args.count == 6 {
        // Launch Services starts a new TCC responsibility chain. Native feature
        // tests instead inherit the hosted runner's existing privacy permissions,
        // just like the fixture client. No grants or security settings are edited.
        let child = Process()
        child.executableURL = appURL.appendingPathComponent("Contents/MacOS/SnapBerry.Desktop")
        child.environment = childEnvironment
        try child.run()
        defer { if child.isRunning { child.terminate(); _ = spin(5) { !child.isRunning } } }
        report["launchServicesPid"] = app.processIdentifier
        report["pid"] = child.processIdentifier
        report["featureLaunchMode"] = "runner-child-existing-permissions"
        report["firstUseScreenRecordingConsentTested"] = false
        try save(report, to: reportURL)
        try require(spin(30) { !child.isRunning || visibleWindow(child.processIdentifier) != nil }, "Feature app window did not appear")
        guard let featureApp = NSRunningApplication(processIdentifier: child.processIdentifier) else {
            throw ProbeError(description: "Feature app exited before registration")
        }
        try require(child.isRunning && sameBundle(featureApp, appURL), "Feature app identity mismatch")
        report["phase"] = "native-features"
        try save(report, to: reportURL)
        let features = Process()
        features.executableURL = URL(fileURLWithPath: args[3])
        features.arguments = [String(featureApp.processIdentifier), args[4], args[5]]
        try features.run()
        if !spin(360, until: { !features.isRunning }) {
            features.terminate()
            _ = spin(5) { !features.isRunning }
            throw ProbeError(description: "Native feature tests timed out")
        }
        featuresPassed = features.terminationStatus == 0
        if let data = try? Data(contentsOf: URL(fileURLWithPath: args[5]).appendingPathComponent("native-features.json")),
           let native = try? JSONSerialization.jsonObject(with: data) as? [String: Any] {
            report["pixelCapturePerformed"] = native["pixelCapturePerformed"] as? Bool ?? false
        }
        report["nativeFeaturesPassed"] = featuresPassed
        try require(featureApp.terminate(), "Feature app rejected normal termination")
        try require(spin(15) { !child.isRunning }, "Feature app did not terminate normally")
        report["featureNormalTermination"] = true
    }
    report["phase"] = "completed"
    report["success"] = featuresPassed
    try save(report, to: reportURL)
    try require(featuresPassed, "Native feature tests failed; see native-features.json")
    print("GUI launch OK: installed bundle opened through Launch Services, visible window, stable 5s, normal termination")
} catch {
    report["error"] = String(describing: error)
    try? save(report, to: reportURL)
    fputs("GUI launch failed: \(error)\n", stderr)
    exit(1)
}
