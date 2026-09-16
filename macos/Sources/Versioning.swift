import Foundation

enum Versioning {
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

    static func parse(_ version: String) -> [Int] {
        version.split(separator: ".").compactMap { Int($0.filter(\.isNumber)) }
    }
}
