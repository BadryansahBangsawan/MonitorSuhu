import AppKit
import Foundation

enum UpdateInstaller {
    /// Copy the DMG's app aside, then replace `destinationApp` after this process exits.
    static func apply(dmg: URL) throws {
        guard dmg.isFileURL,
              ReleaseAssets.matches(dmg.lastPathComponent, platform: "macos")
        else {
            throw error("Update file is not a MonitorSuhu disk image.")
        }

        let stagingApp = FileManager.default.temporaryDirectory
            .appendingPathComponent("MonitorSuhu-\(UUID().uuidString).app", isDirectory: true)
        try extract(dmg: dmg, to: stagingApp)
        try startReplaceHelper(from: stagingApp, to: destinationApp())
        NSApp.terminate(nil)
    }

    static func destinationApp() -> URL {
        let running = Bundle.main.bundleURL
        if running.path.contains("/Volumes/") {
            return URL(fileURLWithPath: "/Applications/MonitorSuhu.app")
        }
        if running.lastPathComponent == "MonitorSuhu.app" {
            return running
        }
        return URL(fileURLWithPath: "/Applications/MonitorSuhu.app")
    }

    private static func extract(dmg: URL, to stagingApp: URL) throws {
        let mountRoot = FileManager.default.temporaryDirectory
            .appendingPathComponent("MonitorSuhu-mnt-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: mountRoot, withIntermediateDirectories: true)
        var attached = false
        defer {
            if attached {
                _ = try? run("/usr/bin/hdiutil", ["detach", mountRoot.path, "-force"])
            }
            try? FileManager.default.removeItem(at: mountRoot)
        }

        _ = try run("/usr/bin/hdiutil", [
            "attach", "-nobrowse", "-readonly", "-mountroot", mountRoot.path, dmg.path
        ])
        attached = true

        guard let app = findApp(under: mountRoot) else {
            throw error("The disk image does not contain MonitorSuhu.app.")
        }
        if FileManager.default.fileExists(atPath: stagingApp.path) {
            try FileManager.default.removeItem(at: stagingApp)
        }
        _ = try run("/usr/bin/ditto", [app.path, stagingApp.path])
    }

    private static func startReplaceHelper(from stagingApp: URL, to dest: URL) throws {
        try FileManager.default.createDirectory(
            at: dest.deletingLastPathComponent(),
            withIntermediateDirectories: true
        )
        let pid = ProcessInfo.processInfo.processIdentifier
        let script = FileManager.default.temporaryDirectory
            .appendingPathComponent("MonitorSuhu-replace-\(pid).sh")
        let body = """
        #!/bin/bash
        set -euo pipefail
        while /bin/kill -0 \(pid) 2>/dev/null; do
          /bin/sleep 0.2
        done
        /usr/bin/ditto \(shQuote(stagingApp.path)) \(shQuote(dest.path))
        /usr/bin/xattr -cr \(shQuote(dest.path))
        /usr/bin/open \(shQuote(dest.path))
        /bin/rm -rf \(shQuote(stagingApp.path)) \(shQuote(script.path))
        """
        try body.write(to: script, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.posixPermissions: 0o700], ofItemAtPath: script.path)

        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/bash")
        process.arguments = [script.path]
        process.standardOutput = FileHandle.nullDevice
        process.standardError = FileHandle.nullDevice
        try process.run()
    }

    private static func findApp(under root: URL) -> URL? {
        let fm = FileManager.default
        guard let enumerator = fm.enumerator(
            at: root,
            includingPropertiesForKeys: [.isDirectoryKey],
            options: [.skipsHiddenFiles]
        ) else { return nil }
        for case let url as URL in enumerator {
            if url.lastPathComponent == "MonitorSuhu.app" {
                enumerator.skipDescendants()
                return url
            }
        }
        return nil
    }

    private static func shQuote(_ value: String) -> String {
        "'" + value.replacingOccurrences(of: "'", with: "'\\''") + "'"
    }

    private static func run(_ launchPath: String, _ args: [String]) throws -> String {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: launchPath)
        process.arguments = args
        let stdout = Pipe()
        let stderr = Pipe()
        process.standardOutput = stdout
        process.standardError = stderr
        try process.run()
        process.waitUntilExit()
        let err = String(data: stderr.fileHandleForReading.readDataToEndOfFile(), encoding: .utf8) ?? ""
        if process.terminationStatus != 0 {
            let detail = err.trimmingCharacters(in: .whitespacesAndNewlines)
            throw error(detail.isEmpty ? "\(launchPath) failed (\(process.terminationStatus))." : detail)
        }
        return String(data: stdout.fileHandleForReading.readDataToEndOfFile(), encoding: .utf8) ?? ""
    }

    private static func error(_ message: String) -> NSError {
        NSError(domain: "MonitorSuhu", code: 1, userInfo: [NSLocalizedDescriptionKey: message])
    }
}
