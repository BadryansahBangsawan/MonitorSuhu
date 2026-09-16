import AppKit
import Foundation
import Combine

final class UpdateChecker: ObservableObject {
    static let releasesPage = URL(string: "https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest")!
    private static let api = URL(string: "https://api.github.com/repos/BadryansahBangsawan/MonitorSuhu/releases/latest")!

    let currentVersion: String

    @Published private(set) var latestVersion: String?
    @Published private(set) var latestURL: URL?
    @Published private(set) var hasUpdate = false
    @Published private(set) var checking = false
    @Published private(set) var lastError: String?

    init(currentVersion: String = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0") {
        self.currentVersion = currentVersion
    }

    func check(userInitiated: Bool = false, completion: ((Bool) -> Void)? = nil) {
        guard !checking else { return }
        checking = true
        lastError = nil

        var request = URLRequest(url: Self.api, timeoutInterval: 12)
        request.setValue("MonitorSuhu/\(currentVersion)", forHTTPHeaderField: "User-Agent")
        request.setValue("application/vnd.github+json", forHTTPHeaderField: "Accept")

        URLSession.shared.dataTask(with: request) { [weak self] data, response, error in
            DispatchQueue.main.async {
                guard let self else { return }
                self.checking = false
                if let error {
                    self.lastError = error.localizedDescription
                    completion?(false)
                    if userInitiated { Self.alert("Could not check for updates.", error.localizedDescription) }
                    return
                }
                let status = (response as? HTTPURLResponse)?.statusCode ?? 0
                guard status == 200, let data,
                      let body = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                      let tag = body["tag_name"] as? String
                else {
                    self.lastError = "GitHub returned no release."
                    completion?(false)
                    if userInitiated { Self.alert("Could not check for updates.", "GitHub returned no release.") }
                    return
                }
                let latest = tag.hasPrefix("v") ? String(tag.dropFirst()) : tag
                let page = (body["html_url"] as? String).flatMap(URL.init(string:)) ?? Self.releasesPage
                self.latestVersion = latest
                self.latestURL = page
                self.hasUpdate = Self.isNewer(latest, than: self.currentVersion)
                completion?(true)
                if userInitiated {
                    if self.hasUpdate {
                        Self.promptDownload(latest: latest, url: page)
                    } else {
                        Self.alert("You’re up to date.", "MonitorSuhu \(self.currentVersion) is the latest release.")
                    }
                }
            }
        }.resume()
    }

    func openDownloadPage() {
        NSWorkspace.shared.open(latestURL ?? Self.releasesPage)
    }

    static func isNewer(_ latest: String, than current: String) -> Bool {
        let a = parse(latest)
        let b = parse(current)
        let n = max(a.count, b.count)
        for i in 0..<n {
            let x = i < a.count ? a[i] : 0
            let y = i < b.count ? b[i] : 0
            if x != y { return x > y }
        }
        return false
    }

    private static func parse(_ version: String) -> [Int] {
        version.split(separator: ".").compactMap { Int($0.filter(\.isNumber)) }
    }

    private static func alert(_ title: String, _ message: String) {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        alert.alertStyle = .informational
        alert.addButton(withTitle: "OK")
        alert.runModal()
    }

    private static func promptDownload(latest: String, url: URL) {
        let alert = NSAlert()
        alert.messageText = "MonitorSuhu \(latest) is available"
        alert.informativeText = "Download it from GitHub Releases, then replace the app in Applications."
        alert.alertStyle = .informational
        alert.addButton(withTitle: "Download")
        alert.addButton(withTitle: "Later")
        if alert.runModal() == .alertFirstButtonReturn {
            NSWorkspace.shared.open(url)
        }
    }
}
