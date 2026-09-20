export const PromptType = Object.freeze({
    Negative: 0,
    Positive: 1,
    TopLeft: 2,
    BottomRight: 3,
});

export const ExecutionDevice = Object.freeze({
    Auto: "auto",
    WebGpu: "webgpu",
    Wasm: "wasm",
});

export const EngineState = Object.freeze({
    Uninitialized: "uninitialized",
    Loading: "loading",
    Ready: "ready",
    Encoding: "encoding",
    Error: "error",
    Disposed: "disposed",
});

const DEFAULT_MODEL_BASE_URL = "https://huggingface.co/Acly/MobileSAM/resolve/main/";
const DEFAULT_ENCODER_PATH = "mobile_sam_image_encoder.onnx";
const DEFAULT_DECODER_PATH = "sam_mask_decoder_single.onnx";
const CACHE_NAME = "mobilesam-model-v1";
const ORT_CDN_URL =
    "https://cdn.jsdelivr.net/npm/onnxruntime-web@1.21.0/dist/ort.all.bundle.min.mjs";
const ORT_WASM_PATH = "https://cdn.jsdelivr.net/npm/onnxruntime-web@1.21.0/dist/";

const TARGET_SIZE = 1024;
const CHANNELS = 3;
const LOW_RES_SIZE = 256;
const PIXEL_MEAN_R = 123.675;
const PIXEL_MEAN_G = 116.28;
const PIXEL_MEAN_B = 103.53;
const PIXEL_STD_R = 58.395;
const PIXEL_STD_G = 57.12;
const PIXEL_STD_B = 57.375;

const MASK_THRESHOLD = 0.0;
const SIMPLIFICATION_EPSILON = 1.0;
const NEIGHBOR_DX = [0, 1, 1, 1, 0, -1, -1, -1];
const NEIGHBOR_DY = [-1, -1, 0, 1, 1, 1, 0, -1];

export class ModelStorage {
    #cacheName;

    constructor(cacheName = CACHE_NAME) {
        this.#cacheName = cacheName;
    }

    async loadBuffer(url, onProgress) {
        const cached = await this.#tryGetCached(url);
        if (cached) {
            return cached;
        }

        const buffer = await this.#fetchAndCache(url, onProgress);
        return buffer;
    }

    async #tryGetCached(url) {
        if (!("caches" in globalThis)) {
            return null;
        }

        try {
            const cache = await globalThis.caches.open(this.#cacheName);
            const response = await cache.match(url);
            if (!response || !response.ok) {
                return null;
            }

            return await response.arrayBuffer();
        } catch {
            return null;
        }
    }

    async #fetchAndCache(url, onProgress) {
        const response = await fetch(url);
        if (!response.ok) {
            throw new Error(`Failed to download model asset from ${url}: HTTP ${response.status}`);
        }

        const totalBytes = Number(response.headers.get("content-length")) || 0;
        let buffer;

        if (response.body && onProgress && totalBytes > 0) {
            buffer = await this.#readStreamWithProgress(response.body, totalBytes, onProgress);
        } else {
            buffer = await response.arrayBuffer();
        }

        if ("caches" in globalThis) {
            try {
                const cache = await globalThis.caches.open(this.#cacheName);
                const cacheResponse = new Response(buffer.slice(0), {
                    headers: {
                        "content-type": "application/octet-stream",
                    },
                });
                await cache.put(url, cacheResponse);
            } catch {
                // Storage quota exceeded or private mode restriction
            }
        }

        return buffer;
    }
    async #readStreamWithProgress(body, totalBytes, onProgress) {
        const reader = body.getReader();
        const chunks = [];
        let receivedBytes = 0;

        while (true) {
            const { done, value } = await reader.read();
            if (done) {
                break;
            }

            chunks.push(value);
            receivedBytes += value.byteLength;
            onProgress(receivedBytes, totalBytes);
        }

        const buffer = new Uint8Array(receivedBytes);
        let offset = 0;
        for (const chunk of chunks) {
            buffer.set(chunk, offset);
            offset += chunk.byteLength;
        }

        return buffer.buffer;
    }

    async clearCache() {
        if (!("caches" in globalThis)) {
            return false;
        }

        return await globalThis.caches.delete(this.#cacheName);
    }
}

export class OrtDriver {
    #ort;
    #cdnUrl;
    #wasmPath;

    constructor(options = {}) {
        this.#ort = options.ort || null;
        this.#cdnUrl = options.cdnUrl || ORT_CDN_URL;
        this.#wasmPath = options.wasmPath || ORT_WASM_PATH;
    }

