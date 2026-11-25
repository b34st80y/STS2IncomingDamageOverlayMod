import AppKit
import Foundation

let args = Array(CommandLine.arguments.dropFirst())
do {
  let reader = try CombatReader(resources: Bundle.main.resourceURL!)
  if args.first == "--image", args.count == 2 {
    let result = try reader.read(loadImage(URL(fileURLWithPath: args[1])))
    print(result.text)
    exit(result.incoming == nil ? 2 : 0)
  } else if args == ["--probe"] {
    _ = NSApplication.shared
    Task {
      do {
        let frame = try await capture(gameWindow())
        let result = try reader.read(frame)
        print(result.text)
        exit(result.incoming == nil ? 2 : 0)
      } catch {
        fputs("\(error.localizedDescription)\n", stderr)
        exit(1)
      }
    }
    RunLoop.main.run()
  } else {
    MainActor.assumeIsolated {
      let app = NSApplication.shared
      app.setActivationPolicy(.accessory)
      let overlay = Overlay(reader)
      app.delegate = overlay
      withExtendedLifetime(overlay) { app.run() }
    }
  }
} catch {
  fputs("Could not start STS2 Damage Overlay: \(error)\n", stderr)
  exit(1)
}
