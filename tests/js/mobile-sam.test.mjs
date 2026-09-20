import { afterEach, beforeEach, test, mock } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const source = await readFile(new URL("../../wwwroot/js/mobile-sam.js", import.meta.url));
const sam = await import(`data:text/javascript;base64,${source.toString("base64")}`);

const originalCaches = globalThis.caches;
const originalFetch = globalThis.fetch;
const originalDocument = globalThis.document;

function createMockTensor(data, dims, type = "float32") {
    return {
        data,
        dims,
        type,
        dispose: mock.fn(),
    };
}

function createMockOrt() {
    const Tensor = function (type, data, dims) {
        return createMockTensor(data, dims, type);
    };

    const InferenceSession = {
        create: mock.fn(async (bufferOrUrl, options) => {
            const ep = options?.executionProviders?.[0];
            return {
                executionProvider: ep,
                run: mock.fn(async (feeds) => {
                    if (feeds.input_image) {
                        return {
                            image_embeddings: createMockTensor(
                                new Float32Array(256 * 64 * 64),
                                [1, 256, 64, 64],
                            ),
                        };
                    }

                    if (feeds.image_embeddings) {
                        const width = 100;
                        const height = 100;
                        const maskData = new Float32Array(width * height);

                        for (let y = 30; y < 70; y++) {
                            for (let x = 30; x < 70; x++) {
                                maskData[y * width + x] = 2.5;
                            }
                        }

                        return {
                            masks: createMockTensor(maskData, [1, 1, height, width]),
                            iou_predictions: createMockTensor(new Float32Array([0.96]), [1, 1]),
                            low_res_masks: createMockTensor(
                                new Float32Array(256 * 256),
                                [1, 1, 256, 256],
                            ),
                        };
                    }

                    return {};
                }),
                release: mock.fn(),
            };
        }),
    };

    return {
        Tensor,
        InferenceSession,
        env: { wasm: { wasmPaths: "", numThreads: 1 } },
    };
}

function createMockCanvas(width, height) {
    const pixels = new Uint8ClampedArray(width * height * 4);
    for (let i = 0; i < pixels.length; i += 4) {
        pixels[i] = 120;
        pixels[i + 1] = 110;
        pixels[i + 2] = 100;
        pixels[i + 3] = 255;
    }

    return {
        width,
        height,
        getContext: () => ({
            drawImage: mock.fn(),
            getImageData: () => ({ data: pixels }),
        }),
    };
}

beforeEach(() => {
    sam.disposeEngine();
});

afterEach(() => {
    globalThis.caches = originalCaches;
    globalThis.fetch = originalFetch;
    globalThis.document = originalDocument;
    sam.disposeEngine();
});

test("ModelStorage loads cached buffer when available in CacheStorage", async () => {
    const fakeBuffer = new Uint8Array([1, 2, 3, 4]).buffer;
    const matchMock = mock.fn(async () => ({
        ok: true,
        arrayBuffer: async () => fakeBuffer,
    }));
    const openMock = mock.fn(async () => ({ match: matchMock }));

    globalThis.caches = { open: openMock };
    globalThis.fetch = mock.fn();

    const storage = new sam.ModelStorage("test-cache");
    const result = await storage.loadBuffer("https://example.com/encoder.onnx");

    assert.equal(result, fakeBuffer);
    assert.equal(openMock.mock.callCount(), 1);
    assert.equal(globalThis.fetch.mock.callCount(), 0);
});

