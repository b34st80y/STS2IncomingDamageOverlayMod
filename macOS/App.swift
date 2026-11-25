import AppKit
import ScreenCaptureKit

// The whole HUD is a drag handle, including the text. AppKit can deliver a
// Control-click as a secondary click, depending on the input device.
private final class DragSurface: NSView {
  private var grabOffset: NSPoint?
  override func hitTest(_ point: NSPoint) -> NSView? { self }
  override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
  override func mouseDown(with event: NSEvent) { grabOffset = event.locationInWindow }
  override func mouseDragged(with event: NSEvent) {
    guard let window, !window.ignoresMouseEvents, let grabOffset else { return }
    let pointer = window.convertPoint(toScreen: event.locationInWindow)
    window.setFrameOrigin(NSPoint(x: pointer.x - grabOffset.x, y: pointer.y - grabOffset.y))
  }
  override func mouseUp(with event: NSEvent) { grabOffset = nil }
  override func rightMouseDown(with event: NSEvent) {
    if event.modifierFlags.contains(.control) { mouseDown(with: event) }
  }
  override func rightMouseDragged(with event: NSEvent) { mouseDragged(with: event) }
  override func rightMouseUp(with event: NSEvent) { mouseUp(with: event) }
}

@MainActor final class Overlay: NSObject, NSApplicationDelegate {
  private let reader: CombatReader
  private let panel = NSPanel(
    contentRect: NSRect(x: 0, y: 0, width: 300, height: 43),
    styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
  private let label = NSTextField(labelWithString: "")
  private var item: NSStatusItem!
  private let status = NSMenuItem(title: "Connecting…", action: nil, keyEquivalent: "")
  private var visible = true
  private var menuEditing = false
  private var controlHeld = false
  private var editing: Bool { visible && (menuEditing || controlHeld) }
  private var lastReading = Date.distantPast
  private var lastMessage = ""
  private var poll: Task<Void, Never>?

  init(_ reader: CombatReader) { self.reader = reader }

  func applicationDidFinishLaunching(_ notification: Notification) {
    item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    item.button?.image = NSImage(
      systemSymbolName: "shield.lefthalf.filled", accessibilityDescription: "STS2 Damage Overlay")
    let menu = NSMenu()
    menu.addItem(status)
    menu.addItem(.separator())
    menu.addItem(withTitle: "Hide overlay", action: #selector(toggle(_:)), keyEquivalent: "")
      .target = self
    menu.addItem(withTitle: "Move overlay", action: #selector(edit(_:)), keyEquivalent: "").target =
      self
    menu.addItem(
      withTitle: "Quit", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
    item.menu = menu
    panel.isReleasedWhenClosed = false
    panel.level = .statusBar
    panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .ignoresCycle]
    panel.backgroundColor = .clear
    panel.isOpaque = false
    panel.hidesOnDeactivate = false
    panel.ignoresMouseEvents = true
    let view = DragSurface()
    view.wantsLayer = true
    view.layer?.backgroundColor =
      NSColor(srgbRed: 0.065, green: 0.075, blue: 0.09, alpha: 0.92).cgColor
    view.layer?.cornerRadius = 9
    panel.contentView = view
    label.font = .monospacedDigitSystemFont(ofSize: 18, weight: .semibold)
    label.textColor = .white
    view.addSubview(label)
    if !panel.setFrameUsingName("STS2 Damage Panel") {
      let screen = NSScreen.main?.visibleFrame ?? .zero
      panel.setFrameOrigin(NSPoint(x: screen.minX + 32, y: screen.maxY - 140))
    }
    panel.setFrameAutosaveName("STS2 Damage Panel")
    // Reading the current modifier flags works while the game has focus.
    // Common modes keep the check active during mouse tracking.
    let modifiers = Timer(timeInterval: 1.0 / 30, repeats: true) { [weak self] _ in
      MainActor.assumeIsolated {
        self?.setControlHeld(NSEvent.modifierFlags.contains(.control))
      }
    }
    RunLoop.main.add(modifiers, forMode: .common)
    // A stalled capture must never leave a previous turn's number visible.
    Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in
      Task { @MainActor in
        guard let self, Date().timeIntervalSince(self.lastReading) > 4, !self.editing else {
          return
        }
        self.panel.orderOut(nil)
      }
    }
    poll = Task { await watch() }
  }

  func setControlHeld(_ held: Bool) {
    guard held != controlHeld else { return }
    if held && !panel.isVisible && NSWorkspace.shared.frontmostApplication?.bundleIdentifier != gameID {
      return
    }
    controlHeld = held
    refreshInteraction()
  }
  @objc private func toggle(_ sender: NSMenuItem) {
    visible.toggle()
    sender.title = visible ? "Hide overlay" : "Show overlay"
    refreshInteraction()
  }
  @objc private func edit(_ sender: NSMenuItem) {
    menuEditing.toggle()
    sender.title = menuEditing ? "Lock position" : "Move overlay"
    refreshInteraction()
  }
  private func refreshInteraction() {
    if !panel.ignoresMouseEvents && !editing {
      panel.saveFrame(usingName: "STS2 Damage Panel")
    }
    panel.ignoresMouseEvents = !editing
    if editing {
      draw(menuEditing ? "Drag to move · Lock in shield menu" : "Drag to move · Release Control to lock")
    } else if visible && Date().timeIntervalSince(lastReading) <= 4 {
      draw(lastMessage)
    } else {
      panel.orderOut(nil)
    }
  }
  private func draw(_ text: String) {
    label.stringValue = text
    label.sizeToFit()
    panel.setContentSize(NSSize(width: label.frame.width + 28, height: 42))
    label.setFrameOrigin(NSPoint(x: 14, y: (42 - label.frame.height) / 2))
    panel.orderFrontRegardless()
  }
  private func update(_ message: String, display: Bool = false) {
    if message != lastMessage {
      print(message)
      fflush(stdout)
      lastMessage = message
    }
    status.title = message
    item.button?.toolTip = message
    lastReading = display ? Date() : .distantPast
    if editing { return }
    if visible && display {
      draw(message)
    } else {
      panel.orderOut(nil)
    }
  }

  private func watch() async {
    var window: SCWindow?
    var discovery = Date.distantPast
    var requestedAccess = false
    while !Task.isCancelled {
      var delay = 0.45
      if NSRunningApplication.runningApplications(withBundleIdentifier: gameID).isEmpty {
        window = nil
        update("Waiting for Slay the Spire 2")
        delay = 2
      } else if !CGPreflightScreenCaptureAccess() {
        if !requestedAccess {
          requestedAccess = true
          _ = CGRequestScreenCaptureAccess()
        }
        update("Allow STS2 Damage Overlay in Screen Recording settings")
        delay = 2
      } else {
        do {
          if window == nil || Date().timeIntervalSince(discovery) > 5 {
            window = try await gameWindow()
            discovery = Date()
          }
          guard let target = window else { throw CaptureError.noWindow }
          let frame = try await capture(target)
          let reader = self.reader
          let reading = try await Task.detached(priority: .utility) { try reader.read(frame) }.value
          label.textColor = reading.text.hasSuffix("LETHAL") ? .systemOrange : .white
          update(reading.text, display: reading.incoming != nil)
        } catch {
          window = nil
          update(error.localizedDescription)
          delay = 1.5
        }
      }
      try? await Task.sleep(for: .seconds(delay))
    }
  }
}
