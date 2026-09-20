import { afterEach, beforeEach, test, mock } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

// Load the actual browser module without changing the tooling package to ESM.
const source = await readFile(new URL("../../wwwroot/js/video-scan.js", import.meta.url));
const scan = await import(`data:text/javascript;base64,${source.toString("base64")}`);
const originalDocument = globalThis.document;
const originalDotNet = globalThis.DotNet;
let urls, revoked, canvases;

class Video extends EventTarget {
    duration = 12;
    videoWidth = 1920;
    videoHeight = 1080;
    positions = [];
    load() {
        if (this.src) queueMicrotask(() => this.dispatchEvent(new Event("loadeddata")));
    }
    pause() {}
    removeAttribute(name) {
        delete this[name];
    }
    set currentTime(value) {
        this.positions.push(value);
        queueMicrotask(() => this.dispatchEvent(new Event("seeked")));
    }
}

const input = (overrides = {}) => ({ files: [{ name: "room.mp4", size: 1024, ...overrides }] });

beforeEach(() => {
    urls = [];
    revoked = [];
    canvases = [];
    mock.method(URL, "createObjectURL", () => {
        const url = `blob:fixture-${urls.length}`;
        urls.push(url);
        return url;
    });
    mock.method(URL, "revokeObjectURL", (url) => revoked.push(url));
    globalThis.DotNet = { createJSStreamReference: (blob) => blob };
    globalThis.document = {
        createElement(tag) {
            assert.equal(tag, "canvas");
            const canvas = {
                getContext: () => ({
                    drawImage: (...args) => {
                        canvas.drawArgs = args;
                    },
                }),
                toBlob(callback, type) {
                    callback(
                        new Blob([JSON.stringify({ width: this.width, height: this.height })], {
                            type,
                        }),
                    );
                },
            };
            canvases.push(canvas);
            return canvas;
        },
    };
});

afterEach(() => {
    mock.restoreAll();
    globalThis.document = originalDocument;
    globalThis.DotNet = originalDotNet;
});

test("samples at most six evenly spaced, resized JPEG frames; disposes every object URL", async () => {
    const video = new Video();
    assert.deepEqual(await scan.prepare(input(), video), [1, 3, 5, 7, 9, 11]);
    assert.deepEqual(video.positions, [1, 3, 5, 7, 9, 11]);
    const frame = scan.frameStream(video, 0);
    assert.equal(frame.type, "image/jpeg");
    assert.deepEqual(JSON.parse(await frame.text()), { width: 1280, height: 720 });
    assert.equal(scan.frameUrl(video, 0), "blob:fixture-1");
    scan.dispose(video);
    assert.deepEqual(revoked, urls);
    scan.dispose(video); // Cleanup is idempotent.
    assert.equal(revoked.length, 7);
});

test("short clips use one frame and are not upscaled", async () => {
    const video = new Video();
    video.duration = 0.4;
    video.videoWidth = 320;
    video.videoHeight = 240;
    assert.deepEqual(await scan.prepare(input(), video), [0.2]);
    assert.deepEqual(JSON.parse(await scan.frameStream(video, 0).text()), {
        width: 320,
        height: 240,
    });
    scan.dispose(video);
});

test("a selected region produces an item-sized crop; absent region returns the frame", async () => {
    const video = new Video();
    await scan.prepare(input(), video);
    const crop = await scan.cropStream(video, 0, { x: 0.25, y: 0.25, width: 0.25, height: 0.25 });
    assert.deepEqual(JSON.parse(await crop.text()), { width: 320, height: 180 });
    assert.deepEqual(canvases.at(-1).drawArgs.slice(1), [320, 180, 320, 180, 0, 0, 320, 180]);
    assert.equal(await scan.cropStream(video, 0, null), scan.frameStream(video, 0));
    scan.dispose(video);
});

test("rejects empty, unsupported and oversized files before any decoding", async () => {
    await assert.rejects(scan.prepare({ files: [] }, new Video()), /Choose a video first/);
    await assert.rejects(scan.prepare(input({ size: 0 }), new Video()), /Choose a video first/);
    await assert.rejects(
        scan.prepare(input({ name: "script.html" }), new Video()),
        /Choose an MP4/,
    );
    await assert.rejects(
        scan.prepare(input({ size: 100 * 1024 * 1024 + 1 }), new Video()),
        /100 MB/,
    );
    assert.equal(urls.length, 0);
});

test("rejects overlong or unknown durations and cleans up the source", async () => {
    for (const duration of [30.01, 0, Infinity, NaN]) {
        const video = new Video();
        video.duration = duration;
        await assert.rejects(scan.prepare(input(), video), /up to 30 seconds/);
    }
    assert.deepEqual(revoked, urls);
});

test("accepts the exact limits and caps long clips at six samples", async () => {
    const video = new Video();
    video.duration = 30;
    assert.equal((await scan.prepare(input({ size: 100 * 1024 * 1024 }), video)).length, 6);
    scan.dispose(video);
});

test("replacing a video revokes all prior source and frame URLs", async () => {
    const video = new Video();
    await scan.prepare(input(), video);
    await scan.prepare(input({ name: "second.webm" }), video);
    assert.deepEqual(revoked, urls.slice(0, 7));
    scan.dispose(video);
    assert.deepEqual(revoked, urls);
});

test("a decoder error is actionable and releases the source URL", async () => {
    const video = new Video();
    video.load = () => {
        if (video.src) queueMicrotask(() => video.dispatchEvent(new Event("error")));
    };
    await assert.rejects(scan.prepare(input(), video), /could not decode/);
    assert.deepEqual(revoked, urls);
});

test("disposing during frame encoding does not leak a new frame URL", async () => {
    const video = new Video();
    const createCanvas = document.createElement;
    document.createElement = (tag) => {
        const canvas = createCanvas(tag);
        const encode = canvas.toBlob;
        canvas.toBlob = function (...args) {
            scan.dispose(video);
            encode.apply(this, args);
        };
        return canvas;
    };
    await assert.rejects(scan.prepare(input(), video), /Scan cancelled/);
    assert.deepEqual(revoked, urls);
});
