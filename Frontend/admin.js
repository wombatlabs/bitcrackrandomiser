const API_BASE = "";
const PUZZLES_ENDPOINT = "/api/admin/puzzles";

const adminKeyInput = document.getElementById("admin-key");
const saveKeyButton = document.getElementById("save-key");
const refreshButton = document.getElementById("refresh-puzzles");
const puzzleTableBody = document.getElementById("admin-puzzle-table");
const statusBox = document.getElementById("admin-status");
const puzzleForm = document.getElementById("puzzle-form");

const STORAGE_KEY = "btcmultipuzzle-admin-key";

function getAdminKey() {
    return localStorage.getItem(STORAGE_KEY) || "";
}

function setAdminKey(value) {
    localStorage.setItem(STORAGE_KEY, value.trim());
}

function showStatus(message, isError = false) {
    if (!message) {
        statusBox.classList.add("hidden");
        statusBox.textContent = "";
        statusBox.classList.remove("error");
        return;
    }
    statusBox.textContent = message;
    statusBox.classList.remove("hidden");
    statusBox.classList.toggle("error", isError);
}

async function fetchPuzzles() {
    const key = adminKeyInput.value.trim();
    if (!key) {
        showStatus("Enter admin key to load puzzles.", true);
        return;
    }

    try {
        showStatus("Loading puzzles...");
        const response = await fetch(`${API_BASE}${PUZZLES_ENDPOINT}`, {
            headers: {
                "X-Admin-Key": key
            }
        });

        if (!response.ok) {
            const text = await response.text();
            throw new Error(text || `Request failed (${response.status})`);
        }

        const puzzles = await response.json();
        renderPuzzleTable(puzzles);
        showStatus(`Loaded ${puzzles.length} puzzle${puzzles.length === 1 ? "" : "s"}.`);
    } catch (err) {
        console.error(err);
        showStatus(err.message, true);
        puzzleTableBody.innerHTML = `<tr><td colspan="9" class="empty">${escapeHtml(err.message)}</td></tr>`;
    }
}

function renderPuzzleTable(puzzles) {
    if (!puzzles || puzzles.length === 0) {
        puzzleTableBody.innerHTML = `<tr><td colspan="9" class="empty">No data</td></tr>`;
        return;
    }

    puzzleTableBody.innerHTML = puzzles.map(puzzle => {
        const payload = encodeURIComponent(JSON.stringify(puzzle));
        return `
        <tr>
            <td>${escapeHtml(puzzle.code)}</td>
            <td>${escapeHtml(puzzle.displayName)}</td>
            <td>${escapeHtml(puzzle.minPrefixHex)}</td>
            <td>${escapeHtml(puzzle.maxPrefixHex)}</td>
            <td>${puzzle.chunkSize}</td>
            <td>${puzzle.weight.toFixed(2)}</td>
            <td>${puzzle.enabled ? "Yes" : "No"}</td>
            <td>${puzzle.randomized ? "Yes" : "No"}</td>
            <td>
                <button data-action="edit" data-code="${escapeHtml(puzzle.code)}" data-puzzle="${payload}">Edit</button>
                <button data-action="delete" data-code="${escapeHtml(puzzle.code)}" class="secondary">Delete</button>
            </td>
        </tr>
        `;
    }).join("");
}

function populateForm(puzzle) {
    puzzleForm.code.value = puzzle.code;
    puzzleForm.displayName.value = puzzle.displayName;
    puzzleForm.targetAddress.value = puzzle.targetAddress;
    puzzleForm.minPrefixHex.value = puzzle.minPrefixHex;
    puzzleForm.maxPrefixHex.value = puzzle.maxPrefixHex;
    puzzleForm.chunkSize.value = puzzle.chunkSize;
    puzzleForm.prefixLength.value = puzzle.prefixLength ?? "";
    puzzleForm.weight.value = puzzle.weight;
    puzzleForm.workloadStartSuffix.value = puzzle.workloadStartSuffix ?? "";
    puzzleForm.workloadEndSuffix.value = puzzle.workloadEndSuffix ?? "";
    puzzleForm.notes.value = puzzle.notes ?? "";
    puzzleForm.enabled.checked = puzzle.enabled;
    puzzleForm.randomized.checked = puzzle.randomized;
}

