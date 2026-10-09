"use strict";

// Constants and Enums
var Action;
(function (Action) {
    Action[Action["StartQuantization"] = 0] = "StartQuantization";
    Action[Action["UpdateProgress"] = 1] = "UpdateProgress";
    Action[Action["UpdateQuantizedImage"] = 2] = "UpdateQuantizedImage";
    Action[Action["UpdatePalettes"] = 3] = "UpdatePalettes";
    Action[Action["DoneQuantization"] = 4] = "DoneQuantization";
})(Action || (Action = {}));

var ColorZeroBehaviour;
(function (ColorZeroBehaviour) {
    ColorZeroBehaviour[ColorZeroBehaviour["Unique"] = 0] = "Unique";
    ColorZeroBehaviour[ColorZeroBehaviour["Shared"] = 1] = "Shared";
    ColorZeroBehaviour[ColorZeroBehaviour["TransparentFromTransparent"] = 2] = "TransparentFromTransparent";
    ColorZeroBehaviour[ColorZeroBehaviour["TransparentFromColor"] = 3] = "TransparentFromColor";
})(ColorZeroBehaviour || (ColorZeroBehaviour = {}));

var Dither;
(function (Dither) {
    Dither[Dither["Off"] = 0] = "Off";
    Dither[Dither["Fast"] = 1] = "Fast";
    Dither[Dither["Slow"] = 2] = "Slow";
})(Dither || (Dither = {}));

var DitherPattern;
(function (DitherPattern) {
    DitherPattern[DitherPattern["Diagonal4"] = 0] = "Diagonal4";
    DitherPattern[DitherPattern["Horizontal4"] = 1] = "Horizontal4";
    DitherPattern[DitherPattern["Vertical4"] = 2] = "Vertical4";
    DitherPattern[DitherPattern["Diagonal2"] = 3] = "Diagonal2";
    DitherPattern[DitherPattern["Horizontal2"] = 4] = "Horizontal2";
    DitherPattern[DitherPattern["Vertical2"] = 5] = "Vertical2";
})(DitherPattern || (DitherPattern = {}));

const ditherPatterns = new Map();
ditherPatterns.set(DitherPattern.Diagonal4, [[0, 2], [3, 1]]);
ditherPatterns.set(DitherPattern.Horizontal4, [[0, 3], [1, 2]]);
ditherPatterns.set(DitherPattern.Vertical4, [[0, 1], [3, 2]]);
ditherPatterns.set(DitherPattern.Diagonal2, [[0, 1], [1, 0]]);
ditherPatterns.set(DitherPattern.Horizontal2, [[0, 1], [0, 1]]);
ditherPatterns.set(DitherPattern.Vertical2, [[0, 0], [1, 1]]);

// --- STATE ---
let ditherPattern = ditherPatterns.get(DitherPattern.Diagonal4);
let ditherPixels = 4;
let quantizationOptions = {
    tileWidth: 8,
    tileHeight: 8,
    numPalettes: 8,
    colorsPerPalette: 4,
    bitsPerChannel: 5,
    fractionOfPixels: 0.1,
    colorZeroBehaviour: ColorZeroBehaviour.Unique,
    colorZeroValue: [0, 0, 0],
    dither: Dither.Off,
    ditherWeight: 0.5,
    ditherPattern: DitherPattern.Diagonal4,
};