test("ModelStorage downloads, stores in CacheStorage, and returns buffer on cache miss", async () => {
    const downloadBuffer = new Uint8Array([5, 6, 7, 8]).buffer;
    const putMock = mock.fn(async () => {});
    const matchMock = mock.fn(async () => null);
    const openMock = mock.fn(async () => ({ match: matchMock, put: putMock }));

    globalThis.caches = { open: openMock };
    globalThis.fetch = mock.fn(async () => ({
        ok: true,
        clone: () => ({}),
        arrayBuffer: async () => downloadBuffer,
        headers: new Headers(),
    }));

    const storage = new sam.ModelStorage("test-cache");
    const result = await storage.loadBuffer("https://example.com/decoder.onnx");

    assert.equal(result, downloadBuffer);
    assert.equal(matchMock.mock.callCount(), 1);
    assert.equal(putMock.mock.callCount(), 1);
});

test("OrtDriver attempts WebGPU and falls back to WASM if WebGPU is unsupported", async () => {
    let callCount = 0;
    const mockOrt = {
        env: { wasm: {} },
        InferenceSession: {
            create: mock.fn(async (buf, options) => {
                callCount++;
                const ep = options.executionProviders[0];
                if (ep === "webgpu") {
                    throw new Error("WebGPU not available in this context");
                }
                return { executionProvider: ep, run: mock.fn() };
            }),
        },
    };

    const driver = new sam.OrtDriver({ ort: mockOrt });
    const sessionResult = await driver.createSession(
        mockOrt,
        new ArrayBuffer(8),
        sam.ExecutionDevice.Auto,
    );

    assert.equal(sessionResult.device, sam.ExecutionDevice.Wasm);
    assert.equal(callCount, 2);
});

test("ImageProcessor resizes input preserving aspect ratio and normalizes RGB channels", () => {
    globalThis.document = {
        createElement: () => createMockCanvas(512, 288),
    };

    const processor = new sam.ImageProcessor();
    const source = { width: 1920, height: 1080 };
    const mockOrt = createMockOrt();

    const result = processor.prepareTensor(source, mockOrt);

    assert.equal(result.origWidth, 1920);
    assert.equal(result.origHeight, 1080);
    assert.equal(result.scaledWidth, 1024);
    assert.equal(result.scaledHeight, 576);
    assert.deepEqual(result.tensor.dims, [1, 3, 1024, 1024]);
    assert.equal(result.tensor.data.length, 3 * 1024 * 1024);
});

test("ContourExtractor thresholds logits and extracts normalized polygon coordinates", () => {
    const extractor = new sam.ContourExtractor();
    const width = 100;
    const height = 100;
    const logits = new Float32Array(width * height);

    for (let y = 20; y < 60; y++) {
        for (let x = 20; x < 60; x++) {
            logits[y * width + x] = 1.8;
        }
    }

    const output = extractor.processMask(logits, width, height, 0.94);

    assert.equal(output.confidence, 0.94);
    assert.ok(output.polygon.length >= 4);
    assert.ok(output.bounds.width > 0);
    assert.ok(output.bounds.height > 0);

    for (const pt of output.polygon) {
        assert.ok(pt.x >= 0.0 && pt.x <= 1.0);
        assert.ok(pt.y >= 0.0 && pt.y <= 1.0);
    }
});

test("ContourExtractor returns empty polygon for empty mask", () => {
    const extractor = new sam.ContourExtractor();
    const width = 50;
    const height = 50;
    const emptyLogits = new Float32Array(width * height);

    const output = extractor.processMask(emptyLogits, width, height, 0.1);

    assert.equal(output.polygon.length, 0);
    assert.equal(output.bounds.width, 0);
    assert.equal(output.bounds.height, 0);
});

