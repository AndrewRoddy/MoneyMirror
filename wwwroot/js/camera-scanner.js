export const CameraFacing = Object.freeze({
    Environment: "environment",
    User: "user",
});

const DEFAULT_IDEAL_WIDTH = 1280;
const DEFAULT_IDEAL_HEIGHT = 720;
const JPEG_QUALITY = 0.9;

const activeStreams = new WeakMap();

export class CameraDriver {
    async start(video, facing = CameraFacing.Environment) {
        if (!navigator?.mediaDevices?.getUserMedia) {
            throw new Error("Camera streaming is not supported by this browser.");
        }

        this.stop(video);

        const constraints = {
            audio: false,
            video: {
                facingMode: { ideal: facing },
                width: { ideal: DEFAULT_IDEAL_WIDTH },
                height: { ideal: DEFAULT_IDEAL_HEIGHT },
            },
        };

        try {
            const stream = await navigator.mediaDevices.getUserMedia(constraints);
            activeStreams.set(video, stream);

            video.srcObject = stream;
            video.setAttribute("playsinline", "true");
            video.setAttribute("autoplay", "true");
            video.muted = true;

            await video.play();

            if (video.parentElement && video.videoWidth > 0 && video.videoHeight > 0) {
                video.parentElement.style.aspectRatio = `${video.videoWidth} / ${video.videoHeight}`;
            }

            return {
                facing,
                width: video.videoWidth || DEFAULT_IDEAL_WIDTH,
                height: video.videoHeight || DEFAULT_IDEAL_HEIGHT,
            };
        } catch (error) {
            throw new Error(this.#mapErrorMessage(error));
        }
    }

    #mapErrorMessage(error) {
        const name = error?.name || "";

        if (name === "NotAllowedError" || name === "PermissionDeniedError") {
            return "Camera access was denied. Grant camera permission in browser settings.";
        }

        if (name === "NotFoundError" || name === "DevicesNotFoundError") {
            return "No camera device found on this system.";
        }

        if (name === "NotReadableError" || name === "TrackStartError") {
            return "Camera is in use by another application or tab.";
        }

        if (name === "OverconstrainedError") {
            return "Requested camera settings are not supported on this device.";
        }

        return error?.message || "Failed to start camera feed.";
    }

    async switchFacing(video, currentFacing) {
        const nextFacing =
            currentFacing === CameraFacing.Environment
                ? CameraFacing.User
                : CameraFacing.Environment;

        const result = await this.start(video, nextFacing);
        return result;
    }

    stop(video) {
        const stream = activeStreams.get(video);
        if (!stream) {
            return;
        }

        for (const track of stream.getTracks()) {
            track.stop();
        }

        activeStreams.delete(video);

        if (video) {
            video.pause();
            video.srcObject = null;
        }
    }
}

export class FrameCapture {
    freeze(video, canvas) {
        if (!video) {
            throw new Error("Video element is required to freeze frame.");
        }

        const width = video.videoWidth || video.clientWidth || DEFAULT_IDEAL_WIDTH;
        const height = video.videoHeight || video.clientHeight || DEFAULT_IDEAL_HEIGHT;

        canvas.width = width;
        canvas.height = height;

        const ctx = canvas.getContext("2d");
        ctx.drawImage(video, 0, 0, width, height);

        video.pause();

        return { width, height };
    }

    async unfreeze(video) {
        if (video && video.srcObject) {
            await video.play();
        }
    }

    async toBlob(canvas, quality = JPEG_QUALITY) {
        return new Promise((resolve, reject) => {
            canvas.toBlob(
                (blob) => {
                    if (blob) {
                        resolve(blob);
                    } else {
                        reject(new Error("Failed to extract image blob from canvas."));
                    }
                },
                "image/jpeg",
                quality,
            );
        });
    }
}

export function normalizePoint(clientX, clientY, containerElement, videoElement) {
    if (!containerElement) {
        return { x: 0, y: 0 };
    }

    const rect = containerElement.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) {
        return { x: 0, y: 0 };
    }

    const video =
        videoElement || containerElement.querySelector?.("video") || containerElement.querySelector?.("canvas");
    const videoWidth = video?.videoWidth || video?.width || 0;
    const videoHeight = video?.videoHeight || video?.height || 0;

    if (videoWidth > 0 && videoHeight > 0) {
        const containerAspect = rect.width / rect.height;
        const videoAspect = videoWidth / videoHeight;

        let visibleW = rect.width;
        let visibleH = rect.height;
        let offsetX = 0;
        let offsetY = 0;

        if (videoAspect > containerAspect) {
            visibleH = rect.width / videoAspect;
            offsetY = (rect.height - visibleH) / 2;
        } else {
            visibleW = rect.height * videoAspect;
            offsetX = (rect.width - visibleW) / 2;
        }

        const relX = clientX - rect.left - offsetX;
        const relY = clientY - rect.top - offsetY;

        const x = Math.max(0, Math.min(1, relX / visibleW));
        const y = Math.max(0, Math.min(1, relY / visibleH));
        return { x, y };
    }

    const x = Math.max(0, Math.min(1, (clientX - rect.left) / rect.width));
    const y = Math.max(0, Math.min(1, (clientY - rect.top) / rect.height));

    return { x, y };
}

const defaultDriver = new CameraDriver();
const defaultCapture = new FrameCapture();

export async function startCamera(video, facing = CameraFacing.Environment) {
    return await defaultDriver.start(video, facing);
}

export async function switchCamera(video, currentFacing) {
    return await defaultDriver.switchFacing(video, currentFacing);
}

export function stopCamera(video) {
    defaultDriver.stop(video);
}

export function freezeFrame(video, canvas) {
    return defaultCapture.freeze(video, canvas);
}

export async function unfreezeFrame(video) {
    await defaultCapture.unfreeze(video);
}

export async function captureBlob(canvas) {
    return await defaultCapture.toBlob(canvas);
}

export async function getCanvasBlob(canvas) {
    return await defaultCapture.toBlob(canvas);
}