    async getOrt() {
        if (this.#ort) {
            return this.#ort;
        }

        if (globalThis.ort) {
            this.#ort = globalThis.ort;
            this.#configureWasm(this.#ort);
            return this.#ort;
        }

        const loaded = await import(/* webpackIgnore: true */ this.#cdnUrl);
        this.#ort = loaded.default || loaded;
        this.#configureWasm(this.#ort);
        return this.#ort;
    }

    #configureWasm(ort) {
        if (!ort?.env?.wasm) {
            return;
        }

        if (!ort.env.wasm.wasmPaths) {
            ort.env.wasm.wasmPaths = this.#wasmPath;
        }

        const cores = navigator.hardwareConcurrency || 2;
        ort.env.wasm.numThreads = Math.min(4, cores);
    }

    async createSession(ort, bufferOrUrl, preferredDevice = ExecutionDevice.Auto) {
        if (preferredDevice === ExecutionDevice.WebGpu) {
            return await this.#tryWebGpuThenWasm(ort, bufferOrUrl, true);
        }

        if (preferredDevice === ExecutionDevice.Wasm) {
            return await this.#createWasmSession(ort, bufferOrUrl);
        }

        return await this.#tryWebGpuThenWasm(ort, bufferOrUrl, false);
    }

    async #tryWebGpuThenWasm(ort, bufferOrUrl, strictGpu) {
        try {
            const session = await ort.InferenceSession.create(bufferOrUrl, {
                executionProviders: [ExecutionDevice.WebGpu],
            });
            return { session, device: ExecutionDevice.WebGpu };
        } catch (gpuError) {
            if (strictGpu) {
                throw new Error(`WebGPU initialization failed: ${gpuError.message}`);
            }

            const wasmResult = await this.#createWasmSession(ort, bufferOrUrl);
            return wasmResult;
        }
    }

    async #createWasmSession(ort, bufferOrUrl) {
        const session = await ort.InferenceSession.create(bufferOrUrl, {
            executionProviders: [ExecutionDevice.Wasm],
        });
        return { session, device: ExecutionDevice.Wasm };
    }
}

export class ImageProcessor {
    prepareTensor(source, ort) {
        const { width: origWidth, height: origHeight } = this.getSourceDimensions(source);
        if (origWidth <= 0 || origHeight <= 0) {
            throw new Error("Invalid image source dimensions.");
        }

        const scale = TARGET_SIZE / Math.max(origWidth, origHeight);
        const scaledWidth = Math.round(origWidth * scale);
        const scaledHeight = Math.round(origHeight * scale);

        const canvas = this.#createResizedCanvas(source, scaledWidth, scaledHeight);
        const ctx = canvas.getContext("2d");
        const imageData = ctx.getImageData(0, 0, scaledWidth, scaledHeight);
        const pixels = imageData.data;

        const totalPixels = scaledWidth * scaledHeight;
        const tensorData = new Float32Array(totalPixels * CHANNELS);

        for (let i = 0, j = 0; i < pixels.length; i += 4, j += 3) {
            tensorData[j] = pixels[i];
            tensorData[j + 1] = pixels[i + 1];
            tensorData[j + 2] = pixels[i + 2];
        }

        const tensor = new ort.Tensor("float32", tensorData, [scaledHeight, scaledWidth, CHANNELS]);
        return {
            tensor,
            origWidth,
            origHeight,
            scale,
            scaledWidth,
            scaledHeight,
        };
    }

    getSourceDimensions(source) {
        if (!source) {
            return { width: 0, height: 0 };
        }

        if (typeof HTMLVideoElement !== "undefined" && source instanceof HTMLVideoElement) {
            return { width: source.videoWidth, height: source.videoHeight };
        }

        if (
            (typeof HTMLCanvasElement !== "undefined" && source instanceof HTMLCanvasElement) ||
            (typeof ImageData !== "undefined" && source instanceof ImageData) ||
            (typeof ImageBitmap !== "undefined" && source instanceof ImageBitmap)
        ) {
            return { width: source.width, height: source.height };
        }

        if (typeof source.videoWidth === "number" && typeof source.videoHeight === "number") {
            return { width: source.videoWidth, height: source.videoHeight };
        }

        if (typeof source.width === "number" && typeof source.height === "number") {
            return { width: source.width, height: source.height };
        }

        return { width: 0, height: 0 };
    }

    #createResizedCanvas(source, width, height) {
        const canvas = document.createElement("canvas");
        canvas.width = width;
        canvas.height = height;

