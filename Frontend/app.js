const API_ENDPOINT = "/api/stats/overview";
const REFRESH_INTERVAL_MS = 10_000;
const OFFLINE_THRESHOLD_MS = 2 * 60 * 1000;

const summaryCardsEl = document.getElementById("summary-cards");
const workerTableBody = document.getElementById("worker-table-body");
const rangeTableBody = document.getElementById("range-table-body");
const puzzleTableBody = document.getElementById("puzzle-table-body");
const workerCountEl = document.getElementById("worker-count");
const rangeCountEl = document.getElementById("range-count");
const statusMessageEl = document.getElementById("status-message");
const lastUpdatedEl = document.getElementById("last-updated");

async function fetchStats() {
    const response = await fetch(API_ENDPOINT, { cache: "no-store" });
    if (!response.ok) {
        throw new Error(`Failed to fetch stats (${response.status})`);
    }
    return response.json();
}

function renderSummary(stats) {
    const totalRanges = stats.puzzles?.reduce((sum, puzzle) => sum + puzzle.rangesTotal, 0) ?? 0;
    const completedRanges = stats.puzzles?.reduce((sum, puzzle) => sum + puzzle.rangesCompleted, 0) ?? 0;
    const overallProgress = totalRanges > 0 ? (completedRanges / totalRanges) * 100 : 0;
    const puzzlesOnline = stats.puzzles?.length ?? 0;
    const keysFound = stats.puzzles?.reduce((sum, puzzle) => sum + puzzle.keysFound, 0) ?? 0;

    const cards = [
        {
            title: "Progress",
            value: `${overallProgress.toFixed(2)}%`,
            subtitle: `${completedRanges.toLocaleString()} / ${totalRanges.toLocaleString()} ranges`
        },
        {
            title: "Active Puzzles",
            value: puzzlesOnline,
            subtitle: "enabled in coordinator"
        },
        {
            title: "Active Workers",
            value: stats.workersOnline,
            subtitle: `${stats.workers.length} registered`
        },
        {
            title: "Pool Speed",
            value: formatSpeed(stats.totalSpeedKeysPerSecond),
            subtitle: "keys per second"
        },
        {
            title: "Keys Found",
            value: keysFound.toLocaleString(),
            subtitle: "reported to coordinator"
        }
    ];

    summaryCardsEl.innerHTML = cards.map(card => `
        <article class="summary-card">
            <h3>${card.title}</h3>
            <p class="summary-value">${card.value}</p>
            <p class="summary-subtitle">${card.subtitle}</p>
        </article>
    `).join("");
}

function renderWorkers(workers) {
    if (!workers || workers.length === 0) {
        workerTableBody.innerHTML = `<tr><td colspan="10" class="empty">No worker data</td></tr>`;
        workerCountEl.textContent = "0 online";
        return;
    }

    const now = Date.now();
    const totalSpeed = workers
        .map(worker => Number.isFinite(worker.speedKeysPerSecond) ? worker.speedKeysPerSecond : 0)
        .reduce((sum, speed) => sum + speed, 0);

    const rows = workers.map(worker => {
        const lastSeen = new Date(worker.lastSeenUtc);
        const isOffline = now - lastSeen.getTime() > OFFLINE_THRESHOLD_MS;
        const progress = worker.currentRangeProgress ?? 0;
        const currentRange = worker.currentRange ?? "—";
        const status = worker.status ?? (isOffline ? "Offline" : "Unknown");
        const speed = Number.isFinite(worker.speedKeysPerSecond) ? worker.speedKeysPerSecond : 0;
        const share = totalSpeed > 0 ? (speed / totalSpeed) * 100 : 0;

        return `
            <tr class="${isOffline ? "offline" : ""}">
                <td>${escapeHtml(worker.user)}</td>
                <td>${escapeHtml(worker.workerName ?? "—")}</td>
                <td>${escapeHtml(worker.applicationType)}</td>
                <td>${escapeHtml(worker.puzzleCode || "—")}</td>
                <td>${worker.cardsConnected}</td>
                <td><span class="speed-chip">${formatSpeed(speed)}</span></td>
                <td>${share.toFixed(2)}%</td>
                <td>${escapeHtml(currentRange)}</td>
                <td>
                    <div class="progress-bar">
                        <div class="progress-fill" style="width: ${Math.min(100, Math.max(0, progress)).toFixed(1)}%;"></div>
                    </div>
                    <div>${progress.toFixed(1)}% · ${status}</div>
                </td>
                <td>${formatRelativeTime(lastSeen)}</td>
            </tr>
        `;
    }).join("");

    workerTableBody.innerHTML = rows;

    const onlineCount = workers.filter(worker => {
        const lastSeen = new Date(worker.lastSeenUtc);
        return Date.now() - lastSeen.getTime() <= OFFLINE_THRESHOLD_MS;
    }).length;

    workerCountEl.textContent = `${onlineCount} online`;
}

