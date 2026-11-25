import AppKit
import ScreenCaptureKit

let gameID = "com.megacrit.SlayTheSpire2"

enum CaptureError: LocalizedError {
  case noWindow
  var errorDescription: String? { "Waiting for the STS2 window" }
}

func gameWindow() async throws -> SCWindow {
  let content = try await SCShareableContent.excludingDesktopWindows(
    true, onScreenWindowsOnly: false)
  let games = content.windows.filter {
    $0.owningApplication?.bundleIdentifier == gameID && $0.windowLayer == 0 && $0.frame.width >= 400
      && $0.frame.height >= 300
  }
  guard
    let window = games.sorted(by: { a, b in
      if a.isOnScreen != b.isOnScreen { return a.isOnScreen }
      if (a.title == "Slay the Spire 2") != (b.title == "Slay the Spire 2") {
        return a.title == "Slay the Spire 2"
      }
      return a.frame.width * a.frame.height > b.frame.width * b.frame.height
    }).first
  else { throw CaptureError.noWindow }
  return window
}

func capture(_ window: SCWindow) async throws -> CGImage {
  let filter = SCContentFilter(desktopIndependentWindow: window)
  let config = SCStreamConfiguration()
  let frame = filter.contentRect
  guard frame.height > 0 else { throw CaptureError.noWindow }
  config.height = min(1896, Int(frame.height * CGFloat(filter.pointPixelScale)))
  config.width = max(1, Int(CGFloat(config.height) * frame.width / frame.height))
  config.showsCursor = false
  config.ignoreShadowsSingleWindow = true
  config.captureResolution = .best
  return try await SCScreenshotManager.captureImage(contentFilter: filter, configuration: config)
}