// --- HELPER FUNCTIONS ---
function cloneColor(color) { return [color[0], color[1], color[2]]; }
function copyColor(dest, source) { dest[0] = source[0]; dest[1] = source[1]; dest[2] = source[2]; }
function addColor(c1, c2) { c1[0] += c2[0]; c1[1] += c2[1]; c1[2] += c2[2]; }
function subtractColor(c1, c2) { c1[0] -= c2[0]; c1[1] -= c2[1]; c1[2] -= c2[2]; }
function scaleColor(color, scaleFactor) { color[0] *= scaleFactor; color[1] *= scaleFactor; color[2] *= scaleFactor; }
function clampColor(color, minValue, maxValue) {
    color[0] = Math.max(minValue, Math.min(maxValue, color[0]));
    color[1] = Math.max(minValue, Math.min(maxValue, color[1]));
    color[2] = Math.max(minValue, Math.min(maxValue, color[2]));
}
const alphaValues = [0, 255, 85, 36.42857, 17, 8.22581, 4.04762, 2.00787, 1];
function toNbit(value, n) {
    const alpha = alphaValues[n];
    return Math.round(Math.round(value / alpha) * alpha);
}
function toNbitColor(color, n) {
    color[0] = toNbit(color[0], n);
    color[1] = toNbit(color[1], n);
    color[2] = toNbit(color[2], n);
}
function toLinear(x) { return x * x; }
function toLinearColor(color) { color[0] = toLinear(color[0]); color[1] = toLinear(color[1]); color[2] = toLinear(color[2]); }
function toSrgb(x) { return Math.sqrt(x); }
function toSrgbColor(color) { color[0] = toSrgb(color[0]); color[1] = toSrgb(color[1]); color[2] = toSrgb(color[2]); }
const brightnessScale = [0.299, 0.587, 0.114];
function brightness(color) {
    let sum = 0;
    for (let i = 0; i < 3; i++) sum += brightnessScale[i] * toLinear(color[i]);
    return sum;
}
function moveColorCloser(color, pixelColor, alpha) {
    const invAlpha = 1.0 - alpha;
    color[0] = (invAlpha * color[0] + alpha * pixelColor[0]);
    color[1] = (invAlpha * color[1] + alpha * pixelColor[1]);
    color[2] = (invAlpha * color[2] + alpha * pixelColor[2]);
}
function colorDistance(a, b) {
    return 2 * Math.pow((a[0] - b[0]), 2) + 4 * Math.pow((a[1] - b[1]), 2) + Math.pow((a[2] - b[2]), 2);
}
function equalColors(c1, c2) { return c1[0] === c2[0] && c1[1] === c2[1] && c1[2] === c2[2]; }

// --- ARRAY HELPERS ---
function zeroArray(len) { return new Array(len).fill(0); }
function zeros2(len1, len2) { return new Array(len1).fill(0).map(() => zeroArray(len2)); }
function zeros3(len1, len2, len3) { return new Array(len1).fill(0).map(() => zeros2(len2, len3)); }
function maxIndex(values) {
    let maxI = 0;
    for (let i = 1; i < values.length; i++) if (values[i] > values[maxI]) maxI = i;
    return maxI;
}
function minIndex(values) {
    let minI = 0;
    for (let i = 1; i < values.length; i++) if (values[i] < values[minI]) minI = i;
    return minI;
}
function reverse(a, left, right) {
    const middle = (left + right) / 2.0;
    while (left < middle) {
        [a[left], a[right]] = [a[right], a[left]];
        left++; right--;
    }
}
class RandomShuffle {
    constructor(n) { this.values = Array.from({length: n}, (_, i) => i); this.currentIndex = n - 1; }
    shuffle() {
        for (let i = 0; i < this.values.length; i++) {
            const index = i + Math.floor(Math.random() * (this.values.length - i));
            [this.values[i], this.values[index]] = [this.values[index], this.values[i]];
        }
    }
    next() {
        this.currentIndex++;
        if (this.currentIndex >= this.values.length) { this.shuffle(); this.currentIndex = 0; }
        return this.values[this.currentIndex];
    }
}