function renderRanges(ranges) {
    if (!ranges || ranges.length === 0) {
        rangeTableBody.innerHTML = `<tr><td colspan="5" class="empty">No active ranges</td></tr>`;
        rangeCountEl.textContent = "0 in progress";
        return;
    }

    const rows = ranges.map(range => `
        <tr>
            <td>${escapeHtml(range.prefixStart)}</td>
            <td>${escapeHtml(range.prefixEnd)}</td>
            <td>${escapeHtml(range.puzzleCode ?? "—")}</td>
            <td>
                <div class="progress-bar">
                    <div class="progress-fill" style="width: ${Math.min(100, Math.max(0, range.progressPercent)).toFixed(1)}%;"></div>
                </div>
                <div>${range.progressPercent.toFixed(1)}%</div>
            </td>
            <td>${escapeHtml(range.assignedTo ?? "—")}</td>
            <td>${range.lastUpdateUtc ? new Date(range.lastUpdateUtc).toISOString().replace("T", " ").replace("Z", " UTC") : "—"}</td>
        </tr>
    `).join("");

    rangeTableBody.innerHTML = rows;
    rangeCountEl.textContent = `${ranges.length} in progress`;
}

function renderPuzzles(puzzles) {
    if (!puzzles || puzzles.length === 0) {
        puzzleTableBody.innerHTML = `<tr><td colspan="5" class="empty">No puzzle data</td></tr>`;
        return;
    }

    const rows = puzzles.map(puzzle => `
        <tr>
            <td>${escapeHtml(puzzle.code)}</td>
            <td>${escapeHtml(puzzle.displayName)}</td>
            <td>
                <div class="progress-bar">
                    <div class="progress-fill" style="width: ${Math.min(100, Math.max(0, puzzle.percentageSearched)).toFixed(1)}%;"></div>
                </div>
                <div>${puzzle.percentageSearched.toFixed(2)}%</div>
            </td>
            <td>${puzzle.rangesCompleted.toLocaleString()} / ${puzzle.rangesTotal.toLocaleString()}</td>
            <td>${puzzle.keysFound.toLocaleString()}</td>
        </tr>
    `).join("");

    puzzleTableBody.innerHTML = rows;
}

function updateLastUpdated(timestamp) {
    if (!timestamp) {
        lastUpdatedEl.textContent = "Never";
        return;
    }
    const date = new Date(timestamp);
    lastUpdatedEl.textContent = `${date.toLocaleString()} (${formatRelativeTime(date)})`;
}

function showStatus(message, isError = false) {
    if (!message) {
        statusMessageEl.classList.add("hidden");
        statusMessageEl.textContent = "";
        return;
    }
    statusMessageEl.textContent = message;
    statusMessageEl.classList.toggle("hidden", false);
    statusMessageEl.classList.toggle("error", Boolean(isError));
}

function formatSpeed(value) {
    if (!Number.isFinite(value) || value <= 0) {
        return "0";
    }

    const units = ["", "K", "M", "G", "T", "P"];
    let unitIndex = 0;
    let speed = value;
    while (speed >= 1000 && unitIndex < units.length - 1) {
        speed /= 1000;
        unitIndex += 1;
    }
    const unitLabel = units[unitIndex] ? `${units[unitIndex]} keys/s` : "keys/s";
    return `${speed.toFixed(speed >= 100 ? 0 : 2)} ${unitLabel}`;
}

function formatRelativeTime(date) {
    const diffMs = Date.now() - date.getTime();
    if (!Number.isFinite(diffMs)) {
        return "unknown";
    }
    const diffSec = Math.floor(diffMs / 1000);
    if (diffSec < 5) {
        return "just now";
    }
    if (diffSec < 60) {
        return `${diffSec}s ago`;
    }
    const diffMin = Math.floor(diffSec / 60);
    if (diffMin < 60) {
        return `${diffMin}m ago`;
    }
    const diffHours = Math.floor(diffMin / 60);
    if (diffHours < 24) {
        return `${diffHours}h ago`;
    }
    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays}d ago`;
}

function escapeHtml(value) {
    if (value === null || value === undefined) {
        return "";
    }
    return String(value)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

async function refresh() {
    try {
        showStatus(`Fetching data from ${API_ENDPOINT} ...`);
        const stats = await fetchStats();
        renderSummary(stats);
        renderWorkers(stats.workers);
        renderRanges(stats.activeRanges);
        renderPuzzles(stats.puzzles);
        updateLastUpdated(stats.generatedAtUtc);
        const puzzleNames = (stats.puzzles ?? []).map(p => p.code).join(", ");
        showStatus(`Tracking ${stats.puzzles?.length ?? 0} puzzles${puzzleNames ? ` (${puzzleNames})` : ""}.`);
    } catch (error) {
        console.error(error);
        showStatus(error.message, true);
    }
}

document.addEventListener("DOMContentLoaded", () => {
    refresh();
    setInterval(refresh, REFRESH_INTERVAL_MS);
});
