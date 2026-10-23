// Authorized first-use screen recording consent test, GitHub macOS runners only.
// Operates the real macOS UI. Only a disposable CI account's credentials are used.
// Never edits TCC, resets grants, or changes authentication policy.
import AppKit
import ApplicationServices
import Foundation

struct ConsentError: Error, CustomStringConvertible { let description: String }
func require(_ condition: Bool, _ message: String) throws {
    if !condition { throw ConsentError(description: message) }
}
func wait(_ seconds: Double, _ done: () -> Bool) -> Bool {
    let deadline = Date().addingTimeInterval(seconds)
    repeat {
        if done() { return true }
        _ = RunLoop.current.run(mode: .default, before: Date().addingTimeInterval(0.2))
    } while Date() < deadline
    return done()
}
func attr(_ element: AXUIElement, _ key: String) -> CFTypeRef? {
    var value: CFTypeRef?
    return AXUIElementCopyAttributeValue(element, key as CFString, &value) == .success ? value : nil
}
func tree(_ root: AXUIElement, _ depth: Int = 0) -> [AXUIElement] {
    if depth > 25 { return [] }
    return [root] + (attr(root, kAXChildrenAttribute) as? [AXUIElement] ?? []).flatMap { tree($0, depth+1) }
}
func label(_ element: AXUIElement) -> String {
    let keys = attr(element, kAXSubroleAttribute) as? String == "AXSecureTextField"
        ? [kAXTitleAttribute, kAXDescriptionAttribute] : [kAXTitleAttribute, kAXDescriptionAttribute, kAXValueAttribute]
    return keys.compactMap { attr(element, $0) as? String }.joined(separator: " ")
}
func text(_ root: AXUIElement) -> String { tree(root).map(label).joined(separator: "\n") }
func button(_ root: AXUIElement, titles: [String]) -> AXUIElement? {
    tree(root).first { element in
        let role = attr(element, kAXRoleAttribute) as? String ?? ""
        return role == kAXButtonRole && titles.contains(where: { title in
            let value = (attr(element, kAXTitleAttribute) as? String ?? attr(element, kAXDescriptionAttribute) as? String ?? "")
                .replacingOccurrences(of: "…", with: "").trimmingCharacters(in: .whitespacesAndNewlines)
            return value == title
        })
    }
}
func press(_ element: AXUIElement) throws {
    try require(AXUIElementPerformAction(element, kAXPressAction as CFString) == .success, "Accessibility press failed: \(label(element))")
}
func systemRoots() -> [(String, AXUIElement)] {
    NSWorkspace.shared.runningApplications.filter { app in
        let id = app.bundleIdentifier ?? ""
        return id == "com.ynsim.snapberry" || id.hasPrefix("com.apple.")
    }.map { ($0.bundleIdentifier ?? "", AXUIElementCreateApplication($0.processIdentifier)) }
}
func dialog() -> AXUIElement? {
    for (_, root) in systemRoots() {
        for window in attr(root, kAXWindowsAttribute) as? [AXUIElement] ?? [] {
            let contents = text(window).lowercased()
            if contents.contains("snapberry") && (contents.contains("record") || contents.contains("screen")),
               button(window, titles: ["Open System Settings", "Open System Preferences", "Allow", "Allow While Using the App"]) != nil {
                return window
            }
        }
    }
    return nil
}
func additionalCapturePrompt() -> AXUIElement? {
    for app in NSRunningApplication.runningApplications(withBundleIdentifier: "com.apple.UserNotificationCenter") {
        let root = AXUIElementCreateApplication(app.processIdentifier)
        for window in attr(root, kAXWindowsAttribute) as? [AXUIElement] ?? [] {
            let contents = text(window).lowercased()
            if contents.contains("snapberry") && contents.contains("system private window picker")
                && contents.contains("screen and audio"),
               let allow = button(window, titles: ["Allow"]), attr(allow, kAXEnabledAttribute) as? Bool == true {
                return window
            }
        }
    }
    return nil
}
let args = CommandLine.arguments, env = ProcessInfo.processInfo.environment
guard env["GITHUB_ACTIONS"] == "true", env["RUNNER_OS"] == "macOS", env["RUNNER_ENVIRONMENT"] == "github-hosted", args.count == 7,
      let pid = Int32(args[1]), let original = NSRunningApplication(processIdentifier: pid),
      original.bundleURL?.resolvingSymlinksInPath().path == "/Applications/SnapBerry.app",
      args[2] == "/Applications/SnapBerry.app" else {
    fputs("Consent test is restricted to the owned GitHub macOS installation.\n", stderr); exit(1)
}
let appURL = URL(fileURLWithPath: args[2], isDirectory: true)
let output = URL(fileURLWithPath: args[5], isDirectory: true)
let reportURL = output.appendingPathComponent("first-use-consent.json")
var report: [String: Any] = ["success": false, "pid": pid, "phase": "requesting",
    "tccDatabaseEdited": false, "permissionApprovalMethod": "real-macOS-UI", "launchMode": "LaunchServices"]
