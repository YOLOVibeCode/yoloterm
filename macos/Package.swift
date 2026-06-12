// swift-tools-version: 6.0

import PackageDescription

let package = Package(
    name: "YOLOTermKit",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .library(
            name: "YOLOTermKit",
            targets: ["YOLOTermKit"]
        ),
        .executable(
            name: "YOLOTerm",
            targets: ["YOLOTerm"]
        ),
        .executable(
            name: "pty-probe",
            targets: ["pty-probe"]
        )
    ],
    dependencies: [
        .package(url: "https://github.com/migueldeicaza/SwiftTerm", exact: "1.13.0"),
        .package(url: "https://github.com/groue/GRDB.swift", from: "6.29.0")
    ],
    targets: [
        .target(
            name: "YOLOTermKit",
            dependencies: [
                .product(name: "SwiftTerm", package: "SwiftTerm"),
                .product(name: "GRDB", package: "GRDB.swift")
            ],
            path: "Sources/YOLOTermKit",
            swiftSettings: [
                .enableExperimentalFeature("StrictConcurrency")
            ]
        ),
        .executableTarget(
            name: "YOLOTerm",
            dependencies: [
                "YOLOTermKit",
                .product(name: "SwiftTerm", package: "SwiftTerm")
            ],
            path: "YOLOTermApp/Sources",
            swiftSettings: [
                .enableExperimentalFeature("StrictConcurrency")
            ]
        ),
        .executableTarget(
            name: "pty-probe",
            dependencies: [],
            path: "Sources/pty-probe",
            swiftSettings: [
                .enableExperimentalFeature("StrictConcurrency")
            ]
        ),
        .testTarget(
            name: "YOLOTermKitTests",
            dependencies: ["YOLOTermKit"],
            path: "Tests/YOLOTermKitTests",
            swiftSettings: [
                .enableExperimentalFeature("StrictConcurrency")
            ]
        )
    ],
    swiftLanguageModes: [.v6]
)