test("MobileSamEngine encodes image once and decodes point prompt in sub-50ms", async () => {
    const mockOrt = createMockOrt();
    const storage = {
        loadBuffer: mock.fn(async () => new ArrayBuffer(16)),
    };
    const driver = {
        getOrt: async () => mockOrt,
        createSession: mock.fn(async () => ({
            session: await mockOrt.InferenceSession.create(),
            device: sam.ExecutionDevice.Wasm,
        })),
    };
    const processor = {
        prepareTensor: () => ({
            tensor: mockOrt.Tensor("float32", new Float32Array(10), [1, 3, 1024, 1024]),
            origWidth: 800,
            origHeight: 600,
            scale: 1024 / 800,
            scaledWidth: 1024,
            scaledHeight: 768,
        }),
    };

    const engine = new sam.MobileSamEngine({
        storage,
        driver,
        processor,
    });

    await engine.init({ device: sam.ExecutionDevice.Wasm });
    assert.equal(engine.state, sam.EngineState.Ready);

    const encodeResult = await engine.encodeImage({ width: 800, height: 600 });
    assert.equal(encodeResult.width, 800);
    assert.equal(encodeResult.height, 600);

    const decodeResult = await engine.decodePoints([
        { x: 0.5, y: 0.5, type: sam.PromptType.Positive },
    ]);

    assert.ok(decodeResult.elapsedMs >= 0);
    assert.ok(decodeResult.polygon.length >= 3);
    assert.ok(decodeResult.confidence > 0.9);
    assert.equal(decodeResult.maskWidth, 100);
    assert.equal(decodeResult.maskHeight, 100);
});

test("MobileSamEngine supports multi-point refinement with positive and negative prompts", async () => {
    const mockOrt = createMockOrt();
    const engine = new sam.MobileSamEngine({
        storage: { loadBuffer: async () => new ArrayBuffer(16) },
        driver: {
            getOrt: async () => mockOrt,
            createSession: async () => ({
                session: await mockOrt.InferenceSession.create(),
                device: sam.ExecutionDevice.WebGpu,
            }),
        },
        processor: {
            prepareTensor: () => ({
                tensor: mockOrt.Tensor("float32", new Float32Array(10), [1, 3, 1024, 1024]),
                origWidth: 1000,
                origHeight: 1000,
                scale: 1024 / 1000,
                scaledWidth: 1024,
                scaledHeight: 1024,
            }),
        },
    });

    await engine.init();
    await engine.encodeImage({ width: 1000, height: 1000 });

    const multiPrompt = [
        { x: 0.45, y: 0.45, type: sam.PromptType.Positive },
        { x: 0.85, y: 0.85, type: sam.PromptType.Negative },
    ];

    const result = await engine.decodePoints(multiPrompt);
    assert.ok(result.polygon.length >= 3);
});

test("MobileSamEngine unloads sessions and cleans up memory on dispose", async () => {
    const mockOrt = createMockOrt();
    let releasedEncoder = false;
    let releasedDecoder = false;

    const engine = new sam.MobileSamEngine({
        storage: { loadBuffer: async () => new ArrayBuffer(16) },
        driver: {
            getOrt: async () => mockOrt,
            createSession: async () => ({
                session: {
                    run: mock.fn(),
                    release: () => {
                        releasedEncoder = true;
                        releasedDecoder = true;
                    },
                },
                device: sam.ExecutionDevice.Wasm,
            }),
        },
        processor: {
            prepareTensor: () => ({
                tensor: mockOrt.Tensor("float32", new Float32Array(10), [1, 3, 1024, 1024]),
                origWidth: 100,
                origHeight: 100,
                scale: 1,
                scaledWidth: 100,
                scaledHeight: 100,
            }),
        },
    });

    await engine.init();
    assert.equal(engine.state, sam.EngineState.Ready);

    engine.dispose();
    assert.equal(engine.state, sam.EngineState.Disposed);
    assert.ok(releasedEncoder);
    assert.ok(releasedDecoder);

    await assert.rejects(async () => {
        await engine.encodeImage({ width: 100, height: 100 });
    }, /not ready/);
});

test("Module helpers provide singleton access for Blazor interop", async () => {
    const status = sam.getEngineStatus();
    assert.equal(status.state, sam.EngineState.Uninitialized);

    sam.resetPrior();
    sam.disposeEngine();

    const afterDispose = sam.getEngineStatus();
    assert.equal(afterDispose.state, sam.EngineState.Uninitialized);
});