var reopened: NSRunningApplication?
func save() {
    try? JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted, .sortedKeys]).write(to: reportURL, options: .atomic)
}
func evidence(_ phase: String) {
    report["phase"] = phase; save()
    let roots = systemRoots().map { id, root -> [String: Any] in
        ["bundle": id, "elements": tree(root).map { element -> [String: String] in
            var item: [String: String] = [:]
            for key in [kAXRoleAttribute, kAXSubroleAttribute, kAXTitleAttribute, kAXDescriptionAttribute, kAXValueAttribute, kAXIdentifierAttribute] {
                if key == kAXValueAttribute && attr(element, kAXSubroleAttribute) as? String == "AXSecureTextField" {
                    item[key] = "<redacted>"; continue
                }
                if let value = attr(element, key) { item[key] = String(describing: value) }
            }
            return item
        }]
    }
    try? JSONSerialization.data(withJSONObject: roots, options: [.prettyPrinted, .sortedKeys])
        .write(to: output.appendingPathComponent("consent-\(phase)-accessibility.json"))
    let capture = Process()
    capture.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
    capture.arguments = ["-x", "-m", output.appendingPathComponent("consent-\(phase).png").path]
    do { try capture.run(); capture.waitUntilExit() } catch { print("Diagnostic capture failed: \(error)") }
}
func settingsApp() -> NSRunningApplication? {
    NSRunningApplication.runningApplications(withBundleIdentifier: "com.apple.systempreferences").first
}
func permissionToggle(_ root: AXUIElement) -> AXUIElement? {
    // Select the smallest container identifying SnapBerry and exactly one switch.
    let candidates = tree(root).compactMap { element -> (Int, AXUIElement)? in
        let children = tree(element)
        guard children.count < 40, children.contains(where: { label($0).contains("SnapBerry") }) else { return nil }
        let switches = children.filter { ["AXCheckBox", "AXSwitch"].contains(attr($0, kAXRoleAttribute) as? String ?? "") }
        return switches.count == 1 ? (children.count, switches[0]) : nil
    }
    return candidates.min { $0.0 < $1.0 }?.1
}
func authenticationForm(_ root: AXUIElement) -> (AXUIElement, AXUIElement, AXUIElement)? {
    let candidates = tree(root).compactMap { element -> (Int, AXUIElement, AXUIElement, AXUIElement)? in
        let nodes = tree(element)
        guard nodes.count < 100, let submit = button(element, titles: ["Modify Settings", "Unlock"]) else { return nil }
        let secure = nodes.filter { attr($0, kAXSubroleAttribute) as? String == "AXSecureTextField" }
        let users = nodes.filter { attr($0, kAXRoleAttribute) as? String == "AXTextField"
            && !["AXSecureTextField", "AXSearchField"].contains(attr($0, kAXSubroleAttribute) as? String ?? "") }
        return secure.count == 1 && users.count == 1 ? (nodes.count, users[0], secure[0], submit) : nil
    }
    guard let chosen = candidates.min(by: { $0.0 < $1.0 }) else { return nil }
    return (chosen.1, chosen.2, chosen.3)
}
func authenticateFixture(_ root: AXUIElement) throws -> Bool {
    guard let (usernameField, passwordField, submit) = authenticationForm(root) else { return false }
    let url = URL(fileURLWithPath: args[6])
    let metadata = try FileManager.default.attributesOfItem(atPath: url.path)
    try require((metadata[.posixPermissions] as? NSNumber)?.intValue == 0o600
                && (metadata[.ownerAccountID] as? NSNumber)?.uint32Value == getuid()
                && metadata[.type] as? FileAttributeType == .typeRegular, "Unsafe private credential fixture")
    let credentials = try JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: String] ?? [:]
    guard credentials["username"] == "sbconsentci", let password = credentials["password"], password.count >= 32 else {
        throw ConsentError(description: "Invalid disposable account fixture")
    }
    try require(AXUIElementSetAttributeValue(usernameField, kAXValueAttribute as CFString, "sbconsentci" as CFString) == .success,
                "Could not enter the temporary CI username")
    try require(AXUIElementSetAttributeValue(passwordField, kAXValueAttribute as CFString, password as CFString) == .success,
                "Could not enter the temporary CI account credential")
    try press(submit)
    try require(wait(20) { authenticationForm(root) == nil }, "macOS did not accept temporary CI account authentication")
    return true
}
do {
    save()
    try require(AXIsProcessTrusted(), "CI Accessibility permission unavailable")
    let axApp = AXUIElementCreateApplication(pid)
    var captureButton: AXUIElement?
    try require(wait(15) { captureButton = button(axApp, titles: ["전체 화면"]); return captureButton != nil }, "No capture button")
    try press(captureButton!)
    var prompt: AXUIElement?
    let observed = wait(30) { prompt = dialog(); return prompt != nil }
    evidence("initial-prompt")
    try require(observed, "No first-use macOS consent prompt; preapproved or unavailable environments do not pass")
    report["initialPromptObserved"] = true
    report["initialPromptText"] = text(prompt!)
    let library = URL(fileURLWithPath: args[4], isDirectory: true)
    let premature = (try? FileManager.default.contentsOfDirectory(at: library, includingPropertiesForKeys: nil))?.contains { $0.pathExtension == "qcapture" } ?? false
    try require(!premature, "An image was persisted before permission approval")
    report["noCaptureBeforeApproval"] = true
    let openSettings = button(prompt!, titles: ["Open System Settings", "Open System Preferences"])
    let approval = openSettings ?? button(prompt!, titles: ["Allow", "Allow While Using the App"])
    try require(approval != nil, "No supported screen recording consent button")
    try press(approval!)
    report["systemPromptActionPerformed"] = true
    if openSettings == nil {
        _ = NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture")!)
        report["directAllowClicked"] = true
    }
    try require(wait(30) { settingsApp() != nil }, "System Settings did not open")
    let settings = settingsApp()!
    try require(settings.bundleURL?.path.hasPrefix("/System/") == true, "Unexpected System Settings identity")
    _ = settings.activate(options: [.activateIgnoringOtherApps])
    let axSettings = AXUIElementCreateApplication(settings.processIdentifier)
    var toggle: AXUIElement?
    let found = wait(30) { toggle = permissionToggle(axSettings); return toggle != nil }
    evidence("settings-before-approval")
    try require(found, "SnapBerry screen recording switch not found")
    let state = (attr(toggle!, kAXValueAttribute) as? NSNumber)?.intValue
    report["switchBeforeApproval"] = state ?? -1
    try require(state == 0 || report["directAllowClicked"] as? Bool == true, "SnapBerry was already approved before the first-use test")
    if state == 0 { try press(toggle!); report["settingsSwitchPressed"] = true }
    // Authenticate normally with a newly provisioned, disposable CI fixture account.
    _ = wait(3) { false }
    evidence("approval-result")
    report["authenticationRequired"] = authenticationForm(axSettings) != nil
    if authenticationForm(axSettings) != nil {
        report["temporaryAccountAuthenticationCompleted"] = try authenticateFixture(axSettings)
        evidence("authenticated")
    }
    for (_, root) in systemRoots() {
        for window in attr(root, kAXWindowsAttribute) as? [AXUIElement] ?? [] where text(window).lowercased().contains("snapberry") {
            if let later = button(window, titles: ["Later", "Not Now"]) { try press(later) }
        }
    }
    try require(wait(15) { guard let next = permissionToggle(axSettings) else { return false }; return (attr(next, kAXValueAttribute) as? NSNumber)?.intValue == 1 }, "Screen recording switch did not become enabled")
    report["approvalUIConfirmed"] = true
    evidence("approved")
    try require(original.terminate(), "Original app rejected normal termination")
    try require(wait(20) { original.isTerminated }, "Original app did not terminate normally")
    report["originalNormalTermination"] = true
    let configuration = NSWorkspace.OpenConfiguration()
    configuration.activates = true; configuration.addsToRecentItems = false
    configuration.createsNewApplicationInstance = true; configuration.allowsRunningApplicationSubstitution = false
    configuration.promptsUserIfNeeded = false
    var childEnvironment = env
    for key in ["DOTNET_ROOT", "DOTNET_ROOT_ARM64", "DOTNET_ROOT_X64", "DOTNET_ROOT_X86"] { childEnvironment[key] = "/nonexistent/snapberry-no-shared-dotnet" }
    childEnvironment["PATH"] = "/usr/bin:/bin:/usr/sbin:/sbin"; childEnvironment["DOTNET_MULTILEVEL_LOOKUP"] = "0"
    configuration.environment = childEnvironment
    var complete = false, launchError: Error?
    NSWorkspace.shared.openApplication(at: appURL, configuration: configuration) { app, error in reopened = app; launchError = error; complete = true }
    try require(wait(30) { complete }, "Approved app relaunch timed out")
    if let error = launchError { throw error }
    guard let app = reopened else { throw ConsentError(description: "Relaunch returned no app") }
    try require(app.bundleURL?.resolvingSymlinksInPath() == appURL, "Relaunched app identity mismatch")
    report["pid"] = app.processIdentifier; report["relaunchedViaLaunchServices"] = true; save()
    try require(wait(20) { app.isFinishedLaunching }, "Approved app not ready")
    let feature = Process()
    feature.executableURL = URL(fileURLWithPath: args[3]); feature.arguments = [String(app.processIdentifier), args[4], args[5]]
    try feature.run()
    defer { if feature.isRunning { feature.terminate(); _ = wait(5) { !feature.isRunning } } }
    var additionalError: Error?
    try require(wait(360) {
        if !feature.isRunning { return true }
        if let prompt = additionalCapturePrompt(), let allow = button(prompt, titles: ["Allow"]) {
            do {
                report["additionalCaptureConsentText"] = text(prompt)
                evidence("additional-capture-request")
                try press(allow)
                report["additionalCaptureConsentAllowed"] = true; save()
                _ = wait(3) { additionalCapturePrompt() == nil }
            } catch { additionalError = error; feature.terminate(); return true }
        }
        return false
    }, "Approved-app feature tests timed out")
    if let error = additionalError { throw error }
    report["nativeFeaturesPassed"] = feature.terminationStatus == 0
    try require(app.terminate(), "Approved app rejected normal termination")
    try require(wait(20) { app.isTerminated }, "Approved app did not terminate normally")
    report["approvedAppNormalTermination"] = true
    try require(feature.terminationStatus == 0, "Approved app failed real capture/clipboard/global shortcut tests")
    report["success"] = true; report["phase"] = "completed"; save()
    print("First-use consent OK: real prompt, UI approval, Launch Services relaunch, real native features")
} catch {
    report["error"] = String(describing: error); evidence("failed")
    fputs("First-use consent failed: \(error)\n", stderr); exit(1)
}
