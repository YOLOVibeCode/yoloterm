// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "SwiftTermSpike",
    platforms: [.macOS(.v14)],
    products: [
        .executable(name: "SwiftTermSpike", targets: ["SwiftTermSpike"]),
    ],
    dependencies: [
        .package(url: "https://github.com/migueldeicaza/SwiftTerm", from: "1.13.0"),
    ],
    targets: [
        .executableTarget(
            name: "SwiftTermSpike",
            dependencies: ["SwiftTerm"],
            path: "Sources"
        ),
    ]
)
