document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-list-page]");
  const grid = page?.querySelector("[data-sector-grid]");
  const table = grid?.querySelector("table");
  const tableBody = table?.querySelector("tbody");
  const sortHeaders = Array.from(table?.querySelectorAll("[data-sort-key]") ?? []);
  const rows = Array.from(table?.querySelectorAll("[data-sector-row]") ?? []);
  const empty = page?.querySelector("[data-sector-empty]");
  const editor = page?.querySelector("[data-sector-editor]");
  const editorFields = editor?.querySelector("[data-sector-editor-fields]");
  const editorCode = editor?.querySelector("[data-sector-code]");
  const editorCodeDisplay = editor?.querySelector("[data-sector-code-display]");
  const editorDescription = editor?.querySelector("[data-sector-description]");
  const saveButton = editor?.querySelector("[data-sector-save]");
  const cancelButton = editor?.querySelector("[data-sector-cancel]");
  const actionButtons = Array.from(page?.querySelectorAll("[data-sector-action]") ?? []);
  const deleteForm = page?.querySelector("[data-sector-delete-form]");
  const deleteCode = deleteForm?.querySelector("[data-sector-delete-code]");
  const hasValidationErrors = Boolean(
    editor?.querySelector(".validation-summary-errors, .field-validation-error"));

  page?.querySelectorAll("[data-page-message]").forEach((pageMessage) => {
    window.MicronoteMessageBox?.show({
      message: pageMessage.dataset.messageText,
      variant: pageMessage.dataset.messageVariant || "info"
    });
  });

  if (!page || !grid || !table) {
    return;
  }

  const normalize = (value) =>
    String(value ?? "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase();

  const visibleRows = () => rows.filter((row) => !row.hidden);
  const selectedRow = () => table.querySelector(".selected-row");
  const codeText = (code) => code ? String(code).padStart(3, "0") : (page.dataset.nextCode || "");
  let currentSortKey = "";
  let currentSortDirection = "asc";
  let isEditing = false;

  const setEditing = (editing) => {
    isEditing = editing;
    page.classList.toggle("is-sector-editing", editing);
    grid.setAttribute("aria-disabled", editing ? "true" : "false");

    if (editorFields) {
      editorFields.disabled = !editing;
    }
    if (saveButton) {
      saveButton.disabled = !editing;
    }
    if (cancelButton) {
      cancelButton.disabled = !editing;
    }

    actionButtons.forEach((button) => {
      button.disabled = editing;
    });
  };

  const updateEditor = (row) => {
    if (!editorCode || !editorCodeDisplay || !editorDescription || !row) {
      return;
    }

    editorCode.value = row.dataset.code ?? "";
    editorCodeDisplay.value = codeText(row.dataset.code);
    editorDescription.value = row.dataset.description ?? "";
  };

  const ensureVisible = (row, direction = 0) => {
    const headerHeight = table.tHead?.offsetHeight ?? 0;
    const visibleTop = grid.scrollTop + headerHeight;
    const visibleBottom = grid.scrollTop + grid.clientHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;

    if (rowTop >= visibleTop && rowBottom <= visibleBottom) {
      return;
    }

    if (direction >= 0 && rowBottom > visibleBottom) {
      grid.scrollTop = rowBottom - grid.clientHeight + 1;
      return;
    }

    if (direction <= 0 && rowTop < visibleTop) {
      grid.scrollTop = Math.max(rowTop - headerHeight - 1, 0);
    }
  };

  const selectRow = (row, focus = false, direction = 0) => {
    if (!row || row.hidden || isEditing) {
      return;
    }

    rows.forEach((candidate) => {
      candidate.classList.remove("selected-row");
      candidate.removeAttribute("aria-selected");
    });

    row.classList.add("selected-row");
    row.setAttribute("aria-selected", "true");
    updateEditor(row);
    setEditing(false);

    if (focus) {
      row.focus({ preventScroll: true });
    }
    ensureVisible(row, direction);
  };

  const sortValue = (row, key, type) => {
    const value = row.dataset[`sort${key[0].toUpperCase()}${key.slice(1)}`] ?? "";
    return type === "number" ? Number.parseFloat(value) || 0 : normalize(value);
  };

  const applySort = (key, direction, type = "text") => {
    if (!tableBody || isEditing) {
      return;
    }

    [...rows]
      .sort((left, right) => {
        const leftValue = sortValue(left, key, type);
        const rightValue = sortValue(right, key, type);
        let result = 0;

        if (leftValue < rightValue) {
          result = -1;
        } else if (leftValue > rightValue) {
          result = 1;
        }

        return direction === "desc" ? -result : result;
      })
      .forEach((row) => tableBody.appendChild(row));
  };

  const updateSortHeaders = () => {
    sortHeaders.forEach((header) => {
      const isActive = header.dataset.sortKey === currentSortKey;
      header.classList.toggle("is-sorted", isActive);
      header.dataset.sortDirection = isActive ? currentSortDirection : "";
      header.setAttribute(
        "aria-sort",
        isActive
          ? (currentSortDirection === "asc" ? "ascending" : "descending")
          : "none"
      );
    });
  };

  const sortByHeader = (header) => {
    const key = header.dataset.sortKey;
    if (!key || isEditing) {
      return;
    }

    currentSortDirection =
      currentSortKey === key && currentSortDirection === "asc" ? "desc" : "asc";
    currentSortKey = key;

    applySort(key, currentSortDirection, header.dataset.sortType);
    updateSortHeaders();
    selectRow(visibleRows()[0]);
  };

  const gridViewport = () => {
    const gridRect = grid.getBoundingClientRect();
    const headerHeight = table.tHead?.getBoundingClientRect().height ?? 0;
    return {
      top: gridRect.top + headerHeight,
      bottom: gridRect.bottom
    };
  };

  const isRowVisible = (row, viewport) => {
    const rowRect = row.getBoundingClientRect();
    return rowRect.bottom > viewport.top + 1 &&
      rowRect.top < viewport.bottom - 1;
  };

  const fullyVisibleRows = (viewport) =>
    visibleRows().filter((row) => {
      const rowRect = row.getBoundingClientRect();
      return rowRect.top >= viewport.top + 1 &&
        rowRect.bottom <= viewport.bottom - 1;
    });


  const selectVisibleRowFromScroll = (delta) => {
    if (delta === 0 || (typeof isEditing !== "undefined" && isEditing)) {
      return;
    }

    const viewport = gridViewport();
    const selected = selectedRow();
    if (!selected || isRowVisible(selected, viewport)) {
      return;
    }

    const currentRows = fullyVisibleRows(viewport);
    if (currentRows.length === 0) {
      return;
    }

    selectRow(delta > 0 ? currentRows[0] : currentRows[currentRows.length - 1]);
  };
  const requireSelection = () => {
    const row = selectedRow();
    if (!row) {
      window.MicronoteMessageBox?.show({
        message: "Selezionare un settore.",
        variant: "info"
      });
      return null;
    }
    return row;
  };

  const startEdit = () => {
    const row = requireSelection();
    if (!row || !editorDescription) {
      return;
    }

    updateEditor(row);
    setEditing(true);
    editorDescription.focus();
    editorDescription.select();
  };

  const startNew = () => {
    rows.forEach((candidate) => {
      candidate.classList.remove("selected-row");
      candidate.removeAttribute("aria-selected");
    });

    if (editorCode) {
      editorCode.value = "0";
    }
    if (editorCodeDisplay) {
      editorCodeDisplay.value = page.dataset.nextCode || "";
    }
    if (editorDescription) {
      editorDescription.value = "";
    }

    setEditing(true);
    editorDescription?.focus();
  };

  const cancelEdit = () => {
    setEditing(false);
    const row = selectedRow() || visibleRows()[0];
    if (row) {
      updateEditor(row);
      row.focus({ preventScroll: true });
    }
  };

  const navigateGrid = (event, row) => {
    if (isEditing) {
      return false;
    }

    const currentRows = visibleRows();
    if (currentRows.length === 0) {
      return false;
    }

    const currentIndex = Math.max(currentRows.indexOf(row), 0);

    if (event.key === "Enter") {
      event.preventDefault();
      startEdit();
    } else if (event.key === "ArrowDown") {
      event.preventDefault();
      selectRow(
        currentRows[Math.min(currentIndex + 1, currentRows.length - 1)],
        true,
        1
      );
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      selectRow(currentRows[Math.max(currentIndex - 1, 0)], true, -1);
    } else if (event.key === "Home") {
      event.preventDefault();
      selectRow(currentRows[0], true, -1);
    } else if (event.key === "End") {
      event.preventDefault();
      selectRow(currentRows[currentRows.length - 1], true, 1);
    } else {
      return false;
    }

    return true;
  };

  rows.forEach((row) => {
    row.addEventListener("click", () => selectRow(row, true));
    row.addEventListener("dblclick", () => {
      if (isEditing) {
        return;
      }

      selectRow(row);
      startEdit();
    });
    row.addEventListener("keydown", (event) => {
      navigateGrid(event, selectedRow() || row);
    });
  });

  sortHeaders.forEach((header) => {
    header.tabIndex = 0;
    header.setAttribute("role", "button");
    header.setAttribute("aria-sort", "none");
    header.addEventListener("click", () => sortByHeader(header));
    header.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" && event.key !== " ") {
        return;
      }

      event.preventDefault();
      sortByHeader(header);
    });
  });

  grid.addEventListener("keydown", (event) => {
    if (event.target.closest("[data-sector-row]")) {
      return;
    }

    const row = selectedRow() || visibleRows()[0];
    if (row) {
      navigateGrid(event, row);
    }
  });

  let lastScrollTop = grid.scrollTop;
  let scrollFrame = 0;

  grid.addEventListener("scroll", () => {
    if (scrollFrame || isEditing) {
      return;
    }

    scrollFrame = window.requestAnimationFrame(() => {
      scrollFrame = 0;
      const currentScrollTop = grid.scrollTop;
      const delta = currentScrollTop - lastScrollTop;
      lastScrollTop = currentScrollTop;

      if (delta === 0) {
        return;
      }

      const viewport = gridViewport();
      const selected = selectedRow();
      if (!selected || isRowVisible(selected, viewport)) {
        return;
      }

      const currentRows = fullyVisibleRows(viewport);
      if (currentRows.length === 0) {
        return;
      }

      selectRow(delta > 0 ? currentRows[0] : currentRows[currentRows.length - 1]);
    });
  }, { passive: true });


  let wheelFrame = 0;

  grid.addEventListener("wheel", (event) => {
    if (wheelFrame) {
      window.cancelAnimationFrame(wheelFrame);
    }

    const delta = event.deltaY;
    wheelFrame = window.requestAnimationFrame(() => {
      wheelFrame = window.requestAnimationFrame(() => {
        wheelFrame = 0;
        selectVisibleRowFromScroll(delta);
      });
    });
  }, { passive: true });
  page.querySelectorAll("[data-sector-action]").forEach((button) => {
    button.addEventListener("click", () => {
      const action = button.dataset.sectorAction;
      if (action === "new") {
        startNew();
        return;
      }

      if (action === "edit") {
        startEdit();
        return;
      }

      const row = requireSelection();
      if (action === "delete" && row && deleteForm && deleteCode) {
        window.MicronoteMessageBox?.show({
          mode: "confirm",
          variant: "confirm",
          message: `Eliminare il settore ${row.dataset.recordLabel}?`,
          detail: "L'operazione non potrà essere annullata.",
          okText: "Elimina",
          onConfirm: () => {
            deleteCode.value = row.dataset.deleteCode ?? "";
            deleteForm.submit();
          }
        });
      }
    });
  });

  cancelButton?.addEventListener("click", cancelEdit);

  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape" || event.defaultPrevented) {
      return;
    }

    event.preventDefault();
    if (isEditing) {
      cancelEdit();
      return;
    }

    window.location.href = page.dataset.menuUrl || "/";
  });

  if (empty) {
    empty.hidden = rows.length !== 0;
  }

  window.setTimeout(() => {
    const initialRow = rows.find((row) => row.dataset.selected === "true") ||
      visibleRows()[0];
    if (initialRow) {
      selectRow(initialRow);
    } else {
      setEditing(false);
    }

    if (hasValidationErrors) {
      setEditing(true);
      editorDescription?.focus();
    }
  }, 0);
});
