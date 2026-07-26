(() => {
  const root = document.querySelector("[data-electronic-invoice-upload]");
  if (!root) {
    return;
  }

  const selectButton = root.querySelector("[data-fe-upload-select]");
  const input = root.querySelector("[data-fe-upload-input]");
  const clearButton = root.querySelector("[data-fe-upload-clear]");
  const rows = root.querySelector("[data-fe-upload-rows]");
  const count = root.querySelector("[data-fe-upload-count]");
  const selectedCount = root.querySelector("[data-fe-upload-selected]");
  const uploadedCount = root.querySelector("[data-fe-upload-uploaded]");
  const existingCount = root.querySelector("[data-fe-upload-existing]");
  const skippedCount = root.querySelector("[data-fe-upload-skipped]");
  const errorCount = root.querySelector("[data-fe-upload-errors]");
  const progress = root.querySelector("[data-fe-upload-progress]");
  const progressBar = root.querySelector("[data-fe-upload-progress-bar]");
  const progressText = root.querySelector("[data-fe-upload-progress-text]");

  const state = {
    selected: 0,
    uploaded: 0,
    existing: 0,
    skipped: 0,
    errors: 0,
    processed: 0,
    currentFile: ""
  };

  const allowedFile = (file) => /\.(xml|p7m)$/i.test(file?.name ?? "");
  const token = () => document.querySelector("input[name='__RequestVerificationToken']")?.value ?? "";

  const setText = (element, value) => {
    if (element) {
      element.textContent = String(value);
    }
  };

  const refreshProgress = () => {
    const total = state.selected;
    const processed = Math.min(state.processed, total);
    const percent = total > 0 ? Math.round((processed / total) * 100) : 0;

    if (progressBar) {
      progressBar.style.width = `${percent}%`;
    }

    if (progressText) {
      const suffix = state.currentFile && processed < total ? ` - ${state.currentFile}` : "";
      progressText.textContent = total > 0 ? `${processed} / ${total} file (${percent}%)${suffix}` : "In attesa di selezione";
    }
  };

  const refreshSummary = () => {
    setText(count, state.uploaded + state.existing);
    setText(selectedCount, state.selected);
    setText(uploadedCount, state.uploaded);
    setText(existingCount, state.existing);
    setText(skippedCount, state.skipped);
    setText(errorCount, state.errors);
    refreshProgress();
  };

  const resetState = () => {
    state.selected = 0;
    state.uploaded = 0;
    state.existing = 0;
    state.skipped = 0;
    state.errors = 0;
    state.processed = 0;
    state.currentFile = "";
  };

  const clearRows = () => {
    if (rows) {
      rows.innerHTML = '<tr class="empty-row"><td colspan="5">Nessun caricamento effettuato.</td></tr>';
    }

    resetState();
    refreshSummary();
  };

  const statusLabel = (status) => {
    switch (status) {
      case "uploaded": return "Caricato";
      case "existing": return "Gia presente";
      case "skipped": return "Scartato";
      default: return "Errore";
    }
  };

  const appendRow = (result) => {
    if (!rows) {
      return;
    }

    if (rows.querySelector(".empty-row")) {
      rows.innerHTML = "";
    }

    const tr = document.createElement("tr");
    tr.className = `fe-upload-${result.status || "error"}`;

    for (let i = 0; i < 5; i += 1) {
      tr.appendChild(document.createElement("td"));
    }

    tr.cells[0].textContent = result.fileName || "";
    tr.cells[1].textContent = result.type || "";
    tr.cells[2].textContent = result.size || "";
    tr.cells[3].textContent = statusLabel(result.status);
    tr.cells[4].textContent = result.message || "";
    rows.appendChild(tr);
  };

  const addToSummary = (status) => {
    if (status === "uploaded") {
      state.uploaded += 1;
    } else if (status === "existing") {
      state.existing += 1;
    } else if (status === "skipped") {
      state.skipped += 1;
    } else {
      state.errors += 1;
    }

    refreshSummary();
  };

  const markProcessed = () => {
    state.processed += 1;
    state.currentFile = "";
    refreshProgress();
  };

  const skippedResult = (file, message) => ({
    status: "skipped",
    fileName: file?.name ?? "",
    type: "",
    size: "",
    message
  });

  const uploadFile = async (file) => {
    if (!allowedFile(file)) {
      const result = skippedResult(file, "Estensione non ammessa.");
      appendRow(result);
      addToSummary(result.status);
      markProcessed();
      return;
    }

    const url = new URL(window.location.href);
    url.searchParams.set("handler", "Upload");

    const formData = new FormData();
    formData.append("file", file, file.name);

    const requestToken = token();
    if (requestToken) {
      formData.append("__RequestVerificationToken", requestToken);
    }

    try {
      const response = await fetch(url, {
        method: "POST",
        body: formData,
        headers: { Accept: "application/json" }
      });

      if (!response.ok) {
        throw new Error("HTTP " + response.status);
      }

      const result = await response.json();
      appendRow(result);
      addToSummary(result.status);
    } catch {
      const result = {
        status: "error",
        fileName: file.name,
        type: "",
        size: "",
        message: "Upload non riuscito."
      };
      appendRow(result);
      addToSummary(result.status);
    } finally {
      markProcessed();
    }
  };
  const showCompletionMessage = () => {
    const message = "Caricamento completato.";

    if (window.MicronoteMessageBox?.show) {
      window.MicronoteMessageBox.show({
        title: "Caricamento FE acquisti",
        message
      });
      return;
    }

    window.alert(message);
  };

  const uploadFiles = async (files) => {
    const list = Array.from(files ?? []);
    resetState();
    state.selected = list.length;
    refreshSummary();

    if (!list.length) {
      return;
    }

    if (rows) {
      rows.innerHTML = "";
    }

    if (selectButton) {
      selectButton.disabled = true;
    }

    if (clearButton) {
      clearButton.disabled = true;
    }

    try {
      for (const file of list) {
        state.currentFile = file.name;
        refreshProgress();
        await uploadFile(file);
      }
    } finally {
      state.currentFile = "";
      refreshProgress();

      if (selectButton) {
        selectButton.disabled = false;
      }

      if (clearButton) {
        clearButton.disabled = false;
      }

      if (input) {
        input.value = "";
      }

      showCompletionMessage();
    }
  };

  selectButton?.addEventListener("click", () => input?.click());
  input?.addEventListener("change", () => uploadFiles(input.files));
  clearButton?.addEventListener("click", clearRows);

  refreshSummary();
})();