        const ctx = canvas.getContext("2d");
        ctx.drawImage(source, 0, 0, width, height);
        return canvas;
    }
}

export class ContourExtractor {
    processMask(maskData, maskWidth, maskHeight, confidence = 1.0) {
        const totalPixels = maskWidth * maskHeight;
        const binaryMask = new Uint8Array(totalPixels);

        let minX = maskWidth;
        let maxX = -1;
        let minY = maskHeight;
        let maxY = -1;
        let foregroundCount = 0;

        for (let y = 0; y < maskHeight; y++) {
            const rowOffset = y * maskWidth;
            for (let x = 0; x < maskWidth; x++) {
                const idx = rowOffset + x;
                if (maskData[idx] > MASK_THRESHOLD) {
                    binaryMask[idx] = 1;
                    foregroundCount++;

                    if (x < minX) {
                        minX = x;
                    }
                    if (x > maxX) {
                        maxX = x;
                    }
                    if (y < minY) {
                        minY = y;
                    }
                    if (y > maxY) {
                        maxY = y;
                    }
                }
            }
        }

        if (foregroundCount === 0) {
            return {
                polygon: [],
                bounds: { x: 0, y: 0, width: 0, height: 0 },
                confidence,
                binaryMask,
                maskWidth,
                maskHeight,
            };
        }

        const rawContour = this.#traceBorder(binaryMask, maskWidth, maskHeight, minX, minY);
        const simplified = this.#simplifyRdp(rawContour, SIMPLIFICATION_EPSILON);

        const polygon = simplified.map((p) => ({
            x: Math.max(0, Math.min(1, p.x / maskWidth)),
            y: Math.max(0, Math.min(1, p.y / maskHeight)),
        }));

        const bounds = {
            x: minX / maskWidth,
            y: minY / maskHeight,
            width: (maxX - minX + 1) / maskWidth,
            height: (maxY - minY + 1) / maskHeight,
        };

        return {
            polygon,
            bounds,
            confidence,
            binaryMask,
            maskWidth,
            maskHeight,
        };
    }

    #traceBorder(binaryMask, width, height, startX, startY) {
        let firstX = -1;
        let firstY = -1;

        for (let y = startY; y < height; y++) {
            for (let x = 0; x < width; x++) {
                if (binaryMask[y * width + x] === 1) {
                    firstX = x;
                    firstY = y;
                    break;
                }
            }
            if (firstX !== -1) {
                break;
            }
        }

        if (firstX === -1) {
            return [];
        }

        const contour = [];
        let currX = firstX;
        let currY = firstY;
        let enterDir = 0;
        const maxSteps = width * height * 2;
        let steps = 0;

        do {
            contour.push({ x: currX, y: currY });
            let foundNext = false;

            const checkStart = (enterDir + 4 + 1) % 8;
            for (let i = 0; i < 8; i++) {
                const dir = (checkStart + i) % 8;
                const nextX = currX + NEIGHBOR_DX[dir];
                const nextY = currY + NEIGHBOR_DY[dir];

                if (nextX < 0 || nextX >= width || nextY < 0 || nextY >= height) {
                    continue;
                }

                if (binaryMask[nextY * width + nextX] === 1) {
                    currX = nextX;
                    currY = nextY;
                    enterDir = dir;
                    foundNext = true;
                    break;
                }
            }

            if (!foundNext) {
                break;
            }

            steps++;
            if (steps > maxSteps) {
                break;
            }
        } while (currX !== firstX || currY !== firstY);

        return contour;
    }

    #simplifyRdp(points, epsilon) {
        if (points.length <= 4) {
            return points;
        }

        let maxDist = 0;
        let maxIdx = 0;
        const last = points.length - 1;

        for (let i = 1; i < last; i++) {
            const dist = this.#pointToLineDist(points[i], points[0], points[last]);
            if (dist > maxDist) {
                maxDist = dist;
                maxIdx = i;
            }
        }

        if (maxDist > epsilon) {
            const left = this.#simplifyRdp(points.slice(0, maxIdx + 1), epsilon);
            const right = this.#simplifyRdp(points.slice(maxIdx), epsilon);
            return left.slice(0, -1).concat(right);
        }

        return [points[0], points[last]];
    }

    #pointToLineDist(point, start, end) {
        const dx = end.x - start.x;
        const dy = end.y - start.y;
        const lenSq = dx * dx + dy * dy;

        if (lenSq === 0) {
            const px = point.x - start.x;
            const py = point.y - start.y;
            return Math.sqrt(px * px + py * py);
        }

        const numerator = Math.abs(dy * point.x - dx * point.y + end.x * start.y - end.y * start.x);
        return numerator / Math.sqrt(lenSq);
    }
}

