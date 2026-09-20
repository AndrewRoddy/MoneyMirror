const sessions = new WeakMap();
const MAX_BYTES = 100 * 1024 * 1024;
const MAX_SECONDS = 30;
const MAX_FRAMES = 6;

function waitFor(target, success, action) {
    return new Promise((resolve, reject) => {
        const finish = (error) => {
            clearTimeout(timer);
            target.removeEventListener(success, ok);
            target.removeEventListener("error", fail);
            target.removeEventListener("abort", abort);
            error ? reject(error) : resolve();
        };
        const ok = () => finish();
        const fail = () =>
            finish(
                new Error(
                    "This browser could not decode the video. Try an MP4 (H.264) or WebM recording.",
                ),
            );
        const abort = () =>
            finish(new Error("Video loading was interrupted. Please try again."));
        const timer = setTimeout(
            () => finish(new Error("Video decoding timed out. Try a shorter clip.")),
            15000,
        );
        target.addEventListener(success, ok, { once: true });
        target.addEventListener("error", fail, { once: true });
        target.addEventListener("abort", abort, { once: true });
        try {
            action();
        } catch (error) {
            finish(error);
        }
    });
}

function blobFromCanvas(canvas) {
    return new Promise((resolve, reject) =>
        canvas.toBlob(
            (blob) => (blob ? resolve(blob) : reject(new Error("Could not capture this frame."))),
            "image/jpeg",
            0.85,
        ),
    );
}

export async function prepare(input, video) {
    dispose(video);
    const file = input.files?.[0];
    if (!file || file.size === 0) throw new Error("Choose a video first.");
    if (file.size > MAX_BYTES) throw new Error("Choose a video smaller than 100 MB.");
    if (!/\.(mp4|webm|mov|m4v)$/i.test(file.name))
        throw new Error("Choose an MP4, WebM, MOV, or M4V video.");
    const session = { url: URL.createObjectURL(file), frames: [], cancelled: false };
    sessions.set(video, session);
    try {
        await waitFor(video, "loadeddata", () => {
            video.src = session.url;
            video.load();
        });
        if (
            !Number.isFinite(video.duration) ||
            video.duration <= 0 ||
            video.duration > MAX_SECONDS
        ) {
            throw new Error("Choose a video up to 30 seconds long.");
        }
        const count = Math.min(MAX_FRAMES, Math.max(1, Math.ceil(video.duration / 2)));
        const timestamps = [];
        for (let i = 0; i < count; i++) {
            if (session.cancelled) throw new Error("Scan cancelled.");
            const timestamp = (video.duration * (i + 0.5)) / count;
            await waitFor(video, "seeked", () => {
                video.currentTime = timestamp;
            });
            if (session.cancelled) throw new Error("Scan cancelled.");
            const canvas = document.createElement("canvas");
            const scale = Math.min(1, 1280 / Math.max(video.videoWidth, video.videoHeight));
            canvas.width = Math.max(1, Math.round(video.videoWidth * scale));
            canvas.height = Math.max(1, Math.round(video.videoHeight * scale));
            canvas.getContext("2d").drawImage(video, 0, 0, canvas.width, canvas.height);
            const blob = await blobFromCanvas(canvas);
            if (session.cancelled) throw new Error("Scan cancelled.");
            session.frames.push({ canvas, blob, url: URL.createObjectURL(blob) });
            timestamps.push(timestamp);
        }
        video.pause();
        return timestamps;
  } catch (error) {
    // An older decoding task must not dispose a replacement video's session.
    if (sessions.get(video) === session) dispose(video);
        throw error;
    }
}

export function frameUrl(video, index) {
    const session = sessions.get(video);
    if (!session) throw new Error("No active video session.");
    return session.frames[index].url;
}

export function frameStream(video, index) {
    const session = sessions.get(video);
    if (!session) throw new Error("No active video session.");
    return DotNet.createJSStreamReference(session.frames[index].blob);
}

export async function cropStream(video, index, region) {
    const session = sessions.get(video);
    if (!session) throw new Error("No active video session.");
    const source = session.frames[index];
    if (!region) return DotNet.createJSStreamReference(source.blob);
    const canvas = document.createElement("canvas");
    const x = Math.floor(region.x * source.canvas.width);
    const y = Math.floor(region.y * source.canvas.height);
    canvas.width = Math.max(
        1,
        Math.min(source.canvas.width - x, Math.ceil(region.width * source.canvas.width)),
    );
    canvas.height = Math.max(
        1,
        Math.min(source.canvas.height - y, Math.ceil(region.height * source.canvas.height)),
    );
    canvas
        .getContext("2d")
        .drawImage(
            source.canvas,
            x,
            y,
            canvas.width,
            canvas.height,
            0,
            0,
            canvas.width,
            canvas.height,
        );
    return DotNet.createJSStreamReference(await blobFromCanvas(canvas));
}

export function dispose(video) {
    const session = sessions.get(video);
    if (!session) return;
    session.cancelled = true;
    URL.revokeObjectURL(session.url);
    for (const frame of session.frames) URL.revokeObjectURL(frame.url);
    sessions.delete(video);
    video.pause();
    video.removeAttribute("src");
}
