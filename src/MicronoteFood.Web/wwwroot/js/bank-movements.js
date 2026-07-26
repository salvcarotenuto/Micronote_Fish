document.addEventListener("DOMContentLoaded", () => {
  const filterForm = document.querySelector("[data-bank-filters]");
  const grid = document.querySelector(".bank-movements-grid-frame");
  const table = grid?.querySelector("table");
  let rows = Array.from(table?.querySelectorAll("[data-bank-row]") ?? []);
  const sortHeaders = Array.from(table?.querySelectorAll("th[data-sort-key]") ?? []);
  const actionButtons = Array.from(document.querySelectorAll("[data-bank-action]"));
  const deleteForm = document.querySelector("[data-bank-delete-form]");
  const deleteId = deleteForm?.querySelector("[data-bank-delete-id]");
  const articleModal = document.querySelector("[data-bank-accounting-article-modal]");
  const articleModalFrame = document.querySelector("[data-bank-accounting-article-modal-frame]");
  let filterTimer;
  let sortState = { key: "", direction: "asc" };

  const submitFilters = () => {
    if (!filterForm) {
      return;
    }

    window.clearTimeout(filterTimer);
    filterTimer = window.setTimeout(() => filterForm.requestSubmit(), 150);
  };

  filterForm?.querySelectorAll("select[name]").forEach((field) => {
    field.addEventListener("change", submitFilters);
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      event.preventDefault();
      window.location.href = "/";
    }
  });

  const visibleRows = () => rows.filter((row) => !row.hidden);
  const selectedRow = () => table?.querySelector("[data-bank-row].selected");
  const toKebab = (value) => value.replace(/[A-Z]/g, (match) => `-${match.toLowerCase()}`);

  const sortValue = (row, key, type) => {
    const value = row.getAttribute(`data-sort-${toKebab(key)}`) ?? "";
    if (type === "number") {
      const numeric = Number.parseFloat(value.replace(",", "."));
      return Number.isFinite(numeric) ? numeric : Number.NEGATIVE_INFINITY;
    }

    return value.trim().toLocaleLowerCase("it-IT");
  };

  const updateSortHeaders = () => {
    sortHeaders.forEach((header) => {
      const active = header.dataset.sortKey === sortState.key;
      header.classList.toggle("is-sorted", active);
      header.dataset.sortDirection = active ? sortState.direction : "";
      header.setAttribute("aria-sort", active ? (sortState.direction === "asc" ? "ascending" : "descending") : "none");
    });
  };

  const ensureVisible = (row, direction = 0) => {
    if (!grid || !table || !row) {
      return;
    }

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
    if (!row || row.hidden) {
      return;
    }

    rows.forEach((candidate) => {
      candidate.classList.remove("selected", "selected-row");
      candidate.removeAttribute("aria-selected");
    });

    row.classList.add("selected", "selected-row");
    row.setAttribute("aria-selected", "true");

    if (focus) {
      row.focus({ preventScroll: true });
    }

    ensureVisible(row, direction);
  };

  const sortRows = (header) => {
    if (!table?.tBodies[0]) {
      return;
    }

    const key = header.dataset.sortKey || "";
    const type = header.dataset.sortType || "text";
    if (!key) {
      return;
    }

    sortState = {
      key,
      direction: sortState.key === key && sortState.direction === "asc" ? "desc" : "asc"
    };

    const direction = sortState.direction === "asc" ? 1 : -1;
    const selected = selectedRow();

    rows = rows
      .map((row, index) => ({ row, index }))
      .sort((left, right) => {
        const leftValue = sortValue(left.row, key, type);
        const rightValue = sortValue(right.row, key, type);
        let result = 0;

        if (typeof leftValue === "number" && typeof rightValue === "number") {
          result = leftValue - rightValue;
        } else {
          result = String(leftValue).localeCompare(String(rightValue), "it-IT", {
            numeric: true,
            sensitivity: "base"
          });
        }

        return result === 0 ? left.index - right.index : result * direction;
      })
      .map((entry) => entry.row);

    rows.forEach((row) => table.tBodies[0].appendChild(row));
    updateSortHeaders();

    if (selected) {
      selectRow(selected, true, 0);
      return;
    }

    const first = visibleRows()[0];
    if (first) {
      selectRow(first, true, 0);
    }
  };

  const navigateGrid = (event, currentRow) => {
    const currentRows = visibleRows();
    const currentIndex = currentRows.indexOf(currentRow);

    if (currentIndex < 0) {
      return;
    }

    if (event.key === "ArrowDown") {
      event.preventDefault();
      selectRow(currentRows[Math.min(currentIndex + 1, currentRows.length - 1)], true, 1);
      return;
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      selectRow(currentRows[Math.max(currentIndex - 1, 0)], true, -1);
      return;
    }

    if (event.key === "Home") {
      event.preventDefault();
      selectRow(currentRows[0], true, -1);
      return;
    }

    if (event.key === "End") {
      event.preventDefault();
      selectRow(currentRows[currentRows.length - 1], true, 1);
    }
  };

  const currentReturnUrl = () => window.location.pathname + window.location.search;

  const movementEditUrl = (row) => `/PrimaNota/Edit/${row.dataset.movementId}?returnTo=${encodeURIComponent(currentReturnUrl())}`;

  const closeArticleModal = () => {
    if (!articleModal) {
      return;
    }

    articleModal.hidden = true;
    document.body.classList.remove("purchase-invoice-payment-modal-open");
    articleModalFrame?.removeAttribute("src");
    document.querySelector("[data-bank-action='article']")?.focus();
  };

  const openArticleModal = (row) => {
    const url = `/ArticoloPrimaNota?id=${encodeURIComponent(row.dataset.movementId || "")}`;
    if (!articleModal || !articleModalFrame) {
      window.location.href = url;
      return;
    }

    articleModalFrame.src = url;
    articleModal.hidden = false;
    document.body.classList.add("purchase-invoice-payment-modal-open");
    articleModalFrame.focus();
  };

  const requireSelection = () => {
    const row = selectedRow();
    if (row) {
      return row;
    }

    window.MicronoteMessageBox?.show({
      title: "Movimenti bancari",
      message: "Selezionare un movimento dalla lista."
    });
    return null;
  };

  const selectedLabel = (row) => {
    const cells = Array.from(row?.querySelectorAll("td") ?? []);
    const number = cells[1]?.textContent?.trim() || "";
    const date = cells[2]?.textContent?.trim() || "";
    return [number, date].filter(Boolean).join(" - ");
  };

  rows.forEach((row) => {
    row.addEventListener("click", () => selectRow(row, true, 0));
    row.addEventListener("dblclick", () => {
      window.location.href = movementEditUrl(row);
    });
    row.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        selectRow(row, true, 0);
        return;
      }

      navigateGrid(event, selectedRow() || row);
    });
  });

  sortHeaders.forEach((header) => {
    header.tabIndex = 0;
    header.setAttribute("aria-sort", "none");
    header.addEventListener("click", () => sortRows(header));
    header.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" && event.key !== " ") {
        return;
      }

      event.preventDefault();
      sortRows(header);
    });
  });

  actionButtons.forEach((button) => {
    button.addEventListener("click", () => {
      const action = button.dataset.bankAction;
      if (action === "new") {
        window.MicronoteMessageBox?.show({
          title: "Nuovo movimento bancario",
          message: "Funzione in preparazione."
        });
        return;
      }

      const row = requireSelection();
      if (!row) {
        return;
      }

      if (action === "edit") {
        window.location.href = movementEditUrl(row);
        return;
      }

      const label = selectedLabel(row);
      if (action === "delete") {
        window.MicronoteMessageBox?.show({
          mode: "confirm",
          variant: "confirm",
          title: "Cancella movimento",
          message: `Cancellare il movimento ${label}?`,
          detail: "L'operazione non potra essere annullata.",
          okText: "Cancella",
          onConfirm: () => {
            if (deleteForm && deleteId) {
              deleteId.value = row.dataset.movementId || "";
              deleteForm.submit();
            }
          }
        });
        return;
      }

      if (action === "article") {
        openArticleModal(row);
        return;
      }

      window.MicronoteMessageBox?.show({
        title: "Stampa",
        message: `Stampa per ${label}.`,
        detail: "Funzione in preparazione."
      });
    });
  });

  window.addEventListener("message", (event) => {
    if (event.origin === window.location.origin && event.data?.type === "micronote:accounting-article:close") {
      closeArticleModal();
    }
  });

  if (grid && table && rows.length > 0) {
    let lastScrollTop = grid.scrollTop;
    let scrollFrame = 0;

    grid.addEventListener("scroll", () => {
      if (scrollFrame) {
        window.cancelAnimationFrame(scrollFrame);
      }

      scrollFrame = window.requestAnimationFrame(() => {
        scrollFrame = 0;
        const currentScrollTop = grid.scrollTop;
        const delta = currentScrollTop - lastScrollTop;
        lastScrollTop = currentScrollTop;

        if (delta === 0) {
          return;
        }

        const selected = selectedRow();
        if (!selected) {
          return;
        }

        const gridRect = grid.getBoundingClientRect();
        const headerHeight = table.tHead?.getBoundingClientRect().height ?? 0;
        const viewportTop = gridRect.top + headerHeight;
        const viewportBottom = gridRect.bottom;
        const selectedRect = selected.getBoundingClientRect();
        const isVisible = selectedRect.bottom > viewportTop + 1 &&
          selectedRect.top < viewportBottom - 1;

        if (isVisible) {
          return;
        }

        const visible = visibleRows().filter((row) => {
          const rect = row.getBoundingClientRect();
          return rect.top >= viewportTop + 1 && rect.bottom <= viewportBottom - 1;
        });

        if (visible.length > 0) {
          selectRow(delta > 0 ? visible[0] : visible[visible.length - 1], true, delta > 0 ? 1 : -1);
        }
      });
    }, { passive: true });

    const initialRow = selectedRow() || visibleRows()[0];
    if (initialRow) {
      selectRow(initialRow, true, 0);
    }
  }
});






