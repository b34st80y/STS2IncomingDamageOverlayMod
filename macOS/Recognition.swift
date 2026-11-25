import CoreGraphics
import CoreText
import Foundation
import ImageIO

enum ReaderError: Error { case invalidImage, missingReferences }
func loadImage(_ url: URL) throws -> CGImage {
  guard let source = CGImageSourceCreateWithURL(url as CFURL, nil),
    let image = CGImageSourceCreateImageAtIndex(source, 0, nil)
  else { throw ReaderError.invalidImage }
  return image
}

struct Pixel {
  let r, g, b, a: UInt8
  func distance(_ p: Pixel) -> Int {
    max(abs(Int(r) - Int(p.r)), abs(Int(g) - Int(p.g)), abs(Int(b) - Int(p.b)))
  }
  var isText: Bool {
    r > 212 && g > 202 && b > 177 && Int(r) - Int(g) < 24 && (5...50).contains(Int(r) - Int(b))
      && r >= g
  }
}

struct Raster {
  let width, height: Int
  var pixels: [Pixel]
  init(_ image: CGImage, height target: Int? = nil) throws {
    height = target ?? image.height
    width = Int((Double(image.width) * Double(height) / Double(image.height)).rounded())
    guard width > 0, width <= 5000, height > 0, height <= 3000 else {
      throw ReaderError.invalidImage
    }
    let clear = Pixel(r: 0, g: 0, b: 0, a: 0)
    pixels = Array(repeating: clear, count: width * height)
    let w = width
    let h = height
    try pixels.withUnsafeMutableBytes { bytes in
      guard
        let ctx = CGContext(
          data: bytes.baseAddress, width: w, height: h,
          bitsPerComponent: 8, bytesPerRow: w * 4,
          space: CGColorSpace(name: CGColorSpace.sRGB)!,
          bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
            | CGBitmapInfo.byteOrder32Big.rawValue)
      else { throw ReaderError.invalidImage }
      ctx.interpolationQuality = .high
      ctx.draw(image, in: CGRect(x: 0, y: 0, width: w, height: h))
    }
  }
  subscript(_ x: Int, _ y: Int) -> Pixel { pixels[y * width + x] }
}

// Use opaque, widely spaced points of the installed icon. Start with colorful
// points so nearly all background candidates fail after a single comparison.
struct Icon {
  struct Point {
    let x, y: Int
    let color: Pixel
  }
  let points: [Point]
  let radius: Int
  let scale: Double
  init(_ image: CGImage, scale: Double, shield: Bool) throws {
    self.scale = scale
    let raster = try Raster(image, height: Int(Double(image.height) * scale))
    radius = max(raster.width, raster.height) / 2 + 1
    var samples: [Point] = []
    for y in stride(from: 2, to: raster.height - 2, by: 3) {
      for x in stride(from: 2, to: raster.width - 2, by: 3) {
        let p = raster[x, y]
        guard p.a > 250, raster[x - 1, y].distance(p) < 30,
          raster[x + 1, y].distance(p) < 30, raster[x, y + 1].distance(p) < 30
        else { continue }
        let dx = x - raster.width / 2
        let dy = y - raster.height / 2
        if shield && abs(dx) < raster.width / 5 && abs(dy) < raster.height / 6 { continue }
        samples.append(Point(x: dx, y: dy, color: p))
      }
    }
    let vivid = samples.filter { max($0.color.r, $0.color.g, $0.color.b) > 130 }
    let dark = samples.filter { max($0.color.r, $0.color.g, $0.color.b) <= 130 }
    guard vivid.count >= 4 else { throw ReaderError.missingReferences }
    func spaced(_ items: [Point], _ n: Int) -> [Point] {
      let count = min(n, items.count)
      return (0..<count).map { items[$0 * items.count / count] }
    }
    points = spaced(vivid, 20) + spaced(dark, 10)
  }

