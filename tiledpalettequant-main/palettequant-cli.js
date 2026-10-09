const fs = require('fs');
const path = require('path');
const { PNG } = require('pngjs');
const core = require('./quantizer-core.js');

function printUsage() {
    console.log("Usage: node palettequant-cli.js <input.png> <output_indices.bin> <output.pal> [options]");
    console.log("Options:");
    console.log("  --tileWidth <n>      (default: 8)");
    console.log("  --tileHeight <n>     (default: 8)");
    console.log("  --numPalettes <n>    (default: 8)");
    console.log("  --colorsPerPalette <n> (default: 4)");
    console.log("  --bitsPerChannel <n> (default: 5)");
    console.log("  --dither <0|1|2>     (0: Off, 1: Fast, 2: Slow)");
    console.log("  --colorZeroBehaviour <0|1|2|3> (0:Unique, 1:Shared, 2:TransparentFromTransparent, 3:TransparentFromColor)");
}

async function run() {
    const args = process.argv.slice(2);
    if (args.length < 3) {
        printUsage();
        process.exit(1);
    }

    const inputPath = args[0];
    const binPath = args[1];
    const palPath = args[2];

    const options = { ...core.quantizationOptions };

    for (let i = 3; i < args.length; i++) {
        if (args[i] === '--tileWidth') options.tileWidth = parseInt(args[++i]);
        if (args[i] === '--tileHeight') options.tileHeight = parseInt(args[++i]);
        if (args[i] === '--numPalettes') options.numPalettes = parseInt(args[++i]);
        if (args[i] === '--colorsPerPalette') options.colorsPerPalette = parseInt(args[++i]);
        if (args[i] === '--bitsPerChannel') options.bitsPerChannel = parseInt(args[++i]);
        if (args[i] === '--dither') options.dither = parseInt(args[++i]);
        if (args[i] === '--colorZeroBehaviour') options.colorZeroBehaviour = parseInt(args[++i]);
    }

    Object.assign(core.quantizationOptions, options);
    if (options.ditherPattern !== undefined) {
        core.setDitherPattern(options.ditherPattern);
    }

    console.log(`Processing ${inputPath}...`);
    
    const data = fs.readFileSync(inputPath);
    const png = PNG.sync.read(data);

    const imageData = {
        width: png.width,
        height: png.height,
        data: png.data
    };

    const result = core.quantizeImage(imageData);
    const { palettes, quantizedImage } = result;

    // 1. Save Indices (.bin)
    // We save them Top-Down as raw bytes.
    fs.writeFileSync(binPath, Buffer.from(quantizedImage.colorIndexes));

    // 2. Save Palette (.pal - SNES RGB15 format)
    // Stride is colorsPerPalette * 2 bytes per palette.
    // For 2BPP: 4 colors × 2 bytes = 8 bytes per palette (contiguous in CGRAM).
    // For 4BPP: 16 colors × 2 bytes = 32 bytes per palette.
    const cpp = options.colorsPerPalette;
    const palStride = cpp * 2;
    const palBufferSize = palettes.length * palStride;
    const palBuffer = Buffer.alloc(palBufferSize); // All zeros = black by default
    
    const isTransparentMode = (options.colorZeroBehaviour === 2 || options.colorZeroBehaviour === 3);

    function toSnesColor(color) {
        const r = Math.floor(color[0] / 8);
        const g = Math.floor(color[1] / 8);
        const b = Math.floor(color[2] / 8);
        return (r & 0x1F) | ((g & 0x1F) << 5) | ((b & 0x1F) << 10);
    }

    for (let p = 0; p < palettes.length; p++) {
        const palBase = p * palStride;
        
        if (isTransparentMode) {
            // Index 0 = $0000 (black/transparent) — already zero from Buffer.alloc
            // Write quantized colors at indices 1+ in quantizer order (already brightness-sorted)
            for (let c = 0; c < palettes[p].length; c++) {
                const offset = palBase + ((c + 1) * 2); // +1 to skip index 0
                if (offset + 1 >= palBufferSize) break;
                palBuffer.writeUInt16LE(toSnesColor(palettes[p][c]), offset);
            }
        } else {
            // No transparency — write colors as-is (already brightness-sorted by quantizer)
            for (let c = 0; c < palettes[p].length; c++) {
                const offset = palBase + (c * 2);
                if (offset + 1 >= palBufferSize) break;
                palBuffer.writeUInt16LE(toSnesColor(palettes[p][c]), offset);
            }
        }
    }
    fs.writeFileSync(palPath, palBuffer);
    
    console.log(`Palette format: ${cpp} colors/palette, ${palStride} bytes/palette, ${palBufferSize} bytes total`);
    console.log(`Transparent mode: ${isTransparentMode} (colorZeroBehaviour=${options.colorZeroBehaviour})`);

    console.log("Quantization complete.");
    console.log(`Indices saved to ${binPath}`);
    console.log(`Palette saved to ${palPath}`);
}

run().catch(err => {
    console.error(err);
    process.exit(1);
});
