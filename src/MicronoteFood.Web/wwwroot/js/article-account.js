document.addEventListener("DOMContentLoaded", () => {
  const root = document.querySelector("[data-article-account]");
  if (!root) {
    return;
  }

  const form = document.querySelector("[data-article-account-filters]");
  const codeInput = document.querySelector("[data-article-account-code]");
  const descriptionInput = document.querySelector("[data-article-account-description]");
  const choice = document.querySelector("[data-article-account-choice]");
  const lookup = document.querySelector("[data-article-account-lookup]");
  const lookupFrame = lookup?.querySelector(".stock-load-article-lookup-frame");
  const lookupBody = lookup?.querySelector("tbody");
  const articleModal = document.querySelector("[data-article-account-article-modal]");
  const articleModalFrame = document.querySelector("[data-article-account-article-modal-frame]");
  const documentModal = document.querySelector("[data-article-account-document-modal]");
  const documentModalFrame = document.querySelector("[data-article-account-document-modal-frame]");
  const grid = document.querySelector(".article-account-grid-frame");
  const table = document.querySelector(".article-account-grid");
  const rows = Array.from(document.querySelectorAll("[data-article-account-row]"));
  const sortHeaders = Array.from(document.querySelectorAll(".article-account-grid th[data-sort-key]"));
  const actionButtons = Array.from(document.querySelectorAll("[data-article-account-action]"));
  const choiceFields = {
    code: choice?.querySelector("[data-choice-code]"),
    description: choice?.querySelector("[data-choice-description]"),
    category: choice?.querySelector("[data-choice-category]"),
    group: choice?.querySelector("[data-choice-group]"),
    subgroup: choice?.querySelector("[data-choice-subgroup]"),
    unit: choice?.querySelector("[data-choice-unit]"),
    price: choice?.querySelector("[data-choice-price]")
  };
  const lookupFields = {
    search: lookup?.querySelector("[data-article-search]"),
    category: lookup?.querySelector("[data-article-category]"),
    supplier: lookup?.querySelector("[data-article-supplier]"),
    count: lookup?.querySelector("[data-article-count]")
  };

  let lookupRows = [];
  let lookupFilteredRows = [];
  let lookupSelectedIndex = -1;
  let lookupSortKey = "description";
  let lookupSortDirection = "asc";
  let sortState = { key: "", direction: "asc" };
  const isHostedModal = window.parent !== window || new URLSearchParams(window.location.search).get("modal") === "1";

  const closeHostedModal = () => {
    if (!isHostedModal) {
      window.location.href = "/";
      return;
    }

    window.parent.postMessage({ type: "micronote:article-account-cancel" }, "*");
  };

  const escapeHtml = (value) => String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");

  const currentReturnUrl = () => `${window.location.pathname}${window.location.search}`;

  const submitFilters = () => {
    form?.requestSubmit();
  };

  const formatMoney = (value) => {
    const number = Number(value) || 0;
    return number.toLocaleString("it-IT", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  };

  const clearChoice = () => {
    Object.values(choiceFields).forEach((field) => {
      if (field) {
        field.value = "";
      }
    });
  };

  const fillChoice = (row) => {
    if (!row) {
      clearChoice();
      return;
    }

    if (choiceFields.code) {
      choiceFields.code.value = row.code ?? "";
    }
    if (choiceFields.description) {
      choiceFields.description.value = row.description ?? "";
    }
    if (choiceFields.category) {
      choiceFields.category.value = row.category ?? "";
    }
    if (choiceFields.group) {
      choiceFields.group.value = row.group ?? "";
    }
    if (choiceFields.subgroup) {
      choiceFields.subgroup.value = row.subgroup ?? "";
    }
    if (choiceFields.unit) {
      choiceFields.unit.value = row.unitMeasure ?? "";
    }
    if (choiceFields.price) {
      choiceFields.price.value = formatMoney(row.price ?? 0);
    }
  };

  const findArticle = async (code) => {
    const normalized = String(code ?? "").trim();
    if (!normalized) {
      return null;
    }

    const local = lookupRows.find((row) => String(row.code ?? "").toLocaleLowerCase("it") === normalized.toLocaleLowerCase("it"));
    if (local) {
      return local;
    }

    const response = await fetch(`/api/articoli?q=${encodeURIComponent(normalized)}`, {
      headers: { "Accept": "application/json" }
    });
    const payload = await response.json();
    const rows = Array.isArray(payload.rows) ? payload.rows : [];
    return rows.find((row) => String(row.code ?? "").toLocaleLowerCase("it") === normalized.toLocaleLowerCase("it"))
      ?? null;
  };

  const showChoiceCodeMessage = (message) => {
    const refocusCode = () => {
      choiceFields.code?.focus();
      choiceFields.code?.select?.();
    };

    if (window.MicronoteMessageBox?.show) {
      window.MicronoteMessageBox.show({
        title: "Scelta articolo",
        message,
        onConfirm: refocusCode
      });
    } else {
      window.alert(message);
      refocusCode();
    }
  };

  const validateChoiceCode = async (showMessage = false) => {
    const code = String(choiceFields.code?.value ?? "").trim();
    if (!code) {
      clearChoice();
      return null;
    }

    const article = await findArticle(code);
    if (!article) {
      if (choiceFields.description) {
        choiceFields.description.value = "";
      }
      if (choiceFields.category) {
        choiceFields.category.value = "";
      }
      if (choiceFields.group) {
        choiceFields.group.value = "";
      }
      if (choiceFields.subgroup) {
        choiceFields.subgroup.value = "";
      }
      if (choiceFields.unit) {
        choiceFields.unit.value = "";
      }
      if (choiceFields.price) {
        choiceFields.price.value = "";
      }
      if (showMessage) {
        showChoiceCodeMessage("Articolo non trovato.");
      }
      return null;
    }

    fillChoice(article);
    return article;
  };

  const openChoice = async () => {
    if (!choice) {
      openLookup();
      return;
    }

    const currentCode = codeInput?.value || "";
    choice.hidden = false;
    document.body.classList.add("lookup-open");
    if (currentCode) {
      fillChoice(await findArticle(currentCode) ?? {
        code: currentCode,
        description: descriptionInput?.value || ""
      });
    } else {
      clearChoice();
    }
    window.setTimeout(() => choiceFields.code?.focus(), 0);
  };

  const closeChoice = (fromCancel = false) => {
    if (!choice) {
      return;
    }

    choice.hidden = true;
    document.body.classList.remove("lookup-open");
    if (fromCancel && !codeInput?.value) {
      window.location.href = "/";
      return;
    }

    document.querySelector("[data-article-account-change]")?.focus();
  };

  const confirmChoice = async () => {
    const code = String(choiceFields.code?.value ?? "").trim();
    if (!code) {
      showChoiceCodeMessage("Selezionare un articolo.");
      return;
    }

    const article = await findArticle(code);
    if (!article) {
      showChoiceCodeMessage("Articolo non trovato.");
      return;
    }

    if (codeInput) {
      codeInput.value = article.code ?? code;
    }
    if (descriptionInput) {
      descriptionInput.value = article.description ?? "";
    }
    closeChoice();
    submitFilters();
  };

  const buildLookupOptions = (select, sourceRows, codeKey, labelKey) => {
    if (!select) {
      return;
    }

    const seen = new Set();
    const options = [{ code: "", label: "" }];
    sourceRows.forEach((row) => {
      const code = String(row[codeKey] ?? "");
      const label = String(row[labelKey] ?? "");
      if (!code || seen.has(code)) {
        return;
      }

      seen.add(code);
      options.push({ code, label });
    });

    select.innerHTML = options
      .sort((left, right) => String(left.label).localeCompare(String(right.label), "it", { numeric: true }))
      .map((option) => `<option value="${escapeHtml(option.code)}">${escapeHtml(option.label)}</option>`)
      .join("");
  };

  const sortLookupRows = () => {
    const direction = lookupSortDirection === "desc" ? -1 : 1;
    lookupFilteredRows.sort((left, right) => {
      if (lookupSortKey === "price" || lookupSortKey === "vatRate") {
        return ((Number(left[lookupSortKey]) || 0) - (Number(right[lookupSortKey]) || 0)) * direction;
      }

      return String(left[lookupSortKey] ?? "").localeCompare(String(right[lookupSortKey] ?? ""), "it", {
        sensitivity: "base",
        numeric: true
      }) * direction;
    });
  };

  const ensureLookupSelectedVisible = (direction = 0) => {
    const row = lookup?.querySelector("tbody tr.selected");
    if (!row || !lookupFrame) {
      return;
    }

    const frameTop = lookupFrame.scrollTop;
    const frameBottom = frameTop + lookupFrame.clientHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;

    if (rowTop < frameTop) {
      lookupFrame.scrollTop = rowTop;
    } else if (rowBottom > frameBottom) {
      lookupFrame.scrollTop = rowBottom - lookupFrame.clientHeight;
    } else if (direction === 0) {
      row.scrollIntoView({ block: "nearest" });
    }
  };

  const setLookupSelectedIndex = (index, direction = 0) => {
    if (index < 0 || index >= lookupFilteredRows.length) {
      return;
    }

    lookupSelectedIndex = index;
    lookup?.querySelectorAll("tbody tr").forEach((row) => {
      row.classList.toggle("selected", Number(row.dataset.index) === lookupSelectedIndex);
    });
    ensureLookupSelectedVisible(direction);
  };

  const renderLookupRows = () => {
    if (!lookupBody) {
      return;
    }

    lookup?.querySelectorAll("[data-article-sort]").forEach((button) => {
      const active = button.dataset.articleSort === lookupSortKey;
      button.classList.toggle("is-active", active);
      button.dataset.direction = active ? lookupSortDirection : "";
    });

    lookupBody.innerHTML = lookupFilteredRows.map((row, index) => `
      <tr data-index="${index}" class="${index === lookupSelectedIndex ? "selected" : ""}">
        <td>${escapeHtml(row.code)}</td>
        <td>${escapeHtml(row.description)}</td>
        <td>${escapeHtml(row.category)}</td>
        <td>${escapeHtml(row.group)}</td>
        <td>${escapeHtml(row.subgroup)}</td>
        <td>${escapeHtml(row.price ?? "")}</td>
        <td>${escapeHtml(row.vatRate ?? "")}</td>
      </tr>
    `).join("");

    if (lookupFields.count) {
      lookupFields.count.value = String(lookupFilteredRows.length);
    }
  };

  const filterLookupRows = () => {
    const search = String(lookupFields.search?.value ?? "").trim().toLocaleLowerCase("it");
    const category = lookupFields.category?.value ?? "";
    const supplier = lookupFields.supplier?.value ?? "";

    lookupFilteredRows = lookupRows.filter((row) => {
      const matchesSearch = !search
        || String(row.code ?? "").toLocaleLowerCase("it").includes(search)
        || String(row.description ?? "").toLocaleLowerCase("it").includes(search);
      const matchesCategory = !category || String(row.categoryCode ?? "") === category;
      const matchesSupplier = !supplier || String(row.supplierCode ?? "") === supplier;
      return matchesSearch && matchesCategory && matchesSupplier;
    });

    sortLookupRows();
    lookupSelectedIndex = lookupFilteredRows.length > 0 ? 0 : -1;
    renderLookupRows();
  };

  const loadLookupRows = async () => {
    const response = await fetch("/api/articoli", {
      headers: { "Accept": "application/json" }
    });
    const payload = await response.json();
    lookupRows = Array.isArray(payload.rows) ? payload.rows : [];
    buildLookupOptions(lookupFields.category, lookupRows, "categoryCode", "category");
    buildLookupOptions(lookupFields.supplier, lookupRows, "supplierCode", "supplier");
    filterLookupRows();
  };

  const closeLookup = (fromCancel = false) => {
    if (!lookup) {
      return;
    }

    lookup.hidden = true;
    if (choice && !choice.hidden) {
      document.body.classList.add("lookup-open");
      choice.querySelector("[data-choice-lookup]")?.focus();
    } else {
      document.body.classList.remove("lookup-open");
      if (fromCancel && !codeInput?.value) {
        window.location.href = "/";
        return;
      }

      document.querySelector("[data-article-account-change]")?.focus();
    }
  };

  const openLookup = async () => {
    if (!lookup || !lookupFields.search) {
      return;
    }

    lookup.hidden = false;
    document.body.classList.add("lookup-open");
    lookupFields.search.value = "";
    await loadLookupRows();
    window.setTimeout(() => lookupFields.search?.focus(), 0);
  };

  const selectLookupRow = () => {
    const row = lookupFilteredRows[lookupSelectedIndex];
    if (!row) {
      return;
    }

    if (choice && !choice.hidden) {
      fillChoice(row);
      closeLookup();
      return;
    }

    if (codeInput) {
      codeInput.value = row.code ?? "";
    }
    if (descriptionInput) {
      descriptionInput.value = row.description ?? "";
    }
    closeLookup();
    submitFilters();
  };

  const selectedRow = () => document.querySelector("[data-article-account-row].selected")
    || document.querySelector("[data-article-account-row].selected-row");

  const visibleRows = () => rows.filter((row) => !row.hidden);

  const ensureVisible = (row, direction = 0) => {
    if (!grid || !row) {
      return;
    }

    const gridTop = grid.scrollTop;
    const gridBottom = gridTop + grid.clientHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;

    if (rowTop < gridTop) {
      grid.scrollTop = rowTop;
    } else if (rowBottom > gridBottom) {
      grid.scrollTop = rowBottom - grid.clientHeight;
    } else if (direction === 0) {
      row.scrollIntoView({ block: "nearest" });
    }
  };

  const selectRow = (row, focus = false, direction = 0) => {
    if (!row) {
      return;
    }

    rows.forEach((candidate) => {
      candidate.classList.toggle("selected", candidate === row);
      candidate.classList.toggle("selected-row", candidate === row);
      candidate.tabIndex = candidate === row ? 0 : -1;
    });

    if (focus) {
      row.focus({ preventScroll: true });
    }

    ensureVisible(row, direction);
  };

  const navigateGrid = (event, currentRow) => {
    const currentRows = visibleRows();
    const currentIndex = currentRows.indexOf(currentRow);
    if (currentRows.length === 0 || currentIndex < 0) {
      return;
    }

    if (event.key === "ArrowDown") {
      event.preventDefault();
      selectRow(currentRows[Math.min(currentIndex + 1, currentRows.length - 1)], true, 1);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      selectRow(currentRows[Math.max(currentIndex - 1, 0)], true, -1);
    } else if (event.key === "Home") {
      event.preventDefault();
      selectRow(currentRows[0], true, -1);
    } else if (event.key === "End") {
      event.preventDefault();
      selectRow(currentRows[currentRows.length - 1], true, 1);
    }
  };

  const requireSelection = () => {
    const row = selectedRow();
    if (row) {
      return row;
    }

    window.MicronoteMessageBox?.show({
      title: "Estratto conto articolo",
      message: "Selezionare una riga."
    });
    return null;
  };

  const openArticle = () => {
    const code = codeInput?.value || "";
    if (!code) {
      window.MicronoteMessageBox?.show({
        title: "Vedi articolo",
        message: "Selezionare prima un articolo."
      });
      return;
    }

    const url = `/Articoli/Edit/${encodeURIComponent(code)}?azione=101&returnUrl=${encodeURIComponent(currentReturnUrl())}`;
    if (!articleModal || !articleModalFrame) {
      window.location.href = url;
      return;
    }

    articleModalFrame.src = url;
    articleModal.hidden = false;
    document.body.classList.add("purchase-invoice-payment-modal-open");
    articleModalFrame.focus();
  };

  const closeArticleModal = () => {
    if (!articleModal) {
      return;
    }

    articleModal.hidden = true;
    document.body.classList.remove("purchase-invoice-payment-modal-open");
    articleModalFrame?.removeAttribute("src");
    document.querySelector("[data-article-account-action='article']")?.focus();
  };

  const openDocumentModal = (url) => {
    if (!documentModal || !documentModalFrame) {
      window.location.href = url;
      return;
    }

    documentModalFrame.src = url;
    documentModal.hidden = false;
    document.body.classList.add("purchase-invoice-payment-modal-open");
    documentModalFrame.focus();
  };

  const closeDocumentModal = () => {
    if (!documentModal) {
      return;
    }

    documentModal.hidden = true;
    document.body.classList.remove("purchase-invoice-payment-modal-open");
    documentModalFrame?.removeAttribute("src");
    document.querySelector("[data-article-account-action='movement']")?.focus();
  };

  const openMovement = (row) => {
    const stockLoadId = Number.parseInt(row.dataset.stockLoadId || "0", 10);
    const saleId = Number.parseInt(row.dataset.saleId || "0", 10);
    const sector = Number.parseInt(row.dataset.sector || "0", 10);
    const returnUrl = encodeURIComponent(currentReturnUrl());

    if (sector === 10 && stockLoadId > 0) {
      openDocumentModal(`/CaricoAcquisti/Edit?id=${stockLoadId}&azione=101&returnTo=stockMovement&returnUrl=${returnUrl}`);
      return;
    }

    if (sector === 30 && saleId > 0) {
      openDocumentModal(`/Vendite/Index?saleId=${saleId}&azione=101&returnTo=${returnUrl}`);
      return;
    }

    window.MicronoteMessageBox?.show({
      title: "Vedi movimento",
      message: "Movimento origine non disponibile.",
      detail: `Settore ${Number.isFinite(sector) ? sector : ""}`
    });
  };

  const sortRows = (header) => {
    if (!table) {
      return;
    }

    const key = header.dataset.sortKey || "";
    const type = header.dataset.sortType || "text";
    const direction = sortState.key === key && sortState.direction === "asc" ? "desc" : "asc";
    sortState = { key, direction };

    const sorted = visibleRows().sort((left, right) => {
      const leftValue = left.dataset[`sort${key.charAt(0).toUpperCase()}${key.slice(1)}`] ?? "";
      const rightValue = right.dataset[`sort${key.charAt(0).toUpperCase()}${key.slice(1)}`] ?? "";
      const result = type === "number"
        ? (Number.parseFloat(leftValue) || 0) - (Number.parseFloat(rightValue) || 0)
        : String(leftValue).localeCompare(String(rightValue), "it", { sensitivity: "base", numeric: true });
      return direction === "asc" ? result : -result;
    });

    sorted.forEach((row) => table.tBodies[0].appendChild(row));
    sortHeaders.forEach((candidate) => candidate.setAttribute("aria-sort", candidate === header ? direction : "none"));
    if (sorted.length > 0) {
      selectRow(sorted[0], true, 0);
    }
  };

  document.querySelector("[data-article-account-change]")?.addEventListener("click", openChoice);
  choice?.querySelector("[data-choice-lookup]")?.addEventListener("click", openLookup);
  choice?.querySelector("[data-choice-confirm]")?.addEventListener("click", confirmChoice);
  choice?.querySelector("[data-choice-cancel]")?.addEventListener("click", () => closeChoice(true));
  choiceFields.code?.addEventListener("change", () => {
    validateChoiceCode(false);
  });
  choiceFields.code?.addEventListener("blur", () => {
    validateChoiceCode(false);
  });
  choiceFields.code?.addEventListener("keydown", async (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      const article = await validateChoiceCode(true);
      if (article) {
        choice.querySelector("[data-choice-confirm]")?.focus();
      }
    } else if (event.key === "F4") {
      event.preventDefault();
      openLookup();
    } else if (event.key === "Tab") {
      event.preventDefault();
      const article = await validateChoiceCode(true);
      if (article) {
        choice.querySelector("[data-choice-lookup]")?.focus();
      }
    } else if (event.key === "Escape") {
      event.preventDefault();
      closeChoice(true);
    }
  });
  lookup?.querySelector("[data-article-cancel]")?.addEventListener("click", () => closeLookup(true));
  lookup?.querySelector("[data-article-ok]")?.addEventListener("click", selectLookupRow);
  lookupFields.search?.addEventListener("input", filterLookupRows);
  lookupFields.category?.addEventListener("change", filterLookupRows);
  lookupFields.supplier?.addEventListener("change", filterLookupRows);
  lookup?.querySelector("thead")?.addEventListener("click", (event) => {
    const button = event.target.closest("[data-article-sort]");
    if (!button) {
      return;
    }

    const key = button.dataset.articleSort;
    lookupSortDirection = lookupSortKey === key && lookupSortDirection === "asc" ? "desc" : "asc";
    lookupSortKey = key;
    filterLookupRows();
  });
  lookupBody?.addEventListener("click", (event) => {
    const row = event.target.closest("tr[data-index]");
    if (!row) {
      return;
    }

    setLookupSelectedIndex(Number(row.dataset.index));
    lookupFrame?.focus({ preventScroll: true });
  });
  lookupBody?.addEventListener("dblclick", selectLookupRow);
  lookupFrame?.addEventListener("keydown", (event) => {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setLookupSelectedIndex(Math.min(lookupFilteredRows.length - 1, lookupSelectedIndex + 1), 1);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setLookupSelectedIndex(Math.max(0, lookupSelectedIndex - 1), -1);
    } else if (event.key === "Home") {
      event.preventDefault();
      setLookupSelectedIndex(0, -1);
    } else if (event.key === "End") {
      event.preventDefault();
      setLookupSelectedIndex(lookupFilteredRows.length - 1, 1);
    } else if (event.key === "Enter") {
      event.preventDefault();
      selectLookupRow();
    } else if (event.key === "Escape") {
      event.preventDefault();
      closeLookup(true);
    }
  });
  lookupFields.search?.addEventListener("keydown", (event) => {
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      lookupFrame?.focus({ preventScroll: true });
    } else if (event.key === "Enter") {
      event.preventDefault();
      selectLookupRow();
    } else if (event.key === "Escape") {
      event.preventDefault();
      closeLookup(true);
    }
  });

  document.querySelectorAll("[data-article-account-filter]").forEach((field) => {
    field.addEventListener("change", submitFilters);
  });

  rows.forEach((row) => {
    row.addEventListener("click", () => selectRow(row, true, 0));
    row.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        openMovement(row);
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
      const action = button.dataset.articleAccountAction;
      if (action === "exit") {
        closeHostedModal();
        return;
      }

      if (action === "print") {
        window.MicronoteMessageBox?.show({
          title: "Stampa",
          message: "Stampa estratto conto articolo.",
          detail: "Funzione in preparazione."
        });
        return;
      }

      if (action === "article") {
        openArticle();
        return;
      }

      if (action === "movement") {
        const row = requireSelection();
        if (row) {
          openMovement(row);
        }
      }
    });
  });

  document.querySelector(".module-title-logo")?.addEventListener("click", (event) => {
    if (!isHostedModal) {
      return;
    }

    event.preventDefault();
    closeHostedModal();
  });

  articleModal?.addEventListener("click", (event) => {
    if (event.target === articleModal) {
      closeArticleModal();
    }
  });
  documentModal?.addEventListener("click", (event) => {
    if (event.target === documentModal) {
      closeDocumentModal();
    }
  });
  window.addEventListener("message", (event) => {
    if (event.origin !== window.location.origin) {
      return;
    }

    if (event.data?.type === "micronote:article-cancel" || event.data?.type === "micronote:article-saved") {
      closeArticleModal();
    }

    if (event.data?.type === "micronote:stock-load-cancel" || event.data?.type === "micronote:sales-cancel") {
      closeDocumentModal();
    }
  });

  let lastScrollTop = grid?.scrollTop ?? 0;
  let scrollFrame = 0;
  grid?.addEventListener("scroll", () => {
    if (scrollFrame) {
      window.cancelAnimationFrame(scrollFrame);
    }

    scrollFrame = window.requestAnimationFrame(() => {
      scrollFrame = 0;
      const currentScrollTop = grid.scrollTop;
      const delta = currentScrollTop - lastScrollTop;
      lastScrollTop = currentScrollTop;
      const selected = selectedRow();
      if (!selected || delta === 0) {
        return;
      }

      const selectedTop = selected.offsetTop;
      const selectedBottom = selectedTop + selected.offsetHeight;
      const isVisible = selectedTop >= grid.scrollTop && selectedBottom <= grid.scrollTop + grid.clientHeight;
      if (isVisible) {
        return;
      }

      const visible = visibleRows().filter((row) => {
        const rowTop = row.offsetTop;
        const rowBottom = rowTop + row.offsetHeight;
        return rowTop >= grid.scrollTop && rowBottom <= grid.scrollTop + grid.clientHeight;
      });
      if (visible.length > 0) {
        selectRow(delta > 0 ? visible[0] : visible[visible.length - 1], true, delta > 0 ? 1 : -1);
      }
    });
  }, { passive: true });

  if (rows.length > 0) {
    selectRow(selectedRow() || rows[0], true, 0);
  }

  if (root.dataset.hasArticle !== "true") {
    window.setTimeout(openChoice, 100);
  }
});