  struct Hit {
    let x, y, score: Int
    let scale: Double
  }
  func hits(in image: Raster, xRange: Range<Int>, yRange: Range<Int>) -> [Hit] {
    let x0 = max(radius, xRange.lowerBound)
    let x1 = min(image.width - radius, xRange.upperBound)
    let y0 = max(radius, yRange.lowerBound)
    let y1 = min(image.height - radius, yRange.upperBound)
    guard x0 < x1, y0 < y1 else { return [] }
    let offsets = points.map { $0.y * image.width + $0.x }
    let first = points[0].color
    var found: [Hit] = []
    image.pixels.withUnsafeBufferPointer { buffer in
      let ptr = buffer.baseAddress!
      for y in y0..<y1 {
        for x in x0..<x1 {
          let i = y * image.width + x
          guard ptr[i + offsets[0]].distance(first) <= 54 else { continue }
          var bad = 0
          var total = 0
          for j in 1..<points.count {
            let error = ptr[i + offsets[j]].distance(points[j].color)
            if error > 54 { bad += 1 }
            total += min(error, 80)
            if bad > points.count / 6 { break }
          }
          if bad <= points.count / 6 && total / points.count < 28 {
            found.append(Hit(x: x, y: y, score: total / points.count, scale: scale))
          }
        }
      }
    }
    return found
  }
}

struct Glyph {
  let character: Character
  let path: CGPath
  let bounds: CGRect
  init(_ char: Character, font: CTFont) throws {
    character = char
    var u = String(char).utf16.first!
    var glyph: CGGlyph = 0
    guard CTFontGetGlyphsForCharacters(font, &u, &glyph, 1),
      let shape = CTFontCreatePathForGlyph(font, glyph, nil)
    else { throw ReaderError.missingReferences }
    path = shape
    bounds = shape.boundingBoxOfPath
  }
}

struct Blob {
  var x, y, right, bottom: Int
  var width: Int { right - x + 1 }
  var height: Int { bottom - y + 1 }
}

// Segment the cream-colored glyph fills. Connected components keep nearby
// particles and the red swords out of the text passed to shape matching.
func blobs(_ image: Raster, xRange: Range<Int>, yRange: Range<Int>) -> [Blob] {
  let x0 = max(0, xRange.lowerBound)
  let x1 = min(image.width, xRange.upperBound)
  let y0 = max(0, yRange.lowerBound)
  let y1 = min(image.height, yRange.upperBound)
  let w = x1 - x0
  let h = y1 - y0
  guard w > 0, h > 0 else { return [] }
  let stride = w + 2
  var mask = [Bool](repeating: false, count: stride * (h + 2))
  for y in 0..<h {
    for x in 0..<w where image[x + x0, y + y0].isText { mask[(y + 1) * stride + x + 1] = true }
  }
  var result: [Blob] = []
  var stack: [Int] = []
  let adj = [-1, 1, -stride, stride, -stride - 1, -stride + 1, stride - 1, stride + 1]
  for i in mask.indices where mask[i] {
    mask[i] = false
    stack.append(i)
    let x = i % stride + x0 - 1
    let y = i / stride + y0 - 1
    var box = Blob(x: x, y: y, right: x, bottom: y)
    while let j = stack.popLast() {
      let xx = j % stride + x0 - 1
      let yy = j / stride + y0 - 1
      box.x = min(box.x, xx)
      box.y = min(box.y, yy)
      box.right = max(box.right, xx)
      box.bottom = max(box.bottom, yy)
      for k in adj where mask[j + k] {
        mask[j + k] = false
        stack.append(j + k)
      }
    }
    if box.width >= 2 && box.height >= 5 { result.append(box) }
  }
  return result.sorted { $0.x < $1.x }
}

