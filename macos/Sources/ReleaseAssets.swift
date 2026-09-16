import Foundation

struct ReleaseAsset: Equatable {
    let name: String
    let url: URL
    let size: Int64
}

enum ReleaseAssets {
    static let maxBytes: Int64 = 200 * 1024 * 1024

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
}
