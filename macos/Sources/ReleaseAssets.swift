import Foundation

struct ReleaseAsset: Equatable {
    let name: String
    let url: URL
    let size: Int64
}

struct GitHubRelease: Equatable {
    let tag: String
    let htmlURL: URL?
    let assets: [ReleaseAsset]
    let draft: Bool
    let prerelease: Bool
}

enum ReleaseAssets {
    static let maxBytes: Int64 = 200 * 1024 * 1024

    /// In-app updates always replace `/Applications/MonitorSuhu.app`.
    /// Repo `build/` copies and mounted DMGs must not be the install target —
    /// that left GitHub 1.0.5 "installed" only in the source tree.
    static func installDestination(runningPath: String) -> String {
        let path = URL(fileURLWithPath: runningPath).standardizedFileURL.path
        if path == "/Applications/MonitorSuhu.app" || path.hasPrefix("/Applications/MonitorSuhu.app/") {
            return "/Applications/MonitorSuhu.app"
        }
        if path.hasPrefix("/Applications/"), path.hasSuffix("/MonitorSuhu.app") {
            return path
        }
        return "/Applications/MonitorSuhu.app"
    }

    static func pick(_ assets: [ReleaseAsset], platform: String = "macos") -> ReleaseAsset? {
        assets.first { matches($0.name, platform: platform) && isTrusted($0.url, name: $0.name, platform: platform) }
    }

    static func matches(_ name: String, platform: String) -> Bool {
        let n = name.lowercased()
        guard n.contains("monitorsuhu") else { return false }
        switch platform {
        case "macos": return n.contains("macos") && n.hasSuffix(".dmg")
        case "windows": return n.contains("windows") && n.hasSuffix(".exe")
        case "linux": return n.contains("linux") && (n.hasSuffix(".tar.gz") || n.hasSuffix(".zip"))
        default: return false
        }
    }

    static func isTrusted(_ url: URL, name: String, platform: String) -> Bool {
        guard matches(name, platform: platform) else { return false }
        if name.contains("/") || name.contains("\\") || name.contains("..") { return false }
        guard url.scheme?.lowercased() == "https" else { return false }
        switch url.host?.lowercased() {
        case "github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com":
            return true
        default:
            return false
        }
    }

    /// Newest non-draft release that actually ships a file for `platform`.
    /// A Windows-only tag must not be "the latest" on macOS.
    static func latest(in releases: [GitHubRelease], platform: String) -> (tag: String, url: URL?, asset: ReleaseAsset)? {
        for release in releases where !release.draft && !release.prerelease {
            guard let asset = pick(release.assets, platform: platform) else { continue }
            var tag = release.tag
            if tag.hasPrefix("v") { tag = String(tag.dropFirst()) }
            return (tag, release.htmlURL, asset)
        }
        return nil
    }

    /// `hdiutil attach -plist` may prefix checksum lines; take the XML payload.
    static func mountPoints(fromAttachPlist text: String) -> [String] {
        let xml: Substring
        if let start = text.range(of: "<?xml") {
            xml = text[start.lowerBound...]
        } else if let start = text.range(of: "<plist") {
            xml = text[start.lowerBound...]
        } else {
            xml = text[...]
        }
        let data = Data(xml.utf8)
        guard let obj = try? PropertyListSerialization.propertyList(from: data, options: [], format: nil) as? [String: Any],
              let entities = obj["system-entities"] as? [[String: Any]]
        else { return [] }
        return entities.compactMap { $0["mount-point"] as? String }.filter { !$0.isEmpty }
    }

    /// Do not use FileManager.enumerator — it skips mount points, so a
    /// freshly attached APFS DMG looks empty.
    static func findApp(under root: URL) -> URL? {
        let fm = FileManager.default
        if isAppBundle(root) { return root }
        var isDir: ObjCBool = false
        let direct = root.appendingPathComponent("MonitorSuhu.app")
        if isAppBundle(direct) { return direct }
        let kids = (try? fm.contentsOfDirectory(
            at: root,
            includingPropertiesForKeys: nil,
            options: [.skipsHiddenFiles]
        )) ?? []
        if let bundle = kids.first(where: { $0.lastPathComponent == "MonitorSuhu.app" && isAppBundle($0) }) {
            return bundle
        }
        for child in kids {
            let nested = child.appendingPathComponent("MonitorSuhu.app")
            if fm.fileExists(atPath: nested.path, isDirectory: &isDir), isDir.boolValue, isAppBundle(nested) {
                return nested
            }
        }
        return nil
    }

    static func isAppBundle(_ url: URL) -> Bool {
        url.lastPathComponent == "MonitorSuhu.app"
            && FileManager.default.fileExists(atPath: url.appendingPathComponent("Contents/Info.plist").path)
    }
}
