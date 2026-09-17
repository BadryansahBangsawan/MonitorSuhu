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

    static func destinationApp(running: URL = Bundle.main.bundleURL) -> URL {
        URL(fileURLWithPath: ReleaseAssets.installDestination(runningPath: running.path))
    }

    private static func extract(dmg: URL, to stagingApp: URL) throws {
        let mountRoot = FileManager.default.temporaryDirectory
            .appendingPathComponent("MonitorSuhu-mnt-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: mountRoot, withIntermediateDirectories: true)
        var attached = false
        var mountPoints: [String] = []
        defer {
            if attached {
                for point in mountPoints.reversed() {
                    _ = try? run("/usr/bin/hdiutil", ["detach", point, "-force"])
                }
                _ = try? run("/usr/bin/hdiutil", ["detach", mountRoot.path, "-force"])
            }
            try? FileManager.default.removeItem(at: mountRoot)
        }

        _ = try? run("/usr/bin/xattr", ["-cr", dmg.path])
        let plist = try run("/usr/bin/hdiutil", [
            "attach", "-plist", "-nobrowse", "-readonly", "-noverify",
            "-mountroot", mountRoot.path, dmg.path
        ])
        attached = true
        mountPoints = ReleaseAssets.mountPoints(fromAttachPlist: plist)

        var app: URL?
        for point in mountPoints {
            app = ReleaseAssets.findApp(under: URL(fileURLWithPath: point))
            if app != nil { break }
        }
        if app == nil {
            app = ReleaseAssets.findApp(under: mountRoot)
        }
        guard let app else {
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
        let destPath = dest.path
        let body = """
        #!/bin/bash
        set -euo pipefail
        trap '' HUP
        while /bin/kill -0 \(pid) 2>/dev/null; do
          /bin/sleep 0.2
        done
        /usr/bin/pkill -f '/Applications/MonitorSuhu.app/Contents/MacOS/MonitorSuhu' || true
        /bin/sleep 0.4
        /usr/bin/ditto \(shQuote(stagingApp.path)) \(shQuote(destPath))
        /usr/bin/xattr -cr \(shQuote(destPath))
        /usr/bin/open \(shQuote(destPath))
        /bin/rm -rf \(shQuote(stagingApp.path)) \(shQuote(script.path))
        """
        try body.write(to: script, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.posixPermissions: 0o700], ofItemAtPath: script.path)

        // nohup + HUP trap: NSApp.terminate() must not kill the waiter.
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/nohup")
        process.arguments = ["/bin/bash", script.path]
        process.standardInput = FileHandle.nullDevice
        process.standardOutput = FileHandle.nullDevice
        process.standardError = FileHandle.nullDevice
        try process.run()
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
