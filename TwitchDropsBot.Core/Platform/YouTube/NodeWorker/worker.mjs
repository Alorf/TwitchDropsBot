/**
 * YouTube Node Worker
 *
 * Uses the youtubei.js library (https://github.com/LuanRT/YouTube.js) to call
 * YouTube's private InnerTube API.
 *
 * Usage:
 *   node worker.mjs live   <@handle|UCxxx>   → { "videoId": "..." } or { "videoId": null }
 *   node worker.mjs islive <videoId>          → { "isLive": true|false }
 *
 * On error, exits with code 1 and writes { "error": "..." } to stdout.
 */

import { Innertube } from 'youtubei.js';

const [, , action, arg] = process.argv;

if (!action || !arg) {
    writeError('Usage: node worker.mjs <live|islive> <arg>');
    process.exit(1);
}

let yt;
try {
    yt = await Innertube.create({
        generate_session_locally: true,
        retrieve_player: false,
    });
} catch (err) {
    writeError(`Failed to create Innertube session: ${err.message}`);
    process.exit(1);
}

try {
    if (action === 'live') {
        const handle = normalizeHandle(arg);
        const res = await yt.actions.execute('/browse', { browseId: handle });

        if (!res.success) {
            writeError(`/browse returned HTTP ${res.status_code}`);
            process.exit(1);
        }

        const videoId = findLiveVideoId(res.data);
        process.stdout.write(JSON.stringify({ videoId: videoId ?? null }));

    } else if (action === 'islive') {
        const res = await yt.actions.execute('/player', {
            videoId: arg,
            contentCheckOk: true,
            racyCheckOk: true,
        });

        if (!res.success) {
            writeError(`/player returned HTTP ${res.status_code}`);
            process.exit(1);
        }

        const details = res.data?.videoDetails;
        const isLive =
            details?.isLive === true ||
            (details?.isLiveContent === true && details?.lengthSeconds === '0');

        process.stdout.write(JSON.stringify({ isLive: !!isLive }));

    } else {
        writeError(`Unknown action: ${action}`);
        process.exit(1);
    }
} catch (err) {
    writeError(err.message);
    process.exit(1);
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Ensures the handle is prefixed with '@' as expected by InnerTube's /browse
 * endpoint. UC… channel IDs are passed through unchanged.
 */
function normalizeHandle(handle) {
    if (handle.startsWith('UC') && handle.length > 10) return handle;
    return handle.startsWith('@') ? handle : '@' + handle;
}

/**
 * Depth-first search over the raw InnerTube JSON object.
 * Returns the videoId of the first videoRenderer that carries a LIVE badge,
 * or null when no live video is found.
 */
function findLiveVideoId(node) {
    if (node === null || typeof node !== 'object') return null;

    if (Array.isArray(node)) {
        for (const item of node) {
            const result = findLiveVideoId(item);
            if (result !== null) return result;
        }
        return null;
    }

    // Check current object: if it has a videoId and a live indicator, return it.
    if (typeof node.videoId === 'string' && isRendererLive(node)) {
        return node.videoId;
    }

    for (const value of Object.values(node)) {
        const result = findLiveVideoId(value);
        if (result !== null) return result;
    }

    return null;
}

/**
 * Returns true when the video renderer object carries any recognised live
 * indicator: a time-status overlay with style "LIVE", or a
 * "BADGE_STYLE_TYPE_LIVE_NOW" metadata badge.
 */
function isRendererLive(renderer) {
    // thumbnailOverlays: [{ thumbnailOverlayTimeStatusRenderer: { style: "LIVE" } }]
    if (Array.isArray(renderer.thumbnailOverlays)) {
        for (const overlay of renderer.thumbnailOverlays) {
            if (overlay?.thumbnailOverlayTimeStatusRenderer?.style === 'LIVE') {
                return true;
            }
        }
    }

    // badges: [{ metadataBadgeRenderer: { style: "BADGE_STYLE_TYPE_LIVE_NOW" } }]
    if (Array.isArray(renderer.badges)) {
        for (const badge of renderer.badges) {
            if (badge?.metadataBadgeRenderer?.style === 'BADGE_STYLE_TYPE_LIVE_NOW') {
                return true;
            }
        }
    }

    return false;
}

function writeError(message) {
    process.stdout.write(JSON.stringify({ error: message }));
}