struct Shapes {
  struct Reference {
    let char: Character
    let aspect: Double
    let mask: [Bool]
  }
  let references: [Reference]
  static let side = 16
  init(fontURL: URL) throws {
    guard let provider = CGDataProvider(url: fontURL as CFURL), let cg = CGFont(provider) else {
      throw ReaderError.missingReferences
    }
    let font = CTFontCreateWithGraphicsFont(cg, 64, nil, nil)
    references = try "0123456789x/".map {
      let g = try Glyph($0, font: font)
      let b = g.bounds
      let mask = (0..<Self.side * Self.side).map { i in
        let nx = (Double(i % Self.side) + 0.5) / Double(Self.side)
        let ny = (Double(i / Self.side) + 0.5) / Double(Self.side)
        return g.path.contains(CGPoint(x: b.minX + b.width * nx, y: b.maxY - b.height * ny))
      }
      return Reference(char: $0, aspect: b.width / b.height, mask: mask)
    }
  }

  func recognize(_ box: Blob, in image: Raster) -> Character? {
    let mask = (0..<Self.side * Self.side).map { i in
      image[
        box.x + (2 * (i % Self.side) + 1) * box.width / (2 * Self.side),
        box.y + (2 * (i / Self.side) + 1) * box.height / (2 * Self.side)
      ].isText
    }
    let aspect = Double(box.width) / Double(box.height)
    let scores = references.map { ref in
      let diff = zip(mask, ref.mask).reduce(0) { $0 + ($1.0 == $1.1 ? 0 : 1) }
      let score = Double(diff) / Double(mask.count) + 0.20 * abs(log(aspect / ref.aspect))
      return (score, ref.char)
    }.sorted { $0.0 < $1.0 }
    return scores[0].0 < 0.29 && scores[1].0 - scores[0].0 > 0.018 ? scores[0].1 : nil
  }

  func string(_ boxes: [Blob], image: Raster) -> String? {
    var result = ""
    for box in boxes {
      guard let char = recognize(box, in: image) else { return nil }
      result.append(char)
    }
    return result.isEmpty ? nil : result
  }
}

struct Reading {
  let incoming: Int?
  let block: Int?
  let hp: Int?
  let status: String
  var text: String {
    guard let incoming else { return status }
    var result = "Incoming: \(incoming)"
    if let block {
      let after = max(0, incoming - block)
      result += "    After block: \(after)"
      if let hp, hp > 0 && after >= hp { result += "    LETHAL" }
    }
    return result
  }
}

struct CombatReader {
  let attacks: [Icon]
  let shields: [Icon]
  let shapes: Shapes
  init(resources: URL) throws {
    func icons(_ names: [String], scales: [Double], shield: Bool = false) throws -> [Icon] {
      try names.flatMap { name in
        let image = try loadImage(resources.appendingPathComponent(name + ".webp"))
        return try scales.map { try Icon(image, scale: $0, shield: shield) }
      }
    }
    attacks = try icons(
      (1...5).map { "intent_attack_\($0)" } + ["intent_death_blow"],
      scales: [0.5, 0.44, 0.56])
    shields = try icons(["block"], scales: [0.47, 0.53, 0.42], shield: true)
    shapes = try Shapes(fontURL: resources.appendingPathComponent("kreon_bold.ttf"))
  }

  static func damage(_ label: String) -> Int? {
    let parts = label.split(separator: "x", omittingEmptySubsequences: false)
    guard (1...2).contains(parts.count), (1...5).contains(parts[0].count),
      let base = Int(parts[0]), base >= 0
    else { return nil }
    if parts.count == 1 { return base }
    guard (1...3).contains(parts[1].count), let hits = Int(parts[1]), hits > 0 else { return nil }
    return base * hits
  }

