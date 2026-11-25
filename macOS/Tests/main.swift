// End-to-end recognition checks using generated game labels at varied positions.
import CoreGraphics
import CoreText
import Foundation

let resources = URL(fileURLWithPath: CommandLine.arguments[1])
let reader = try CombatReader(resources: resources)
let provider = CGDataProvider(url: resources.appendingPathComponent("kreon_bold.ttf") as CFURL)!
let font = CGFont(provider)!
let width = 1720
let height = 1080
let ctx = CGContext(
  data: nil, width: width, height: height, bitsPerComponent: 8, bytesPerRow: width * 4,
  space: CGColorSpace(name: CGColorSpace.sRGB)!,
  bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
let cream = CGColor(srgbRed: 1, green: 0.965, blue: 0.886, alpha: 1)

func reset() {
  ctx.setFillColor(CGColor(gray: 0.12, alpha: 1))
  ctx.fill(CGRect(x: 0, y: 0, width: width, height: height))
}
func icon(_ name: String, x: Int, y: Int, size: Int) throws {
  let source = try loadImage(resources.appendingPathComponent(name + ".webp"))
  ctx.draw(source, in: CGRect(x: x - size / 2, y: height - y - size / 2, width: size, height: size))
}
func text(_ string: String, x: Int, baseline: Int, size: CGFloat, outline: CGColor? = nil) {
  var pen = CGFloat(x)
  var small = false
  for c in string {
    small = small || c == "x"
    let ct = CTFontCreateWithGraphicsFont(font, small ? size * 18 / 22 : size, nil, nil)
    var u = String(c).utf16.first!
    var glyph: CGGlyph = 0
    precondition(CTFontGetGlyphsForCharacters(ct, &u, &glyph, 1))
    let path = CTFontCreatePathForGlyph(ct, glyph, nil)!
    ctx.saveGState()
    ctx.translateBy(x: pen, y: CGFloat(height - baseline))
    if let outline {
      ctx.addPath(path)
      ctx.setStrokeColor(outline)
      ctx.setLineWidth(5)
      ctx.strokePath()
    }
    ctx.setFillColor(cream)
    ctx.addPath(path)
    ctx.fillPath()
    ctx.restoreGState()
    pen += CTFontGetAdvancesForGlyphs(ct, .horizontal, &glyph, nil, 1) + 1
  }
}
func attack(_ label: String, x: Int, y: Int, tier: Int) throws {
  try icon("intent_attack_\(tier)", x: x, y: y, size: 72)
  text(label, x: x - 28, baseline: y + 28, size: 22)
}
func expect(_ incoming: Int?, _ block: Int?, _ hp: Int?, line: UInt = #line) throws {
  let reading = try reader.read(ctx.makeImage()!)
  guard reading.incoming == incoming && reading.block == block && reading.hp == hp else {
    fatalError(
      "Line \(line): expected \(String(describing: incoming))/\(String(describing: block))/\(String(describing: hp)), got \(reading)"
    )
  }
}
reset()
try expect(nil, nil, nil)
try attack("3x4", x: 952, y: 330, tier: 1)
try attack("15", x: 1350, y: 420, tier: 4)
try expect(27, nil, nil)
let red = CGColor(srgbRed: 0.58, green: 0, blue: 0, alpha: 1)
text("42/77", x: 310, baseline: 756, size: 24, outline: red)
try expect(27, 0, 42)
try icon("block", x: 205, y: 746, size: 60)
text("9", x: 199, baseline: 755, size: 24)
try expect(27, 9, 42)
reset()
try attack("12x", x: 1010, y: 380, tier: 2)
try expect(nil, nil, nil)
reset()
try attack("6", x: 1150, y: 420, tier: 3)
try icon("intent_attack_5", x: 1320, y: 390, size: 72)
try expect(nil, nil, nil)
print("Passed blank, multi-enemy, multi-hit, HP, block, malformed and missing-label cases")
