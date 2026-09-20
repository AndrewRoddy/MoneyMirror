import { afterEach, beforeEach, test, mock } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const source = await readFile(new URL("../../wwwroot/js/camera-scanner.js", import.meta.url));
const camera = await import(`data:text/javascript;base64,${source.toString("base64")}`);

const originalDescriptor = Object.getOwnPropertyDescriptor(globalThis, "navigator");

function setMockNavigator(mediaDevices) {
    Object.defineProperty(globalThis, "navigator", {
        value: { mediaDevices },
        configurable: true,
        writable: true,
    });
}

function createMockTrack() {
    return {
        stop: mock.fn(),
    };
}

function createMockStream() {
    const tracks = [createMockTrack(), createMockTrack()];
    return {
        getTracks: () => tracks,
    };
}

function createMockVideo() {
    return {
        videoWidth: 1280,
        videoHeight: 720,
        srcObject: null,
        setAttribute: mock.fn(),
        play: mock.fn(async () => {}),
        pause: mock.fn(),
    };
}

function createMockCanvas() {
    return {
        width: 0,
        height: 0,
        getContext: () => ({
            drawImage: mock.fn(),
        }),
        toBlob: (cb) => cb(new Blob(["test-image"], { type: "image/jpeg" })),
    };
}

afterEach(() => {
    if (originalDescriptor) {
        Object.defineProperty(globalThis, "navigator", originalDescriptor);
    }
});

test("CameraDriver starts video stream with environment facing mode constraints", async () => {
    const mockStream = createMockStream();
    let requestedConstraints = null;

    setMockNavigator({
        getUserMedia: mock.fn(async (constraints) => {
            requestedConstraints = constraints;
            return mockStream;
        }),
    });

    const video = createMockVideo();
    const driver = new camera.CameraDriver();

    const result = await driver.start(video, camera.CameraFacing.Environment);

    assert.equal(result.facing, camera.CameraFacing.Environment);
    assert.equal(result.width, 1280);
    assert.equal(result.height, 720);
    assert.equal(video.srcObject, mockStream);
    assert.equal(video.setAttribute.mock.callCount(), 2);
    assert.equal(video.play.mock.callCount(), 1);
    assert.equal(requestedConstraints.video.facingMode.ideal, "environment");
});

test("CameraDriver switches facing mode between environment and user", async () => {
    const mockStream = createMockStream();
    const calls = [];

    setMockNavigator({
        getUserMedia: mock.fn(async (constraints) => {
            calls.push(constraints.video.facingMode.ideal);
            return mockStream;
        }),
    });

    const video = createMockVideo();
    const driver = new camera.CameraDriver();

    await driver.start(video, camera.CameraFacing.Environment);
    const switched = await driver.switchFacing(video, camera.CameraFacing.Environment);

    assert.equal(switched.facing, camera.CameraFacing.User);
    assert.deepEqual(calls, ["environment", "user"]);
});

test("CameraDriver maps permission denial to clear user guidance", async () => {
    const permissionError = new Error("Permission denied");
    permissionError.name = "NotAllowedError";

    setMockNavigator({
        getUserMedia: mock.fn(async () => {
            throw permissionError;
        }),
    });

    const video = createMockVideo();
    const driver = new camera.CameraDriver();

    await assert.rejects(async () => {
        await driver.start(video);
    }, /Camera access was denied/);
});

test("CameraDriver stops all stream tracks on stop", async () => {
    const mockStream = createMockStream();

    setMockNavigator({
        getUserMedia: mock.fn(async () => mockStream),
    });

    const video = createMockVideo();
    const driver = new camera.CameraDriver();

    await driver.start(video);
    driver.stop(video);

    for (const track of mockStream.getTracks()) {
        assert.equal(track.stop.mock.callCount(), 1);
    }
    assert.equal(video.srcObject, null);
    assert.equal(video.pause.mock.callCount(), 1);
});

test("FrameCapture draws video to canvas and pauses video on freeze", () => {
    const video = createMockVideo();
    const canvas = createMockCanvas();
    const capture = new camera.FrameCapture();

    const dimensions = capture.freeze(video, canvas);

    assert.equal(dimensions.width, 1280);
    assert.equal(dimensions.height, 720);
    assert.equal(canvas.width, 1280);
    assert.equal(canvas.height, 720);
    assert.equal(video.pause.mock.callCount(), 1);
});

test("normalizePoint converts viewport coordinates to clamped [0, 1] range", () => {
    const container = {
        getBoundingClientRect: () => ({
            left: 100,
            top: 200,
            width: 400,
            height: 300,
        }),
    };

    const center = camera.normalizePoint(300, 350, container);
    assert.equal(center.x, 0.5);
    assert.equal(center.y, 0.5);

    const topLeft = camera.normalizePoint(100, 200, container);
    assert.equal(topLeft.x, 0);
    assert.equal(topLeft.y, 0);

    const outside = camera.normalizePoint(600, 600, container);
    assert.equal(outside.x, 1);
    assert.equal(outside.y, 1);
});