// --- CORE LOGIC ---
function getClosestColor(palette, color) {
    let minIndex = palette.length - 1;
    let minDist = colorDistance(palette[minIndex], color);
    for (let i = palette.length - 2; i >= 0; i--) {
        const dist = colorDistance(palette[i], color);
        if (dist < minDist) { minIndex = i; minDist = dist; }
    }
    return [minIndex, minDist];
}
function getClosestColorDither(palette, pixel) {
    const error = [0, 0, 0];
    const linearPixel = cloneColor(pixel.color);
    toLinearColor(linearPixel);
    const candidates = [];
    const c = [0, 0, 0];
    const err = [0, 0, 0];
    const reducedColor = [0, 0, 0];
    for (let i = 0; i < ditherPixels; i++) {
        copyColor(c, linearPixel);
        copyColor(err, error);
        scaleColor(err, quantizationOptions.ditherWeight);
        addColor(c, err);
        clampColor(c, 0, 255 * 255);
        toSrgbColor(c);
        const [minColorIndex, minDist] = getClosestColor(palette, c);
        const minColor = palette[minColorIndex];
        candidates.push({ colorIndex: minColorIndex, colorDistance: minDist, comparedColor: [c[0], c[1], c[2]], brightness: brightness(minColor) });
        copyColor(reducedColor, minColor);
        toNbitColor(reducedColor, quantizationOptions.bitsPerChannel);
        toLinearColor(reducedColor);
        addColor(error, linearPixel);
        subtractColor(error, reducedColor);
    }
    candidates.sort((a, b) => a.brightness - b.brightness);
    const index = ditherPattern[pixel.x & 1][pixel.y & 1];
    return [candidates[index].colorIndex, candidates[index].colorDistance, candidates[index].comparedColor];
}
function paletteDistance(palette, tile) {
    let sum = 0;
    for (let i = 0; i < tile.colors.length; i++) {
        const [, minDist] = getClosestColor(palette, tile.colors[i]);
        sum += tile.counts[i] * minDist;
    }
    return sum;
}
function paletteDistanceDither(palette, tile) {
    let sum = 0;
    for (const pixel of tile.pixels) {
        const [, minDist] = getClosestColorDither(palette, pixel);
        sum += minDist;
    }
    return sum;
}
function getClosestPaletteIndex(palettes, tile) {
    const distances = palettes.map(p => paletteDistance(p, tile));
    return minIndex(distances);
}
function closestPaletteDistance(palettes, tile) {
    const distances = palettes.map(p => paletteDistance(p, tile));
    const index = minIndex(distances);
    return [index, distances[index]];
}
function getClosestPaletteIndexDither(palettes, tile) {
    const distances = palettes.map(p => paletteDistanceDither(p, tile));
    return minIndex(distances);
}
function closestPaletteDistanceDither(palettes, tile) {
    const distances = palettes.map(p => paletteDistanceDither(p, tile));
    const index = minIndex(distances);
    return [index, distances[index]];
}
function getColor(image, x, y) {
    const index = 4 * (x + image.width * y);
    return [image.data[index], image.data[index + 1], image.data[index + 2]];
}
function extractTile(image, startX, startY) {
    const { tileWidth, tileHeight, colorZeroBehaviour, colorZeroValue } = quantizationOptions;
    const tile = { colors: [], counts: [], pixels: [] };
    const endX = Math.min(startX + tileWidth, image.width);
    const endY = Math.min(startY + tileHeight, image.height);
    for (let y = startY; y < endY; y++) {
        for (let x = startX; x < endX; x++) {
            const color = getColor(image, x, y);
            const index = 4 * (x + image.width * y);
            if ((colorZeroBehaviour === ColorZeroBehaviour.TransparentFromTransparent && image.data[index + 3] < 255) ||
                (colorZeroBehaviour === ColorZeroBehaviour.TransparentFromColor && equalColors(color, colorZeroValue))) continue;
            tile.pixels.push({ tile, color, x, y });
            const colorIndex = tile.colors.findIndex(c => equalColors(c, color));
            if (colorIndex >= 0) tile.counts[colorIndex]++;
            else { tile.colors.push(color); tile.counts.push(1); }
        }
    }
    return tile;
}
function extractTiles(image) {
    const { tileWidth, tileHeight } = quantizationOptions;
    const tiles = [];
    for (let y = 0; y < image.height; y += tileHeight) {
        for (let x = 0; x < image.width; x += tileWidth) {
            const tile = extractTile(image, x, y);
            if (tile.colors.length > 0) tiles.push(tile);
        }
    }
    return tiles;
}
function extractAllPixels(tiles) {
    const pixels = [];
    for (const tile of tiles) for (const pixel of tile.pixels) pixels.push({ ...pixel });
    return pixels;
}
function quantizeTiles(palettes, image, useDither) {
    const { bitsPerChannel, colorZeroBehaviour, colorZeroValue, colorsPerPalette } = quantizationOptions;
    const imageIsReduced = quantizationOptions.dither !== Dither.Off;
    const adjustedIndex = (colorZeroBehaviour === ColorZeroBehaviour.TransparentFromColor || colorZeroBehaviour === ColorZeroBehaviour.TransparentFromTransparent) ? 1 : 0;
    const reducedPalettes = palettes.map(pal => pal.map(color => {
        const col = cloneColor(color);
        toNbitColor(col, bitsPerChannel);
        return col;
    }));
    const transparentColor = cloneColor(colorZeroValue);
    if (imageIsReduced) toNbitColor(transparentColor, bitsPerChannel);
    const quantizedImage = {
        width: image.width,
        height: image.height,
        colorIndexes: new Uint8ClampedArray(image.width * image.height),
    };
    for (let startY = 0; startY < image.height; startY += quantizationOptions.tileHeight) {
        for (let startX = 0; startX < image.width; startX += quantizationOptions.tileWidth) {
            const tile = extractTile(image, startX, startY);
            let closestPaletteIndex = 0;
            if (tile.colors.length > 0) {
                closestPaletteIndex = useDither ? getClosestPaletteIndexDither(reducedPalettes, tile) : getClosestPaletteIndex(reducedPalettes, tile);
            }
            const palette = reducedPalettes[closestPaletteIndex];
            const endX = Math.min(startX + quantizationOptions.tileWidth, image.width);
            const endY = Math.min(startY + quantizationOptions.tileHeight, image.height);
            for (let y = startY; y < endY; y++) {
                for (let x = startX; x < endX; x++) {
                    const index = 4 * (x + image.width * y);
                    const outIndex = x + image.width * y;
                    const color = [image.data[index], image.data[index+1], image.data[index+2]];
                    if ((colorZeroBehaviour === ColorZeroBehaviour.TransparentFromTransparent && image.data[index+3] < 255) ||
                        (colorZeroBehaviour === ColorZeroBehaviour.TransparentFromColor && equalColors(color, transparentColor))) {
                        quantizedImage.colorIndexes[outIndex] = closestPaletteIndex * colorsPerPalette;
                    } else {
                        const [cIdx] = useDither ? getClosestColorDither(palette, { color, x, y }) : getClosestColor(palette, color);
                        quantizedImage.colorIndexes[outIndex] = closestPaletteIndex * colorsPerPalette + cIdx + adjustedIndex;
                    }
                }
            }
        }
    }
    return quantizedImage;
}
function colorQuantize1Color(tiles, pixels, randomShuffle) {
    let iterations = quantizationOptions.fractionOfPixels * pixels.length;
    let alpha = 0.3;
    if (quantizationOptions.dither === Dither.Slow) { iterations /= 5; alpha = 0.1; }
    const avgColor = [0, 0, 0];
    for (const p of pixels) addColor(avgColor, p.color);
    scaleColor(avgColor, 1.0 / pixels.length);
    const palettes = [[avgColor]];
    if (quantizationOptions.colorZeroBehaviour === ColorZeroBehaviour.Shared) {
        palettes[0].push(avgColor);
        palettes[0][0] = cloneColor(quantizationOptions.colorZeroValue);
    }
    let splitIndex = 0;
    for (let i = 2; i <= quantizationOptions.numPalettes; i++) {
        palettes.push(palettes[splitIndex].map(cloneColor));
        for (let iter = 0; iter < iterations; iter++) movePalettesCloser(palettes, pixels[randomShuffle.next()], alpha);
        const pDistArr = zeroArray(palettes.length);
        for (const t of tiles) pDistArr[closestPaletteDistance(palettes, t)[0]] += closestPaletteDistance(palettes, t)[1];
        splitIndex = maxIndex(pDistArr);
    }
    return palettes;
}
function expandPalettesByOneColor(palettes, tiles, pixels, randomShuffle) {
    let iterations = quantizationOptions.fractionOfPixels * pixels.length;
    let alpha = 0.3;
    if (quantizationOptions.dither === Dither.Slow) { iterations /= 5; alpha = 0.1; }
    const numColors = palettes[0].length + 1;
    const splitIndexes = zeroArray(palettes.length);
    if (numColors > 2) {
        const totalColorDistances = palettes.map(() => zeroArray(numColors));
        for (const tile of tiles) {
            const pIdx = getClosestPaletteIndex(palettes, tile);
            const pal = palettes[pIdx];
            for (let i = 0; i < tile.colors.length; i++) {
                const [cIdx, dist] = getClosestColor(pal, tile.colors[i]);
                totalColorDistances[pIdx][cIdx] += tile.counts[i] * dist;
            }
        }
        for (let i = 0; i < palettes.length; i++) splitIndexes[i] = maxIndex(totalColorDistances[i]);
    }
    for (let i = 0; i < palettes.length; i++) palettes[i].push(cloneColor(palettes[i][splitIndexes[i]]));
    for (let iter = 0; iter < iterations; iter++) movePalettesCloser(palettes, pixels[randomShuffle.next()], alpha);
}
function replaceWeakestColors(palettes, tiles, minColorFactor, minPaletteFactor, replacePalettes) {
    const { colorZeroBehaviour } = quantizationOptions;
    const useSlowDither = quantizationOptions.dither === Dither.Slow;
    const closestPal = useSlowDither ? closestPaletteDistanceDither : closestPaletteDistance;
    const closestPaletteIndex = zeroArray(tiles.length);
    const totalPaletteMse = zeroArray(palettes.length);
    const removedPaletteMse = zeroArray(palettes.length);
    if (palettes.length > 1) {
        for (let j = 0; j < tiles.length; j++) {
            const [idx, dist] = closestPal(palettes, tiles[j]);
            totalPaletteMse[idx] += dist;
            closestPaletteIndex[j] = idx;
            const remaining = palettes.filter((_, i) => i !== idx);
            if (remaining.length > 0) {
                const [, dist2] = closestPal(remaining, tiles[j]);
                removedPaletteMse[idx] += dist2;
            }
        }
    }
    const result = [];
    const totalColorMse = palettes.map(p => zeroArray(p.length));
    const secondColorMse = palettes.map(p => zeroArray(p.length));
    for (let j = 0; j < tiles.length; j++) {
        const pIdx = closestPaletteIndex[j];
        const pal = palettes[pIdx];
        const tile = tiles[j];
        if (useSlowDither) {
            for (const px of tile.pixels) {
                const [cIdx, dist] = getClosestColorDither(pal, px);
                totalColorMse[pIdx][cIdx] += dist;
                const rem = pal.filter((_, i) => i !== cIdx);
                const [, dist2] = getClosestColorDither(rem, px);
                secondColorMse[pIdx][cIdx] += dist2;
            }
        } else {
            for (let i = 0; i < tile.colors.length; i++) {
                const [cIdx, dist] = getClosestColor(pal, tile.colors[i]);
                totalColorMse[pIdx][cIdx] += dist * tile.counts[i];
                const rem = pal.filter((_, i) => i !== cIdx);
                const [, dist2] = getClosestColor(rem, tile.colors[i]);
                secondColorMse[pIdx][cIdx] += dist2 * tile.counts[i];
            }
        }
    }
    let sharedIdx = (colorZeroBehaviour === ColorZeroBehaviour.Shared) ? 0 : -1;
    for (let pIdx = 0; pIdx < palettes.length; pIdx++) {
        const maxC = maxIndex(totalColorMse[pIdx]);
        const minC = minIndex(secondColorMse[pIdx]);
        const replace = minC !== maxC && minC !== sharedIdx && secondColorMse[pIdx][minC] < minColorFactor * totalColorMse[pIdx][maxC];
        result.push(palettes[pIdx].map((c, i) => (i === minC && replace) ? cloneColor(palettes[pIdx][maxC]) : cloneColor(c)));
    }
    if (replacePalettes && palettes.length > 1) {
        const maxP = maxIndex(totalPaletteMse);
        const minP = minIndex(removedPaletteMse);
        if (minP !== maxP && removedPaletteMse[minP] < minPaletteFactor * totalPaletteMse[maxP]) result[minP] = result[maxP].map(cloneColor);
    }
    return result;
}
function kMeans(palettes, tiles) {
    const counts = palettes.map(p => zeroArray(p.length));
    const sums = palettes.map(p => p.map(() => [0, 0, 0]));
    for (const t of tiles) {
        const pIdx = (quantizationOptions.dither === Dither.Slow) ? getClosestPaletteIndexDither(palettes, t) : getClosestPaletteIndex(palettes, t);
        if (quantizationOptions.dither === Dither.Slow) {
            for (const px of t.pixels) {
                const [cIdx] = getClosestColorDither(palettes[pIdx], px);
                counts[pIdx][cIdx]++; addColor(sums[pIdx][cIdx], px.color);
            }
        } else {
            for (let i = 0; i < t.colors.length; i++) {
                const [cIdx] = getClosestColor(palettes[pIdx], t.colors[i]);
                counts[pIdx][cIdx] += t.counts[i];
                const col = cloneColor(t.colors[i]);
                scaleColor(col, t.counts[i]); addColor(sums[pIdx][cIdx], col);
            }
        }
    }
    let sharedIdx = (quantizationOptions.colorZeroBehaviour === ColorZeroBehaviour.Shared) ? 0 : -1;
    return sums.map((pSums, i) => pSums.map((s, j) => {
        if (counts[i][j] === 0 || j === sharedIdx) return cloneColor(palettes[i][j]);
        scaleColor(s, 1.0 / counts[i][j]); return s;
    }));
}
function reducePalettes(palettes, bits) { return palettes.map(p => p.map(c => { const col = cloneColor(c); toNbitColor(col, bits); return col; })); }
function movePalettesCloser(palettes, pixel, alpha) {
    let sharedIdx = (quantizationOptions.colorZeroBehaviour === ColorZeroBehaviour.Shared) ? 0 : -1;
    const pIdx = (quantizationOptions.dither === Dither.Slow) ? getClosestPaletteIndexDither(palettes, pixel.tile) : getClosestPaletteIndex(palettes, pixel.tile);
    const res = (quantizationOptions.dither === Dither.Slow) ? getClosestColorDither(palettes[pIdx], pixel) : [getClosestColor(palettes[pIdx], pixel.color)[0], null, pixel.color];
    if (res[0] !== sharedIdx) moveColorCloser(palettes[pIdx][res[0]], res[2], alpha);
}
function sortPalettes(palettes, startIndex) {
    const numPal = palettes.length; const numCol = palettes[0].length;
    if (numCol === 2 && startIndex === 1) return palettes;
    const palDist = zeros2(numPal + 2, numPal + 2);
    const colIdx = zeros3(numPal, numPal, numCol);
    for (let i = 0; i < numPal; i++) for (let j = 0; j < numPal; j++) for (let k = 0; k < numCol; k++) colIdx[i][j][k] = k;
    for (let p1 = 0; p1 < numPal - 1; p1++) for (let p2 = p1 + 1; p2 < numPal; p2++) {
        const idx = colIdx[p1][p2];
        for (let it = 0; it < 2000; it++) {
            let i1 = startIndex + Math.floor(Math.random() * (numCol - startIndex - 1));
            let i2 = i1 + 1 + Math.floor(Math.random() * (numCol - i1 - 1));
            if (Math.random() < 0.5) [i1, i2] = [i2, i1];
            if (colorDistance(palettes[p1][i1], palettes[p2][idx[i2]]) + colorDistance(palettes[p1][i2], palettes[p2][idx[i1]]) < colorDistance(palettes[p1][i1], palettes[p2][idx[i1]]) + colorDistance(palettes[p1][i2], palettes[p2][idx[i2]])) [idx[i1], idx[i2]] = [idx[i2], idx[i1]];
        }
        let sum = 0; for (let i = 0; i < numCol; i++) sum += colorDistance(palettes[p1][i], palettes[p2][idx[i]]);
        palDist[p1+1][p2+1] = palDist[p2+1][p1+1] = sum;
    }
    for (let p1 = 1; p1 < numPal; p1++) for (let p2 = 0; p2 < p1; p2++) for (let i = 0; i < numCol; i++) colIdx[p1][p2][i] = colIdx[p2][p1].indexOf(i);
    const pIdxs = Array.from({length: numPal + 2}, (_, i) => i);
    if (numPal > 2) for (let it = 0; it < 100000; it++) {
        const i1 = Math.max(1, Math.floor(Math.random() * numPal)), i2 = Math.min(numPal, i1 + 1 + Math.floor(Math.random() * numPal));
        if (palDist[pIdxs[i1-1]][pIdxs[i2]] + palDist[pIdxs[i1]][pIdxs[i2+1]] < palDist[pIdxs[i1-1]][pIdxs[i1]] + palDist[pIdxs[i2]][pIdxs[i2+1]]) reverse(pIdxs, i1, i2);
    }
    const pIdxRes = zeros2(numPal, numCol);
    const p1IdxList = Array.from({length: numCol + 2}, (_, i) => i);
    const p1Dists = zeros2(numCol + 2, numCol + 2);
    const baseP = palettes[pIdxs[1]-1];
    for (let i = 1; i <= numCol; i++) for (let j = 1; j <= numCol; j++) p1Dists[i][j] = colorDistance(baseP[i-1], baseP[j-1]);
    if (numCol > 2) for (let it = 0; it < 100000; it++) {
        const i1 = Math.max(1 + startIndex, Math.floor(Math.random() * numCol)), i2 = Math.min(numCol, i1 + 1 + Math.floor(Math.random() * numCol));
        if (p1Dists[p1IdxList[i1-1]][p1IdxList[i2]] + p1Dists[p1IdxList[i1]][p1IdxList[i2+1]] < p1Dists[p1IdxList[i1-1]][p1IdxList[i1]] + p1Dists[p1IdxList[i2]][p1IdxList[i2+1]]) reverse(p1IdxList, i1, i2);
    }
    for (let i = 0; i < numCol; i++) pIdxRes[0][i] = p1IdxList[i+1]-1;
    for (let i = 1; i < numPal; i++) for (let j = 0; j < numCol; j++) pIdxRes[i][j] = colIdx[pIdxs[i]-1][pIdxs[i+1]-1][pIdxRes[i-1][j]];
    return pIdxs.slice(1, numPal+1).map((idx, i) => pIdxRes[i].map(cIdx => palettes[idx-1][cIdx]));
}

