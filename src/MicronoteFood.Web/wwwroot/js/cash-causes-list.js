document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-list-page]");
  const grid = page?.querySelector("[data-cash-cause-grid]");
  const table = grid?.querySelector("table");
  const body = table?.querySelector("tbody");
  const rows = Array.from(table?.querySelectorAll("[data-cash-cause-row]") ?? []);
  const headers = Array.from(table?.querySelectorAll("[data-sort-key]") ?? []);
  const deleteForm = page?.querySelector("[data-cash-cause-delete-form]");
  const deleteCode = deleteForm?.querySelector("[data-cash-cause-delete-code]");

  page?.querySelectorAll("[data-page-message]").forEach((message) => {
    window.MicronoteMessageBox?.show({
      message: message.dataset.messageText,
      variant: message.dataset.messageVariant || "info"
    });
  });

  if (!page || !table) return;

  const selectedRow = () => table.querySelector(".selected-row");
  const selectRow = (row, focus = false) => {
    if (!row) return;
    rows.forEach((candidate) => {
      candidate.classList.remove("selected-row");
      candidate.removeAttribute("aria-selected");
    });
    row.classList.add("selected-row");
    row.setAttribute("aria-selected", "true");
    if (focus) row.focus({ preventScroll: true });
  };

  const requireSelection = () => {
    const row = selectedRow();
    if (!row) {
      window.MicronoteMessageBox?.show({ message: "Selezionare una causale di cassa.", variant: "info" });
    }
    return row;
  };

  rows.forEach((row) => {
    row.addEventListener("click", () => selectRow(row, true));
    row.addEventListener("dblclick", () => {
      if (row.dataset.locked !== "true") window.location.href = row.dataset.editUrl;
    });
    row.addEventListener("keydown", (event) => {
      const index = rows.indexOf(row);
      if (event.key === "Enter" && row.dataset.locked !== "true") {
        event.preventDefault();
        window.location.href = row.dataset.editUrl;
      } else if (event.key === "ArrowDown" || event.key === "ArrowUp") {
        event.preventDefault();
        const step = event.key === "ArrowDown" ? 1 : -1;
        selectRow(rows[Math.max(0, Math.min(rows.length - 1, index + step))], true);
      }
    });
  });

  headers.forEach((header) => {
    header.addEventListener("click", () => {
      const key = header.dataset.sortKey;
      const direction = header.dataset.sortDirection === "asc" ? "desc" : "asc";
      const numeric = header.dataset.sortType === "number";
      rows.sort((left, right) => {
        const name = `sort${key[0].toUpperCase()}${key.slice(1)}`;
        const a = left.dataset[name] ?? "";
        const b = right.dataset[name] ?? "";
        const result = numeric ? Number(a) - Number(b) : a.localeCompare(b, "it", { sensitivity: "base" });
        return direction === "asc" ? result : -result;
      }).forEach((row) => body.appendChild(row));
      headers.forEach((candidate) => candidate.dataset.sortDirection = "");
      header.dataset.sortDirection = direction;
    });
  });

  page.querySelectorAll("[data-cash-cause-action]").forEach((button) => {
    button.addEventListener("click", () => {
      const row = requireSelection();
      if (!row) return;
      if (row.dataset.locked === "true") {
        window.MicronoteMessageBox?.show({ message: "La causale selezionata è bloccata.", variant: "info" });
        return;
      }
      if (button.dataset.cashCauseAction === "edit") {
        window.location.href = row.dataset.editUrl;
      } else {
        window.MicronoteMessageBox?.show({
          mode: "confirm",
          variant: "confirm",
          message: `Eliminare la causale ${row.dataset.recordLabel}?`,
          detail: "L'operazione non potrà essere annullata.",
          okText: "Elimina",
          onConfirm: () => {
            deleteCode.value = row.dataset.deleteCode;
            deleteForm.submit();
          }
        });
      }
    });
  });

  const empty = page.querySelector("[data-cash-cause-empty]");
  if (empty) empty.hidden = rows.length !== 0;
  if (rows.length) selectRow(rows[0]);
});