export class MobileSamEngine {
    #storage;
    #driver;
    #processor;
    #extractor;
    #state = EngineState.Uninitialized;
    #initPromise = null;

    #ort = null;
    #encoderSession = null;
    #decoderSession = null;
    #activeDevice = null;

    #currentEmbeddings = null;
    #currentMeta = null;
    #currentMaskPrior = null;
    constructor(options = {}) {
        this.#storage = options.storage || new ModelStorage();
        this.#driver = options.driver || new OrtDriver(options);
        this.#processor = options.processor || new ImageProcessor();
        this.#extractor = options.extractor || new ContourExtractor();
    }

    get state() {
        return this.#state;
    }

    get activeDevice() {
        return this.#activeDevice;
    }

    async init(options = {}) {
        if (this.#state === EngineState.Ready) {
            return this.getStatus();
        }

        if (this.#state === EngineState.Loading && this.#initPromise) {
            return await this.#initPromise;
        }

        this.#state = EngineState.Loading;
        this.#initPromise = this.#doInit(options);

        try {
            return await this.#initPromise;
        } finally {
            this.#initPromise = null;
        }
    }

    async waitForReady() {
        if (this.#state === EngineState.Ready) {
            return;
        }

        if (this.#initPromise) {
            await this.#initPromise;
            return;
        }

        await this.init();
    }

