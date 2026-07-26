document.addEventListener("DOMContentLoaded", () => {
  const filterForm = document.querySelector("[data-stock-filters]");
  const grid = document.querySelector(".stock-movements-grid-frame");
  const table = grid?.querySelector("table");
  let rows = Array.from(table?.querySelectorAll("[data-stock-row]") ?? []);
  const sortHeaders = Array.from(table?.querySelectorAll("th[data-sort-key]") ?? []);
  const actionButtons = Array.from(document.querySelectorAll("[data-stock-action]"));
  const articleCodeFilter = document.querySelector("[data-stock-article-code]");
  const articleDescriptionFilter = document.querySelector("[data-stock-article-description]");
  const articleLookup = document.querySelector("[data-stock-article-lookup]");
  const articleCardModal = document.querySelector("[data-stock-movement-article-modal]");
  const articleCardModalFrame = document.querySelector("[data-stock-movement-article-modal-frame]");
  const documentModal = document.querySelector("[data-stock-movement-document-modal]");
  const documentModalFrame = document.querySelector("[data-stock-movement-document-modal-frame]");
  let filterTimer;
  let sortState = { key: "", direction: "asc" };
  let articleLookupRows = [];
  let articleLookupFilteredRows = [];
  let articleLookupSelectedIndex = -1;
  let articleLookupSortKey = "description";
  let articleLookupSortDirection = "asc";
  let datePickerPanel = null;
  let activeDateField = null;
  let activeDateMonth = null;

  const articleLookupFields = {
    search: articleLookup?.querySelector("[data-article-search]"),
    category: articleLookup?.querySelector("[data-article-category]"),
    supplier: articleLookup?.querySelector("[data-article-supplier]"),
    count: articleLookup?.querySelector("[data-article-count]"),
    body: articleLookup?.querySelector("tbody"),
    frame: articleLookup?.querySelector(".stock-load-article-lookup-frame")
  };

  const dateDisplayFields = Array.from(document.querySelectorAll("[data-stock-date-display]"));

  const padDatePart = (value) => String(value).padStart(2, "0");

  const isRealDateParts = (day, month, year) => {
    if (!Number.isInteger(day) || !Number.isInteger(month) || !Number.isInteger(year)) {
      return false;
    }

    const date = new Date(year, month - 1, day);
    return date.getFullYear() === year
      && date.getMonth() === month - 1
      && date.getDate() === day;
  };

  const toIsoDate = (value) => {
    const text = String(value ?? "").trim();
    const isoMatch = /^(\d{4})-(\d{2})-(\d{2})$/.exec(text);
    if (isoMatch) {
      const year = Number.parseInt(isoMatch[1], 10);
      const month = Number.parseInt(isoMatch[2], 10);
      const day = Number.parseInt(isoMatch[3], 10);
      return isRealDateParts(day, month, year) ? text : "";
    }

    const match = /^(\d{1,2})[/-](\d{1,2})[/-](\d{2}|\d{4})$/.exec(text);
    if (!match) {
      return "";
    }

    const day = Number.parseInt(match[1], 10);
    const month = Number.parseInt(match[2], 10);
    const currentCentury = Math.floor(new Date().getFullYear() / 100) * 100;
    const year = match[3].length === 2
      ? currentCentury + Number.parseInt(match[3], 10)
      : Number.parseInt(match[3], 10);

    return isRealDateParts(day, month, year)
      ? `${year}-${padDatePart(month)}-${padDatePart(day)}`
      : "";
  };

  const toDisplayDate = (value) => {
    const iso = toIsoDate(value);
    if (!iso) {
      return "";
    }

    const [year, month, day] = iso.split("-");
    return `${day}/${month}/${year}`;
  };

  const dateFromField = (field) => {
    const iso = toIsoDate(field?.value);
    if (!iso) {
      return null;
    }

    const [year, month, day] = iso.split("-").map((part) => Number.parseInt(part, 10));
    return new Date(year, month - 1, day);
  };

  const formatDisplayDate = (date) =>
    `${padDatePart(date.getDate())}/${padDatePart(date.getMonth() + 1)}/${date.getFullYear()}`;

  const hiddenDateField = (field) =>
    field.closest("[data-date-control]")?.querySelector("[data-stock-date-hidden]");

  const syncDateHidden = (field, submit = false) => {
    const hidden = hiddenDateField(field);
    const iso = toIsoDate(field.value);
    if (!hidden || !iso) {
      return false;
    }

    field.value = toDisplayDate(iso);
    if (hidden.value !== iso) {
      hidden.value = iso;
      hidden.dispatchEvent(new Event("change", { bubbles: true }));
    } else if (submit) {
      submitFilters();
    }

    return true;
  };

  const buildDatePickerPanel = () => {
    if (datePickerPanel) {
      return datePickerPanel;
    }

    datePickerPanel = document.createElement("div");
    datePickerPanel.className = "micronote-date-picker";
    datePickerPanel.hidden = true;
    datePickerPanel.setAttribute("role", "dialog");
    datePickerPanel.setAttribute("aria-label", "Calendario");
    document.body.appendChild(datePickerPanel);
    return datePickerPanel;
  };

  const closeDatePicker = () => {
    if (!datePickerPanel) {
      return;
    }

    datePickerPanel.hidden = true;
    activeDateField = null;
  };

  const renderDatePicker = () => {
    const panel = buildDatePickerPanel();
    if (!activeDateField || !activeDateMonth) {
      panel.hidden = true;
      return;
    }

    const selectedDate = dateFromField(activeDateField);
    const year = activeDateMonth.getFullYear();
    const month = activeDateMonth.getMonth();
    const monthLabel = activeDateMonth.toLocaleDateString("it-IT", { month: "long", year: "numeric" });
    const firstDay = new Date(year, month, 1);
    const startOffset = (firstDay.getDay() + 6) % 7;
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const weekdays = ["Lu", "Ma", "Me", "Gi", "Ve", "Sa", "Do"];
    const cells = [];

    for (let index = 0; index < startOffset; index += 1) {
      cells.push('<span class="micronote-date-picker-empty"></span>');
    }

    for (let day = 1; day <= daysInMonth; day += 1) {
      const isSelected = selectedDate
        && selectedDate.getFullYear() === year
        && selectedDate.getMonth() === month
        && selectedDate.getDate() === day;
      cells.push(`<button type="button" class="${isSelected ? "is-selected" : ""}" data-date-picker-day="${day}">${day}</button>`);
    }

    panel.innerHTML = `
      <div class="micronote-date-picker-head">
        <button type="button" data-date-picker-prev>&lt;</button>
        <strong>${monthLabel}</strong>
        <button type="button" data-date-picker-next>&gt;</button>
      </div>
      <div class="micronote-date-picker-weekdays">
        ${weekdays.map((day) => `<span>${day}</span>`).join("")}
      </div>
      <div class="micronote-date-picker-days">
        ${cells.join("")}
      </div>
    `;
  };

  const positionDatePicker = () => {
    if (!activeDateField || !datePickerPanel) {
      return;
    }

    const rect = activeDateField.getBoundingClientRect();
    datePickerPanel.style.left = `${Math.max(8, rect.right - datePickerPanel.offsetWidth + window.scrollX)}px`;
    datePickerPanel.style.top = `${rect.bottom + 4 + window.scrollY}px`;
  };

  const openDatePicker = (field) => {
    activeDateField = field;
    const baseDate = dateFromField(field) ?? new Date();
    activeDateMonth = new Date(baseDate.getFullYear(), baseDate.getMonth(), 1);
    const panel = buildDatePickerPanel();
    renderDatePicker();
    panel.hidden = false;
    positionDatePicker();
  };

  const normalizeDateInput = (field) => {
    const digits = field.value.replace(/\D/g, "").slice(0, 8);
    let text = digits;

    if (digits.length > 4) {
      text = `${digits.slice(0, 2)}/${digits.slice(2, 4)}/${digits.slice(4)}`;
    } else if (digits.length > 2) {
      text = `${digits.slice(0, 2)}/${digits.slice(2)}`;
    }

    field.value = text.slice(0, 10);
  };

  dateDisplayFields.forEach((field) => {
    field.addEventListener("input", () => normalizeDateInput(field));
    field.addEventListener("blur", () => syncDateHidden(field, true));
    field.addEventListener("change", () => syncDateHidden(field, true));
    field.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        syncDateHidden(field, true);
      }
    });
  });

  const submitFilters = () => {
    if (!filterForm) {
      return;
    }

    window.clearTimeout(filterTimer);
    filterTimer = window.setTimeout(() => filterForm.requestSubmit(), 150);
  };

  filterForm?.querySelectorAll("input[name], select[name]").forEach((field) => {
    field.addEventListener("change", submitFilters);
  });

  filterForm?.querySelector("input[name='articleCode']")?.addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      submitFilters();
    }
  });

  document.addEventListener("mousedown", (event) => {
    const target = event.target;
    if (target instanceof Element && target.closest(".micronote-date-picker")) {
      event.preventDefault();
    }
  });

  document.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    const dateButton = target.closest("[data-date-picker-button]");
    if (dateButton) {
      event.preventDefault();
      const field = dateButton.closest("[data-date-control]")?.querySelector("[data-stock-date-display]");
      if (field instanceof HTMLInputElement) {
        if (activeDateField === field && datePickerPanel && !datePickerPanel.hidden) {
          closeDatePicker();
          return;
        }

        openDatePicker(field);
        field.focus();
      }
      return;
    }

    if (target.closest(".micronote-date-picker")) {
      event.preventDefault();
      const previousButton = target.closest("[data-date-picker-prev]");
      const nextButton = target.closest("[data-date-picker-next]");
      const dayButton = target.closest("[data-date-picker-day]");

      if (previousButton && activeDateMonth) {
        activeDateMonth = new Date(activeDateMonth.getFullYear(), activeDateMonth.getMonth() - 1, 1);
        renderDatePicker();
        positionDatePicker();
        return;
      }

      if (nextButton && activeDateMonth) {
        activeDateMonth = new Date(activeDateMonth.getFullYear(), activeDateMonth.getMonth() + 1, 1);
        renderDatePicker();
        positionDatePicker();
        return;
      }

      if (dayButton && activeDateField && activeDateMonth) {
        const day = Number.parseInt(dayButton.getAttribute("data-date-picker-day") ?? "0", 10);
        activeDateField.value = formatDisplayDate(new Date(activeDateMonth.getFullYear(), activeDateMonth.getMonth(), day));
        syncDateHidden(activeDateField, true);
        closeDatePicker();
      }

      return;
    }

    if (!target.closest("[data-date-control]")) {
      closeDatePicker();
    }
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && datePickerPanel && !datePickerPanel.hidden) {
      event.preventDefault();
      closeDatePicker();
      return;
    }

    if (event.key === "Escape" && document.body.classList.contains("lookup-open")) {
      return;
    }

    if (event.key === "Escape" && articleCardModal && !articleCardModal.hidden) {
      event.preventDefault();
      closeArticleCardModal();
      return;
    }

    if (event.key === "Escape" && documentModal && !documentModal.hidden) {
      event.preventDefault();
      closeDocumentModal();
      return;
    }

    if (event.key === "Escape") {
      event.preventDefault();
      window.location.href = "/";
    }
  });

  const visibleRows = () => rows.filter((row) => !row.hidden);
  const selectedRow = () => table?.querySelector("[data-stock-row].selected");
  const currentReturnUrl = () => window.location.pathname + window.location.search;
  const toKebab = (value) => value.replace(/[A-Z]/g, (match) => `-${match.toLowerCase()}`);

  const escapeHtml = (value) => String(value ?? "").replace(/[&<>"']/g, (char) => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    "\"": "&quot;",
    "'": "&#039;"
  }[char]));

  const formatLookupMoney = (value) => {
    const number = Number(value ?? 0);
    if (!Number.isFinite(number) || number === 0) {
      return "";
    }

    return number.toLocaleString("it-IT", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  };

  const formatLookupPercent = (value) => {
    const formatted = formatLookupMoney(value);
    return formatted ? `${formatted} %` : "";
  };

  const buildLookupOptions = (select, sourceRows, codeKey, labelKey) => {
    if (!select) {
      return;
    }

    const options = new Map();
    sourceRows.forEach((row) => {
      const code = row[codeKey];
      const label = row[labelKey];
      if (code && label && !options.has(String(code))) {
        options.set(String(code), label);
      }
    });

    select.innerHTML = `<option value=""></option>${Array.from(options.entries())
      .sort((left, right) => String(left[1]).localeCompare(String(right[1]), "it", { sensitivity: "base", numeric: true }))
      .map(([code, label]) => `<option value="${escapeHtml(code)}">${escapeHtml(label)}</option>`)
      .join("")}`;
  };

  const sortArticleRows = () => {
    const direction = articleLookupSortDirection === "desc" ? -1 : 1;
    articleLookupFilteredRows.sort((left, right) => {
      if (articleLookupSortKey === "price" || articleLookupSortKey === "vatRate") {
        return ((Number(left[articleLookupSortKey]) || 0) - (Number(right[articleLookupSortKey]) || 0)) * direction;
      }

      return String(left[articleLookupSortKey] ?? "").localeCompare(String(right[articleLookupSortKey] ?? ""), "it", {
        sensitivity: "base",
        numeric: true
      }) * direction;
    });
  };

  const isArticleLookupRowVisible = (row) => {
    const frame = articleLookupFields.frame;
    if (!row || !frame) {
      return false;
    }

    const headerHeight = frame.querySelector("thead")?.offsetHeight || 0;
    const frameRect = frame.getBoundingClientRect();
    const rowRect = row.getBoundingClientRect();
    return rowRect.top >= frameRect.top + headerHeight + 1 && rowRect.bottom <= frameRect.bottom - 1;
  };

  const ensureArticleLookupSelectedVisible = (direction = 0) => {
    const row = articleLookup?.querySelector("tbody tr.selected");
    const frame = articleLookupFields.frame;
    if (!row || !frame) {
      return;
    }

    const headerHeight = frame.querySelector("thead")?.offsetHeight || 0;
    const visibleTop = frame.scrollTop + headerHeight;
    const visibleBottom = frame.scrollTop + frame.clientHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;

    if (rowTop >= visibleTop && rowBottom <= visibleBottom) {
      return;
    }

    if (direction >= 0 && rowBottom > visibleBottom) {
      frame.scrollTop = rowBottom - frame.clientHeight + 1;
      return;
    }

    if (direction <= 0 && rowTop < visibleTop) {
      frame.scrollTop = Math.max(rowTop - headerHeight - 1, 0);
    }
  };

  const setArticleLookupSelectedIndex = (index, direction = 0) => {
    if (index < 0 || index >= articleLookupFilteredRows.length) {
      return;
    }

    articleLookupSelectedIndex = index;
    articleLookup?.querySelectorAll("tbody tr").forEach((row) => {
      row.classList.toggle("selected", Number(row.dataset.index) === articleLookupSelectedIndex);
    });
    ensureArticleLookupSelectedVisible(direction);
  };

  const renderArticleLookupRows = () => {
    if (!articleLookupFields.body) {
      return;
    }

    articleLookup?.querySelectorAll("[data-article-sort]").forEach((button) => {
      const active = button.dataset.articleSort === articleLookupSortKey;
      button.classList.toggle("is-active", active);
      button.dataset.direction = active ? articleLookupSortDirection : "";
    });

    articleLookupFields.body.innerHTML = articleLookupFilteredRows.map((row, index) => `
      <tr data-index="${index}" class="${index === articleLookupSelectedIndex ? "selected" : ""}">
        <td>${escapeHtml(row.code)}</td>
        <td>${escapeHtml(row.description)}</td>
        <td>${escapeHtml(row.category)}</td>
        <td>${escapeHtml(row.group)}</td>
        <td>${escapeHtml(row.subgroup)}</td>
        <td>${formatLookupMoney(row.price)}</td>
        <td>${formatLookupPercent(row.vatRate)}</td>
      </tr>
    `).join("");

    if (articleLookupFields.count) {
      articleLookupFields.count.value = String(articleLookupFilteredRows.length);
    }

    ensureArticleLookupSelectedVisible();
  };

  const filterArticleLookupRows = () => {
    const search = String(articleLookupFields.search?.value ?? "").trim().toLocaleLowerCase("it");
    const category = articleLookupFields.category?.value ?? "";
    const supplier = articleLookupFields.supplier?.value ?? "";

    articleLookupFilteredRows = articleLookupRows.filter((row) => {
      const matchesSearch = !search
        || String(row.code ?? "").toLocaleLowerCase("it").includes(search)
        || String(row.description ?? "").toLocaleLowerCase("it").includes(search);
      const matchesCategory = !category || String(row.categoryCode ?? "") === category;
      const matchesSupplier = !supplier || String(row.supplierCode ?? "") === supplier;
      return matchesSearch && matchesCategory && matchesSupplier;
    });
    sortArticleRows();
    articleLookupSelectedIndex = articleLookupFilteredRows.length > 0 ? 0 : -1;
    renderArticleLookupRows();
  };

  const loadArticleLookupRows = async () => {
    const response = await fetch("/api/articoli", {
      headers: { "Accept": "application/json" }
    });
    const payload = await response.json();
    articleLookupRows = Array.isArray(payload.rows) ? payload.rows : [];
    buildLookupOptions(articleLookupFields.category, articleLookupRows, "categoryCode", "category");
    buildLookupOptions(articleLookupFields.supplier, articleLookupRows, "supplierCode", "supplier");
    filterArticleLookupRows();
  };

  const closeArticleLookup = () => {
    if (!articleLookup) {
      return;
    }

    articleLookup.hidden = true;
    document.body.classList.remove("lookup-open");
    articleCodeFilter?.focus();
  };

  const openArticleLookup = async () => {
    if (!articleLookup || !articleLookupFields.search) {
      return;
    }

    articleLookup.hidden = false;
    document.body.classList.add("lookup-open");
    articleLookupFields.search.value = "";
    await loadArticleLookupRows();
    window.setTimeout(() => articleLookupFields.search?.focus(), 0);
  };

  const selectArticleLookupRow = () => {
    const row = articleLookupFilteredRows[articleLookupSelectedIndex];
    if (!row || !articleCodeFilter || !articleDescriptionFilter) {
      return;
    }

    articleCodeFilter.value = row.code ?? "";
    articleDescriptionFilter.value = row.description ?? "";
    closeArticleLookup();
    submitFilters();
  };

  document.querySelector("[data-stock-article-lookup-open]")?.addEventListener("click", openArticleLookup);
  articleLookup?.querySelector("[data-article-cancel]")?.addEventListener("click", closeArticleLookup);
  articleLookup?.querySelector("[data-article-ok]")?.addEventListener("click", selectArticleLookupRow);
  articleLookupFields.search?.addEventListener("input", filterArticleLookupRows);
  articleLookupFields.category?.addEventListener("change", filterArticleLookupRows);
  articleLookupFields.supplier?.addEventListener("change", filterArticleLookupRows);
  articleLookup?.querySelector("thead")?.addEventListener("click", (event) => {
    const button = event.target.closest("[data-article-sort]");
    if (!button) {
      return;
    }

    const key = button.dataset.articleSort;
    articleLookupSortDirection = articleLookupSortKey === key && articleLookupSortDirection === "asc" ? "desc" : "asc";
    articleLookupSortKey = key;
    filterArticleLookupRows();
  });
  articleLookupFields.body?.addEventListener("click", (event) => {
    const row = event.target.closest("tr[data-index]");
    if (!row) {
      return;
    }

    setArticleLookupSelectedIndex(Number(row.dataset.index));
    articleLookupFields.frame?.focus({ preventScroll: true });
  });
  articleLookupFields.body?.addEventListener("dblclick", selectArticleLookupRow);
  articleLookupFields.frame?.addEventListener("keydown", (event) => {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setArticleLookupSelectedIndex(Math.min(articleLookupFilteredRows.length - 1, articleLookupSelectedIndex + 1), 1);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setArticleLookupSelectedIndex(Math.max(0, articleLookupSelectedIndex - 1), -1);
    } else if (event.key === "Enter") {
      event.preventDefault();
      selectArticleLookupRow();
    } else if (event.key === "Escape") {
      event.preventDefault();
      closeArticleLookup();
    }
  });
  articleLookupFields.search?.addEventListener("keydown", (event) => {
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      articleLookupFields.frame?.focus({ preventScroll: true });
    } else if (event.key === "Enter") {
      event.preventDefault();
      selectArticleLookupRow();
    } else if (event.key === "Escape") {
      event.preventDefault();
      closeArticleLookup();
    }
  });

  let lastArticleLookupScrollTop = 0;
  let articleLookupScrollFrame = 0;
  articleLookupFields.frame?.addEventListener("scroll", () => {
    if (articleLookupScrollFrame) {
      window.cancelAnimationFrame(articleLookupScrollFrame);
    }

    articleLookupScrollFrame = window.requestAnimationFrame(() => {
      articleLookupScrollFrame = 0;
      const frame = articleLookupFields.frame;
      const currentScrollTop = frame.scrollTop;
      const delta = currentScrollTop - lastArticleLookupScrollTop;
      lastArticleLookupScrollTop = currentScrollTop;
      const selected = frame.querySelector("tbody tr.selected");
      if (!selected || delta === 0 || isArticleLookupRowVisible(selected)) {
        return;
      }

      const visibleArticleRows = Array.from(frame.querySelectorAll("tbody tr[data-index]"))
        .filter(isArticleLookupRowVisible);
      if (visibleArticleRows.length === 0) {
        return;
      }

      const row = delta > 0 ? visibleArticleRows[0] : visibleArticleRows[visibleArticleRows.length - 1];
      setArticleLookupSelectedIndex(Number(row.dataset.index), delta > 0 ? 1 : -1);
    });
  }, { passive: true });

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

  const requireSelection = () => {
    const row = selectedRow();
    if (row) {
      return row;
    }

    window.MicronoteMessageBox?.show({
      title: "Movimenti di magazzino",
      message: "Selezionare un movimento dalla lista."
    });
    return null;
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

  const openArticle = (row) => {
    const code = row.dataset.articleCode || "";
    if (!code) {
      window.MicronoteMessageBox?.show({
        title: "Vedi articolo",
        message: "La riga selezionata non contiene un codice articolo."
      });
      return;
    }

    if (!articleCardModal || !articleCardModalFrame) {
      window.location.href = `/Articoli/Edit/${encodeURIComponent(code)}?azione=1&returnUrl=${encodeURIComponent(currentReturnUrl())}`;
      return;
    }

    articleCardModalFrame.src = `/Articoli/Edit/${encodeURIComponent(code)}?azione=101&returnUrl=${encodeURIComponent(currentReturnUrl())}`;
    articleCardModal.hidden = false;
    document.body.classList.add("purchase-invoice-payment-modal-open");
    articleCardModalFrame.focus();
  };

  const closeArticleCardModal = () => {
    if (!articleCardModal) {
      return;
    }

    articleCardModal.hidden = true;
    document.body.classList.remove("purchase-invoice-payment-modal-open");
    articleCardModalFrame?.removeAttribute("src");
    document.querySelector("[data-stock-action='article']")?.focus();
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
    document.querySelector("[data-stock-action='movement']")?.focus();
  };

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
      const action = button.dataset.stockAction;
      if (action === "exit") {
        window.location.href = "/";
        return;
      }

      if (action === "print") {
        window.MicronoteMessageBox?.show({
          title: "Stampa",
          message: "Stampa lista movimenti di magazzino.",
          detail: "Funzione in preparazione."
        });
        return;
      }

      const row = requireSelection();
      if (!row) {
        return;
      }

      if (action === "article") {
        openArticle(row);
        return;
      }

      if (action === "movement") {
        openMovement(row);
      }
    });
  });

  articleCardModal?.addEventListener("click", (event) => {
    if (event.target === articleCardModal) {
      closeArticleCardModal();
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
      closeArticleCardModal();
    }

    if (event.data?.type === "micronote:stock-load-cancel" || event.data?.type === "micronote:sales-cancel") {
      closeDocumentModal();
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