  func vitals(_ image: Raster) -> (hp: Int, block: Int?)? {
    let left = image.width / 2
    let candidates = blobs(image, xRange: 0..<left, yRange: 370..<875).filter { b in
      guard (12...28).contains(b.height), b.width <= 23, b.x >= 3 else { return false }
      // The HP fraction is the only cream number with a deep red outline.
      for y in b.y...b.bottom {
        let p = image[b.x - 2, y]
        if p.r > 90 && p.g < 40 && p.b < 44 { return true }
      }
      return false
    }
    var players: [(Int, Blob)] = []
    for slash in candidates where shapes.recognize(slash, in: image) == "/" {
      let row = candidates.filter { abs($0.bottom - slash.bottom) < 7 }
      let before = row.filter { $0.right < slash.x }.reversed()
      let after = row.filter { $0.x > slash.right }
      var first = slash
      var last = slash
      var digits: [Blob] = []
      var maxDigits: [Blob] = []
      for box in before {
        if first.x - box.right > 7 { break }
        digits.insert(box, at: 0)
        first = box
      }
      for box in after {
        if box.x - last.right > 7 { break }
        maxDigits.append(box)
        last = box
      }
      if let a = shapes.string(digits, image: image), let hp = Int(a),
        let b = shapes.string(maxDigits, image: image), let maxHP = Int(b), hp <= maxHP
      {
        players.append((hp, Blob(x: first.x, y: first.y, right: last.right, bottom: last.bottom)))
      }
    }
    guard players.count == 1, let (hp, label) = players.first else { return nil }
    let cy = (label.y + label.bottom) / 2
    let cx = (label.x + label.right) / 2
    let low = max(0, cx - 230)
    let high = max(low, cx - 65)
    let hits = shields.flatMap { $0.hits(in: image, xRange: low..<high, yRange: cy - 18..<cy + 19) }
    if let hit = hits.min(by: { $0.score < $1.score }) {
      let digits = blobs(image, xRange: hit.x - 23..<hit.x + 24, yRange: hit.y - 14..<hit.y + 19)
        .filter { (10...26).contains($0.height) }
      return (hp, shapes.string(digits, image: image).flatMap(Int.init))
    }
    // A shield adds a light-blue stroke to the HP bar. Suppress subtraction
    // when that stroke is present but the shield value could not be decoded.
    var blue = 0
    for y in cy - 12...cy + 12 {
      for x in max(0, cx - 85)..<max(0, cx - 35) {
        let p = image[x, y]
        if p.b > 195 && p.g > 135 && Int(p.b) - Int(p.r) > 42 { blue += 1 }
      }
    }
    return (hp, blue > 20 ? nil : 0)
  }

  func read(_ frame: CGImage) throws -> Reading {
    let image = try Raster(frame, height: 1080)
    let matches = attacks.flatMap {
      $0.hits(in: image, xRange: image.width / 4..<image.width, yRange: 75..<790)
    }.sorted { $0.score < $1.score }
    var unique: [Icon.Hit] = []
    for m in matches where !unique.contains(where: { abs($0.x - m.x) < 22 && abs($0.y - m.y) < 22 })
    {
      unique.append(m)
    }
    let waiting = Reading(
      incoming: nil, block: nil, hp: nil, status: "Waiting for a visible attack")
    guard !unique.isEmpty else { return waiting }
    var incoming = 0
    for hit in unique {
      let boxes = blobs(image, xRange: hit.x - 43..<hit.x + 50, yRange: hit.y + 3..<hit.y + 38)
        .filter { (6...27).contains($0.height) && $0.width <= 24 }
      guard
        let start = boxes.firstIndex(where: {
          (-36 ... -15).contains($0.x - hit.x) && (11...26).contains($0.height)
        })
      else { return waiting }
      var digits = [boxes[start]]
      for box in boxes.dropFirst(start + 1) {
        guard box.x - digits.last!.right <= 6, abs(box.bottom - digits[0].bottom) <= 5 else {
          break
        }
        digits.append(box)
      }
      guard let text = shapes.string(digits, image: image), let value = Self.damage(text) else {
        return waiting
      }
      incoming += value
    }
    let player = vitals(image)
    return Reading(incoming: incoming, block: player?.block, hp: player?.hp, status: "")
  }
}