    async #doInit(options) {
        try {
            this.#ort = await this.#driver.getOrt();

            const baseUrl = options.modelBaseUrl || DEFAULT_MODEL_BASE_URL;
            const encoderUrl =
                options.encoderUrl ||
                new URL(options.encoderPath || DEFAULT_ENCODER_PATH, baseUrl).href;
            const decoderUrl =
                options.decoderUrl ||
                new URL(options.decoderPath || DEFAULT_DECODER_PATH, baseUrl).href;
            const preferredDevice = options.device || ExecutionDevice.Auto;

            const [encoderBuffer, decoderBuffer] = await Promise.all([
                this.#storage.loadBuffer(encoderUrl, options.onEncoderProgress),
                this.#storage.loadBuffer(decoderUrl, options.onDecoderProgress),
            ]);

            const [encoderResult, decoderResult] = await Promise.all([
                this.#driver.createSession(this.#ort, encoderBuffer, preferredDevice),
                this.#driver.createSession(this.#ort, decoderBuffer, preferredDevice),
            ]);

            this.#encoderSession = encoderResult.session;
            this.#decoderSession = decoderResult.session;
            this.#activeDevice = encoderResult.device;
            this.#state = EngineState.Ready;

            return this.getStatus();
        } catch (error) {
            this.#state = EngineState.Error;
            throw error;
        }
    }

    async encodeImage(imageSource) {
        this.#ensureReady();

        this.#state = EngineState.Encoding;
        const startTime = performance.now();

        try {
            const prep = this.#processor.prepareTensor(imageSource, this.#ort);
            const feeds = { input_image: prep.tensor };
            const results = await this.#encoderSession.run(feeds);

            this.#currentEmbeddings = results.image_embeddings;
            this.#currentMeta = prep;
            this.#currentMaskPrior = null;
            this.#state = EngineState.Ready;

            const elapsedMs = performance.now() - startTime;
            return {
                elapsedMs,
                device: this.#activeDevice,
                width: prep.origWidth,
                height: prep.origHeight,
            };
        } catch (error) {
            this.#state = EngineState.Ready;
            throw error;
        }
    }

    async decodePoints(points, options = {}) {
        this.#ensureReady();
        if (!this.#currentEmbeddings) {
            throw new Error("No image embeddings available. Call encodeImage first.");
        }

        if (!points || points.length === 0) {
            throw new Error("At least one point prompt is required.");
        }

        const startTime = performance.now();
        const pointCount = points.length;
        const coordData = new Float32Array(pointCount * 2);
        const labelData = new Float32Array(pointCount);

        const { scale, origWidth, origHeight } = this.#currentMeta;

        for (let i = 0; i < pointCount; i++) {
            const pt = points[i];
            const origX = pt.x <= 1.0 ? pt.x * origWidth : pt.x;
            const origY = pt.y <= 1.0 ? pt.y * origHeight : pt.y;

            coordData[i * 2] = origX * scale;
            coordData[i * 2 + 1] = origY * scale;
            labelData[i] = typeof pt.type === "number" ? pt.type : PromptType.Positive;
        }

        const pointCoordsTensor = new this.#ort.Tensor("float32", coordData, [1, pointCount, 2]);
        const pointLabelsTensor = new this.#ort.Tensor("float32", labelData, [1, pointCount]);

        const hasPrior = Boolean(this.#currentMaskPrior && options.usePrior !== false);
        const priorTensor = hasPrior
            ? this.#currentMaskPrior
            : new this.#ort.Tensor("float32", new Float32Array(LOW_RES_SIZE * LOW_RES_SIZE), [
                  1,
                  1,
                  LOW_RES_SIZE,
                  LOW_RES_SIZE,
              ]);
        const hasPriorTensor = new this.#ort.Tensor(
            "float32",
            new Float32Array([hasPrior ? 1.0 : 0.0]),
            [1],
        );
        const origSizeTensor = new this.#ort.Tensor(
            "float32",
            new Float32Array([origHeight, origWidth]),
            [2],
        );

        const feeds = {
            image_embeddings: this.#currentEmbeddings,
            point_coords: pointCoordsTensor,
            point_labels: pointLabelsTensor,
            mask_input: priorTensor,
            has_mask_input: hasPriorTensor,
            orig_im_size: origSizeTensor,
        };

        const results = await this.#decoderSession.run(feeds);
        const elapsedMs = performance.now() - startTime;

        if (results.low_res_masks) {
            this.#currentMaskPrior = results.low_res_masks;
        }

        const masksTensor = results.masks;
        const iouTensor = results.iou_predictions;
        const confidence = iouTensor ? Number(iouTensor.data[0]) : 1.0;

        const maskDims = masksTensor.dims;
        const maskHeight = maskDims[maskDims.length - 2];
        const maskWidth = maskDims[maskDims.length - 1];

        const processed = this.#extractor.processMask(
            masksTensor.data,
            maskWidth,
            maskHeight,
            confidence,
        );
        return {
            ...processed,
            elapsedMs,
        };
    }

    resetMaskPrior() {
        this.#currentMaskPrior = null;
    }

    getStatus() {
        return {
            state: this.#state,
            device: this.#activeDevice,
            hasEmbeddings: Boolean(this.#currentEmbeddings),
            width: this.#currentMeta?.origWidth || 0,
            height: this.#currentMeta?.origHeight || 0,
        };
    }

    dispose() {
        this.#state = EngineState.Disposed;

        if (this.#encoderSession) {
            this.#encoderSession.release?.();
            this.#encoderSession = null;
        }

        if (this.#decoderSession) {
            this.#decoderSession.release?.();
            this.#decoderSession = null;
        }

        if (this.#currentEmbeddings) {
            this.#currentEmbeddings.dispose?.();
            this.#currentEmbeddings = null;
        }

        this.#currentMaskPrior = null;
        this.#currentMeta = null;
        this.#ort = null;
    }

    #ensureReady() {
        if (this.#state !== EngineState.Ready) {
            throw new Error(`MobileSAM engine is not ready (current state: ${this.#state}).`);
        }
    }
}

// Module-level singleton instance for Blazor interop
let defaultEngine = null;

export async function initEngine(options = {}) {
    if (!defaultEngine || defaultEngine.state === EngineState.Disposed) {
        defaultEngine = new MobileSamEngine();
    }
    return await defaultEngine.init(options);
}

export async function encodeFrame(source) {
    if (!defaultEngine || defaultEngine.state === EngineState.Disposed) {
        defaultEngine = new MobileSamEngine();
        await defaultEngine.init();
    } else if (defaultEngine.state === EngineState.Loading) {
        await defaultEngine.waitForReady();
    } else if (defaultEngine.state === EngineState.Uninitialized) {
        await defaultEngine.init();
    }
    return await defaultEngine.encodeImage(source);
}

export async function decodePoint(x, y, type = PromptType.Positive) {
    return await decodeMultiPoints([{ x, y, type }]);
}

export async function decodeMultiPoints(points, options = {}) {
    if (!defaultEngine) {
        throw new Error("MobileSAM engine is not initialized. Call initEngine first.");
    }
    return await defaultEngine.decodePoints(points, options);
}

export function resetPrior() {
    defaultEngine?.resetMaskPrior();
}

export function getEngineStatus() {
    return defaultEngine ? defaultEngine.getStatus() : { state: EngineState.Uninitialized };
}

export function disposeEngine() {
    if (defaultEngine) {
        defaultEngine.dispose();
        defaultEngine = null;
    }
}