function collectFormData() {
    const formData = new FormData(puzzleForm);
    const payload = {
        code: formData.get("code")?.toString().trim() ?? "",
        displayName: formData.get("displayName")?.toString().trim() ?? "",
        targetAddress: formData.get("targetAddress")?.toString().trim() ?? "",
        minPrefixHex: formData.get("minPrefixHex")?.toString().trim() ?? "",
        maxPrefixHex: formData.get("maxPrefixHex")?.toString().trim() ?? "",
        chunkSize: Number(formData.get("chunkSize")) || 1,
        prefixLength: Number(formData.get("prefixLength")) || 0,
        weight: Number(formData.get("weight")) || 1,
        workloadStartSuffix: formData.get("workloadStartSuffix")?.toString().trim() ?? "",
        workloadEndSuffix: formData.get("workloadEndSuffix")?.toString().trim() ?? "",
        notes: formData.get("notes")?.toString().trim() ?? "",
        enabled: formData.get("enabled") === "on",
        randomized: formData.get("randomized") === "on"
    };

    if (!payload.code || !payload.displayName || !payload.minPrefixHex || !payload.maxPrefixHex) {
        throw new Error("Code, name, min prefix, and max prefix are required.");
    }

    return payload;
}

async function savePuzzle(event) {
    event.preventDefault();

    try {
        const payload = collectFormData();
        const key = adminKeyInput.value.trim();
        if (!key) {
            throw new Error("Admin key required.");
        }

        const response = await fetch(`${API_BASE}${PUZZLES_ENDPOINT}/${encodeURIComponent(payload.code)}`, {
            method: "PUT",
            headers: {
                "Content-Type": "application/json",
                "X-Admin-Key": key
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            const text = await response.text();
            throw new Error(text || `Save failed (${response.status})`);
        }

        showStatus(`Puzzle ${payload.code} saved.`);
        await fetchPuzzles();
    } catch (err) {
        console.error(err);
        showStatus(err.message, true);
    }
}

async function deletePuzzle(code) {
    if (!confirm(`Delete puzzle ${code}?`)) return;
    const key = adminKeyInput.value.trim();
    if (!key) {
        showStatus("Admin key required.", true);
        return;
    }

    try {
        const response = await fetch(`${API_BASE}${PUZZLES_ENDPOINT}/${encodeURIComponent(code)}`, {
            method: "DELETE",
            headers: {
                "X-Admin-Key": key
            }
        });

        if (!response.ok) {
            const text = await response.text();
            throw new Error(text || `Delete failed (${response.status})`);
        }

        showStatus(`Puzzle ${code} deleted.`);
        await fetchPuzzles();
    } catch (err) {
        console.error(err);
        showStatus(err.message, true);
    }
}

function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

document.addEventListener("DOMContentLoaded", () => {
    adminKeyInput.value = getAdminKey();

    saveKeyButton.addEventListener("click", () => {
        setAdminKey(adminKeyInput.value);
        showStatus("Admin key saved.");
    });

    refreshButton.addEventListener("click", fetchPuzzles);

    puzzleTableBody.addEventListener("click", (event) => {
        const target = event.target.closest("button");
        if (!target) return;

        const action = target.dataset.action;
        const code = target.dataset.code;
        if (!code) return;

        if (action === "edit") {
            const encoded = target.dataset.puzzle ?? "";
            let puzzle;
            try {
                puzzle = JSON.parse(decodeURIComponent(encoded));
            } catch (err) {
                console.error(err);
                showStatus("Cannot parse puzzle payload.", true);
                return;
            }
            populateForm(puzzle);
            showStatus(`Editing puzzle ${code}.`);
        } else if (action === "delete") {
            deletePuzzle(code);
        }
    });

    puzzleForm.addEventListener("submit", savePuzzle);
    puzzleForm.addEventListener("reset", () => showStatus("Form reset."));

    if (adminKeyInput.value) {
        fetchPuzzles();
    }
});