// --- MAIN ENTRY ---
function quantizeImage(image) {
    const reduced = { width: image.width, height: image.height, data: new Uint8ClampedArray(image.data.length) };
    const ditherOn = quantizationOptions.dither !== Dither.Off;
    for (let i = 0; i < image.data.length; i++) reduced.data[i] = ditherOn ? image.data[i] : toNbit(image.data[i], quantizationOptions.bitsPerChannel);
    const tiles = extractTiles(reduced), pixels = extractAllPixels(tiles), shuffle = new RandomShuffle(pixels.length);
    let pals = colorQuantize1Color(tiles, pixels, shuffle);
    let sC = 2 + (quantizationOptions.colorZeroBehaviour === ColorZeroBehaviour.Shared ? 1 : 0);
    let eC = quantizationOptions.colorsPerPalette - ((quantizationOptions.colorZeroBehaviour === ColorZeroBehaviour.TransparentFromColor || quantizationOptions.colorZeroBehaviour === ColorZeroBehaviour.TransparentFromTransparent) ? 1 : 0);
    for (let n = sC; n <= eC; n++) expandPalettesByOneColor(pals, tiles, pixels, shuffle);
    for (let i = 0; i < 10; i++) {
        pals = replaceWeakestColors(pals, tiles, 0.5, 0.5, true);
        for (let j = 0; j < pixels.length * quantizationOptions.fractionOfPixels; j++) movePalettesCloser(pals, pixels[shuffle.next()], 0.3);
    }
    if (!ditherOn) { pals = reducePalettes(pals, quantizationOptions.bitsPerChannel); for (let i = 0; i < 3; i++) pals = kMeans(pals, tiles); }
    pals = reducePalettes(pals, quantizationOptions.bitsPerChannel);
    const sSort = (quantizationOptions.colorZeroBehaviour !== ColorZeroBehaviour.Unique) ? 1 : 0;
    let sorted = sortPalettes(pals, sSort);
    for (let i = 0; i < sorted.length; i++) {
        sorted[i].sort((a, b) => brightness(a) - brightness(b));
    }
    return { palettes: sorted, quantizedImage: quantizeTiles(sorted, reduced, ditherOn) };
}

if (typeof module !== "undefined" && module.exports) module.exports = {
    Action, ColorZeroBehaviour, Dither, DitherPattern, quantizationOptions, ditherPatterns,
    quantizeImage, quantizeTiles, brightness, toLinear, toSrgb, cloneColor, toNbitColor,
    setDitherPattern: (p) => { ditherPattern = ditherPatterns.get(p); ditherPixels = [DitherPattern.Diagonal2, DitherPattern.Horizontal2, DitherPattern.Vertical2].includes(p) ? 2 : 4; }
};
