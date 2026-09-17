import AppKit
import Foundation
import Combine

final class UpdateChecker: ObservableObject {
    static let releasesPage = URL(string: "https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest")!
    private static let api = URL(string: "https://api.github.com/repos/BadryansahBangsawan/MonitorSuhu/releases?per_page=30")!

    let currentVersion: String

    @Published private(set) var latestVersion: String?
    @Published private(set) var latestURL: URL?
    @Published private(set) var latestAsset: ReleaseAsset?
    @Published private(set) var hasUpdate = false
    @Published private(set) var checking = false
    @Published private(set) var installing = false
    @Published private(set) var progress: Double = 0
    @Published private(set) var lastError: String?

    init(currentVersion: String = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0") {
        self.currentVersion = currentVersion
    }

    func check(userInitiated: Bool = false, completion: ((Bool) -> Void)? = nil) {
        guard !checking, !installing else { return }
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
                      let json = try? JSONSerialization.jsonObject(with: data)
                else {
                    self.lastError = "GitHub returned no release."
                    completion?(false)
                    if userInitiated { Self.alert("Could not check for updates.", "GitHub returned no release.") }
                    return
                }
                let blobs: [[String: Any]]
                if let many = json as? [[String: Any]] {
                    blobs = many
                } else if let one = json as? [String: Any] {
                    blobs = [one]
                } else {
                    blobs = []
                }
                let releases = blobs.compactMap(Self.parseRelease)
                guard let hit = ReleaseAssets.latest(in: releases, platform: "macos") else {
                    self.latestVersion = nil
                    self.latestURL = Self.releasesPage
                    self.latestAsset = nil
                    self.hasUpdate = false
                    self.lastError = "No macOS build on GitHub Releases."
                    completion?(true)
                    if userInitiated {
                        Self.alert("No macOS update.", "GitHub latest has no macOS disk image. Windows or Linux-only releases are ignored here.")
                    }
                    return
                }
                self.latestVersion = hit.tag
                self.latestURL = hit.url ?? Self.releasesPage
                self.latestAsset = hit.asset
                self.hasUpdate = Versioning.isNewer(hit.tag, than: self.currentVersion)
                completion?(true)
                if userInitiated {
                    if self.hasUpdate {
                        self.promptInstall(latest: hit.tag)
                    } else {
                        Self.alert("You’re up to date.", "MonitorSuhu \(self.currentVersion) is the latest macOS release.")
                    }
                }
            }
        }.resume()
    }

    func apply() {
        guard !installing else { return }
        guard let asset = latestAsset else {
            openDownloadPage()
            return
        }
        installing = true
        progress = 0
        lastError = nil
        let agent = "MonitorSuhu/\(currentVersion)"
        Task.detached { [weak self] in
            do {
                let destDir = FileManager.default.temporaryDirectory
                    .appendingPathComponent("MonitorSuhu-update", isDirectory: true)
                try FileManager.default.createDirectory(at: destDir, withIntermediateDirectories: true)
                let file = destDir.appendingPathComponent(asset.name)
                try await Self.download(asset, to: file, userAgent: agent) { value in
                    Task { @MainActor [weak self] in
                        self?.progress = value * 0.85
                    }
                }
                await MainActor.run { [weak self] in
                    self?.progress = 1
                }
                try UpdateInstaller.apply(dmg: file)
            } catch {
                let message = error.localizedDescription
                await MainActor.run { [weak self] in
                    self?.installing = false
                    self?.lastError = message
                    Self.alert("Could not install update.", message)
                }
            }
        }
    }

    func openDownloadPage() {
        NSWorkspace.shared.open(latestURL ?? Self.releasesPage)
    }

    static func isNewer(_ latest: String, than current: String) -> Bool {
        Versioning.isNewer(latest, than: current)
    }

    private func promptInstall(latest: String) {
        let alert = NSAlert()
        alert.messageText = "MonitorSuhu \(latest) is available"
        alert.informativeText = latestAsset == nil
            ? "Download it from GitHub Releases, then replace the app in Applications."
            : "Download and replace the app in place. Your settings stay where they are."
        alert.alertStyle = .informational
        alert.addButton(withTitle: latestAsset == nil ? "Download" : "Install")
        alert.addButton(withTitle: "Later")
        if alert.runModal() == .alertFirstButtonReturn {
            if latestAsset == nil {
                openDownloadPage()
            } else {
                apply()
            }
        }
    }

    private static func parseRelease(_ body: [String: Any]) -> GitHubRelease? {
        guard let tag = body["tag_name"] as? String else { return nil }
        let page = (body["html_url"] as? String).flatMap(URL.init(string:))
        let draft = body["draft"] as? Bool ?? false
        let pre = body["prerelease"] as? Bool ?? false
        let raw = body["assets"] as? [[String: Any]] ?? []
        let assets: [ReleaseAsset] = raw.compactMap { item in
            guard let name = item["name"] as? String,
                  let href = item["browser_download_url"] as? String,
                  let url = URL(string: href)
            else { return nil }
            let size = (item["size"] as? NSNumber)?.int64Value ?? 0
            return ReleaseAsset(name: name, url: url, size: size)
        }
        return GitHubRelease(tag: tag, htmlURL: page, assets: assets, draft: draft, prerelease: pre)
    }

    private static func download(
        _ asset: ReleaseAsset,
        to dest: URL,
        userAgent: String,
        progress: @escaping (Double) -> Void
    ) async throws {
        guard ReleaseAssets.isTrusted(asset.url, name: asset.name, platform: "macos") else {
            throw NSError(domain: "MonitorSuhu", code: 1, userInfo: [
                NSLocalizedDescriptionKey: "Update file is not from GitHub Releases."
            ])
        }
        if asset.size > ReleaseAssets.maxBytes {
            throw NSError(domain: "MonitorSuhu", code: 1, userInfo: [
                NSLocalizedDescriptionKey: "Update is larger than expected."
            ])
        }

        var request = URLRequest(url: asset.url, timeoutInterval: 300)
        request.setValue(userAgent, forHTTPHeaderField: "User-Agent")
        let (temp, response) = try await URLSession.shared.download(for: request)
        let http = response as? HTTPURLResponse
        guard http?.statusCode == 200 else {
            throw NSError(domain: "MonitorSuhu", code: http?.statusCode ?? 0, userInfo: [
                NSLocalizedDescriptionKey: "GitHub returned no file."
            ])
        }
        let length = http?.expectedContentLength ?? asset.size
        if length > ReleaseAssets.maxBytes {
            throw NSError(domain: "MonitorSuhu", code: 1, userInfo: [
                NSLocalizedDescriptionKey: "Update is larger than expected."
            ])
        }
        let written = (try? FileManager.default.attributesOfItem(atPath: temp.path)[.size] as? NSNumber)?.int64Value ?? 0
        if written > ReleaseAssets.maxBytes {
            throw NSError(domain: "MonitorSuhu", code: 1, userInfo: [
                NSLocalizedDescriptionKey: "Update is larger than expected."
            ])
        }
        if asset.size > 0 && written != asset.size {
            throw NSError(domain: "MonitorSuhu", code: 1, userInfo: [
                NSLocalizedDescriptionKey: "Download size did not match GitHub."
            ])
        }
        progress(1)
        if FileManager.default.fileExists(atPath: dest.path) {
            try FileManager.default.removeItem(at: dest)
        }
        try FileManager.default.moveItem(at: temp, to: dest)
    }

    private static func alert(_ title: String, _ message: String) {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        alert.alertStyle = .informational
        alert.addButton(withTitle: "OK")
        alert.runModal()
    }
}
