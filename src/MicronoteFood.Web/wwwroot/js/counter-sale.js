(() => {
  const root = document.querySelector("[data-counter-sale]");
  if (!root) return;
  const data = JSON.parse(root.querySelector("[data-counter-sale-data]").textContent);
  const articles = data.articles || [];
  const customers = data.customers || [];
  const number = new Intl.NumberFormat("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 3 });
  const totalNumber = new Intl.NumberFormat("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const validDimensions = new Set(["category", "group", "species", "origin"]);
  const initialDimension = validDimensions.has(data.initialGrouping) ? data.initialGrouping : "category";
  const state = { dimension: initialDimension, group: null, remainder: new Set(), selectedArticle: null, selectedRow: -1, customer: null, rows: [], draftId: 0 };
  let articleSort = { key: "description", direction: "asc" };
  const $ = selector => root.querySelector(selector);
  const parse = value => Number(String(value || "")
    .replace(/\s/g, "")
    .replace(/%/g, "")
    .replace(/\./g, "")
    .replace(",", ".")) || 0;
  const parseControlledDecimal = value =>
    window.MicronoteDecimal?.parse?.(value) ?? parse(value);
  const isNumeric = value => {
    const text = String(value ?? "").replace(/\s/g, "").replace(/%/g, "");
    if (!text) return true;
    return Number.isFinite(Number(text.replace(/\./g, "").replace(",", ".")));
  };
  const round = (value, digits = 2) => Math.round((value + Number.EPSILON) * 10 ** digits) / 10 ** digits;
  const formatDecimal = (value, digits, suffix = "") =>
    `${new Intl.NumberFormat("it-IT", {
      minimumFractionDigits: digits,
      maximumFractionDigits: digits,
      useGrouping: true
    }).format(round(value, digits))}${suffix}`;
  const editableDecimal = (value, digits) =>
    round(value, digits).toFixed(digits).replace(".", ",");
  const sanitizeDecimal = (value, integerDigits, decimals, signed, maxAbs = null) => {
    let text = String(value ?? "").replace(/\s/g, "").replace(/%/g, "");
    const negative = signed && text.startsWith("-");
    text = text.replace(/[+-]/g, "");
    const separator = text.search(/[.,]/);
    let integer = (separator >= 0 ? text.slice(0, separator) : text).replace(/\D/g, "").slice(0, integerDigits);
    let fraction = separator >= 0
      ? text.slice(separator + 1).replace(/\D/g, "").slice(0, decimals)
      : "";
    integer ||= "0";
    let normalized = `${negative ? "-" : ""}${integer}${separator >= 0 && decimals ? `,${fraction}` : ""}`;
    const numeric = parse(normalized);
    if (maxAbs != null && Math.abs(numeric) > maxAbs)
      normalized = editableDecimal(negative ? -maxAbs : maxAbs, decimals);
    return normalized;
  };
  const sanitizeInteger = (value, maxAbs) => {
    const text = String(value ?? "").replace(/\s/g, "");
    const negative = text.startsWith("-");
    const digits = text.replace(/\D/g, "").slice(0, 5);
    if (!digits) return negative ? "-" : "";
    const numeric = Math.min(Number(digits), maxAbs);
    return `${negative ? "-" : ""}${numeric}`;
  };
  const key = suffix => `${state.dimension}${suffix}`;
  const antiforgeryToken = $("[data-sale-form] input[name='__RequestVerificationToken']")?.value || "";
  const tick = () => { $("[data-clock]").textContent = new Date().toLocaleTimeString("it-IT"); };
  tick(); setInterval(tick, 1000);

  function groupDefinitions() {
    const map = new Map();
    articles.forEach(article => {
      const code = Number(article[key("Code")]) || 0;
      if (!code) return;
      const item = map.get(code) || { code, label: article[state.dimension] || `Codice ${code}`, count: 0 };
      item.count++; map.set(code, item);
    });
    return [...map.values()].sort((a, b) => b.count - a.count || a.code - b.code);
  }

  function renderGroups() {
    const groups = groupDefinitions();
    const top = groups.slice(0, 12);
    state.remainder = new Set(groups.slice(12).map(item => item.code));
    if (state.group === null || (!top.some(item => item.code === state.group) && state.group !== "other"))
      state.group = top[0]?.code ?? "other";
    const host = $("[data-groups]");
    host.replaceChildren();
    top.forEach(item => {
      const button = document.createElement("button");
      button.type = "button";
      button.setAttribute("role", "option");
      button.className = state.group === item.code ? "is-active" : "";
      button.textContent = item.label;
      button.addEventListener("click", () => { state.group = item.code; renderGroups(); renderArticles(); });
      host.append(button);
    });
    if (state.remainder.size || groups.length === 0) {
      const button = document.createElement("button");
      button.type = "button";
      button.className = state.group === "other" ? "is-active" : "";
      button.textContent = "•••";
      button.addEventListener("click", () => { state.group = "other"; renderGroups(); renderArticles(); });
      host.append(button);
    }
  }

  function filteredArticles() {
    const query = $("[data-article-search]").value.trim().toLocaleUpperCase("it-IT");
    return articles
      .filter(article => {
        const code = Number(article[key("Code")]) || 0;
        const inGroup = state.group === "other" ? state.remainder.has(code) : code === state.group;
        return inGroup && (!query || `${article.code} ${article.description}`.toLocaleUpperCase("it-IT").includes(query));
      })
      .sort((left, right) => {
        let comparison;
        if (articleSort.key === "price") {
          comparison = Number(left.price) - Number(right.price);
        } else {
          comparison = String(left[articleSort.key] ?? "").localeCompare(
            String(right[articleSort.key] ?? ""),
            "it",
            { sensitivity: "base", numeric: true }
          );
        }
        if (comparison === 0)
          comparison = String(left.code).localeCompare(String(right.code), "it", { numeric: true });
        return articleSort.direction === "asc" ? comparison : -comparison;
      });
  }

  function renderArticles() {
    const rows = filteredArticles();
    if (state.selectedArticle && !rows.some(article => article.code === state.selectedArticle.code))
      state.selectedArticle = null;
    $("[data-article-count]").textContent = `${rows.length} articoli`;
    const host = $("[data-article-list]");
    host.replaceChildren();
    rows.forEach(article => {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "counter-sale-article-row";
      button.article = article;
      button.setAttribute("aria-selected", state.selectedArticle?.code === article.code ? "true" : "false");
      if (state.selectedArticle?.code === article.code) button.classList.add("is-selected");
      button.innerHTML = `<span>${article.code}</span><strong>${article.description}</strong><b>${number.format(article.price)}</b>`;
      button.addEventListener("click", () => {
        host.querySelectorAll(".counter-sale-article-row").forEach(candidate => {
          candidate.classList.toggle("is-selected", candidate === button);
          candidate.setAttribute("aria-selected", candidate === button ? "true" : "false");
        });
        state.selectedArticle = article;
        updateButtons();
      });
      button.addEventListener("dblclick", () => {
        state.selectedArticle = article;
        updateButtons();
        openRow(article);
      });
      button.addEventListener("keydown", event => {
        const visible = [...host.querySelectorAll(".counter-sale-article-row")];
        const current = visible.indexOf(button);
        let next = current;
        if (event.key === "ArrowDown") next = Math.min(current + 1, visible.length - 1);
        else if (event.key === "ArrowUp") next = Math.max(current - 1, 0);
        else if (event.key === "Home") next = 0;
        else if (event.key === "End") next = visible.length - 1;
        else if (event.key === "Enter") {
          event.preventDefault();
          openRow(article);
          return;
        } else return;
        event.preventDefault();
        const nextButton = visible[next];
        visible.forEach(candidate => {
          candidate.classList.toggle("is-selected", candidate === nextButton);
          candidate.setAttribute("aria-selected", candidate === nextButton ? "true" : "false");
        });
        state.selectedArticle = nextButton.article;
        nextButton.focus({ preventScroll: true });
        nextButton.scrollIntoView({ block: "nearest" });
        updateButtons();
      });
      host.append(button);
    });
    updateButtons();
  }

  function rowAmounts(row) {
    const quantity = row.quantity !== 0 ? row.quantity : row.packages;
    const net = round(quantity * row.price);
    const amount = row.amount == null
      ? round(net * (1 + row.vatRate / 100))
      : round(row.amount);
    return { net, amount, vat: round(amount - net) };
  }

  function totals() {
    return state.rows.reduce((sum, row) => {
      const values = rowAmounts(row);
      sum.net += values.net; sum.vat += values.vat; sum.total += values.amount;
      return sum;
    }, { net: 0, vat: 0, total: 0 });
  }

  function renderCart() {
    const host = $("[data-cart-list]"); host.replaceChildren();
    if (!state.rows.length) host.innerHTML = '<div class="counter-sale-empty">Tocca un articolo per iniziare la vendita</div>';
    state.rows.forEach((row, index) => {
      const button = document.createElement("button"); button.type = "button";
      button.className = "counter-sale-cart-row" + (state.selectedRow === index ? " is-selected" : "");
      const amount = rowAmounts(row).amount;
      button.innerHTML = `<span>${row.articleCode}</span><strong>${row.description}</strong><span>${number.format(row.quantity || row.packages)}</span><span>${number.format(row.vatRate)}%</span><span>${number.format(row.price)}</span><b>${totalNumber.format(amount)}</b>`;
      button.addEventListener("click", () => { state.selectedRow = index; renderCart(); updateButtons(); });
      button.addEventListener("dblclick", () => openRow(row.article, index));
      host.append(button);
    });
    const sum = totals();
    $("[data-total-net]").textContent = totalNumber.format(sum.net);
    $("[data-total-vat]").textContent = totalNumber.format(sum.vat);
    $("[data-total]").textContent = totalNumber.format(sum.total);
    updateButtons();
  }

  function updateButtons() {
    const selectedSaleRow = state.selectedRow >= 0 ? state.rows[state.selectedRow] : null;
    $("[data-add]").disabled = !state.selectedArticle;
    $("[data-card]").disabled = !state.selectedArticle;
    $("[data-edit]").disabled = !selectedSaleRow?.article;
    $("[data-remove]").disabled = state.selectedRow < 0;
    $("[data-balance]").disabled = !state.customer;
    $("[data-close]").disabled = !state.customer || state.rows.length < 1;
    const hasSaleData = Boolean(state.customer) || state.rows.length > 0;
    $("[data-cancel]").disabled = !hasSaleData;
  }

  function openRow(article, editIndex = -1) {
    const existing = editIndex >= 0 ? state.rows[editIndex] : null;
    const modal = $("[data-row-modal]"); modal.hidden = false; modal.dataset.editIndex = editIndex;
    $("[data-row-title]").textContent = existing ? "Modifica articolo" : "Inserimento articolo";
    $("[data-row-code]").value = article.code;
    $("[data-row-description]").value = article.description;
    $("[data-row-unit]").value = article.unit || "";
    $("[data-row-stock]").value = number.format(article.stock);
    $("[data-row-last-price]").value = "";
    $("[data-row-last-vat]").value = "";
    $("[data-row-tare]").value = number.format(article.tare);
    $("[data-row-quantity]").value = existing ? formatDecimal(existing.quantity, 3) : "";
    $("[data-row-packages]").value = existing ? existing.packages : "";
    $("[data-row-price]").value = formatDecimal(existing?.price ?? article.price, 2);
    $("[data-row-vat]").value = formatDecimal(existing?.vatRate ?? article.vatRate, 2, " %");
    $("[data-row-amount]").readOnly = !data.enableAmountEditing;
    modal.article = article; updateRowPreview();
    if (existing?.amount != null)
      $("[data-row-amount]").value = formatDecimal(existing.amount, 2);
    if (state.customer) {
      const requestedArticle = article.code;
      fetch(`${location.pathname}?handler=LastPrice&customerCode=${encodeURIComponent(state.customer.code)}&articleCode=${encodeURIComponent(article.code)}`)
        .then(response => response.ok ? response.json() : null)
        .then(result => {
          if (modal.article?.code !== requestedArticle) return;
          $("[data-row-last-price]").value = result?.price == null ? "" : number.format(result.price);
          $("[data-row-last-vat]").value = result?.vatRate == null ? "" : number.format(result.vatRate);
        })
        .catch(() => {
          if (modal.article?.code !== requestedArticle) return;
          $("[data-row-last-price]").value = "";
          $("[data-row-last-vat]").value = "";
        });
    }
    setTimeout(() => $("[data-row-quantity]").focus(), 0);
  }

  function updateRowPreview() {
    const price = parse($("[data-row-price]").value), vat = parse($("[data-row-vat]").value);
    const quantity = parse($("[data-row-quantity]").value), packages = Math.trunc(parse($("[data-row-packages]").value));
    const effective = quantity || packages;
    $("[data-row-vat-price]").value = formatDecimal(price * (1 + vat / 100), 2);
    $("[data-row-amount]").value = formatDecimal(effective * price * (1 + vat / 100), 2);
  }

  function updateRowFromAmount() {
    if (!data.enableAmountEditing) return;
    const amount = parse($("[data-row-amount]").value);
    const vat = parse($("[data-row-vat]").value);
    const quantity = parse($("[data-row-quantity]").value);
    const packages = Math.trunc(parse($("[data-row-packages]").value));
    const effective = quantity || packages;
    if (!effective) return;
    const net = amount / (1 + vat / 100);
    const price = round(net / effective, 2);
    $("[data-row-price]").value = formatDecimal(price, 2);
    $("[data-row-vat-price]").value = formatDecimal(price * (1 + vat / 100), 2);
    $("[data-row-amount]").value = formatDecimal(amount, 2);
  }

  function closeRow() { $("[data-row-modal]").hidden = true; }
  root.querySelectorAll("[data-row-cancel]").forEach(button => button.addEventListener("click", closeRow));
  $("[data-sales-line-article-lookup]")?.addEventListener("click", () =>
    $("[data-counter-row-article-lookup-open]")?.click());
  root.addEventListener("micronote:lookup-selected", event => {
    if (!event.target.matches(".counter-sale-row-article-lookup")) return;
    const code = String(event.detail.row.code || "");
    const source = event.detail.row;
    const catalogueArticle = data.articles.find(item => String(item.code) === code);
    const article = catalogueArticle
      ? {
          ...catalogueArticle,
          unit: catalogueArticle.unit || source.unitMeasure || "",
          tare: catalogueArticle.tare ?? source.tare ?? 0
        }
      : {
          code,
          description: source.description || source.label || "",
          unit: source.unitMeasure || "",
          tare: Number(source.tare) || 0,
          stock: Number(source.stock) || 0,
          price: Number(source.price) || 0,
          vatRate: Number(source.vatRate) || 0
        };
    if (!article) return;
    const modal = $("[data-row-modal]");
    openRow(article, Number(modal.dataset.editIndex ?? -1));
  });
  const controlledInputs = [
    { selector: "[data-row-packages]", sanitize: value => sanitizeInteger(value, 32000), digits: 0, suffix: "" },
    { selector: "[data-row-quantity]", sanitize: value => sanitizeDecimal(value, 6, 3, true, 999999.999), digits: 3, suffix: "" },
    { selector: "[data-row-price]", sanitize: value => sanitizeDecimal(value, 6, 2, false, 999999.99), digits: 2, suffix: "" },
    { selector: "[data-row-vat]", sanitize: value => sanitizeDecimal(value, 3, 2, false, 100), digits: 2, suffix: " %" },
    { selector: "[data-row-amount]", sanitize: value => sanitizeDecimal(value, 10, 2, true), digits: 2, suffix: "" }
  ];
  controlledInputs.forEach(rule => {
    const input = $(rule.selector);
    input.addEventListener("focus", () => {
      if (!input.value) return;
      input.value = rule.digits === 0
        ? String(Math.trunc(parse(input.value)))
        : editableDecimal(parse(input.value), rule.digits);
      input.select();
    });
    input.addEventListener("input", () => {
      input.value = rule.sanitize(input.value);
      if (rule.selector !== "[data-row-amount]") updateRowPreview();
    });
    input.addEventListener("blur", () => {
      if (!input.value || input.value === "-") {
        input.value = "";
        return;
      }
      const value = parse(input.value);
      input.value = rule.digits === 0
        ? String(Math.trunc(value))
        : formatDecimal(value, rule.digits, rule.suffix);
      if (rule.selector === "[data-row-amount]") updateRowFromAmount();
    });
  });
  $("[data-row-amount]").addEventListener("change", updateRowFromAmount);
  $("[data-row-modal]").addEventListener("keydown", event => {
    if (event.key === "Escape") {
      event.preventDefault();
      event.stopPropagation();
      closeRow();
      return;
    }
    if (event.key !== "Enter" || event.target.tagName !== "INPUT") return;
    event.preventDefault();
    const inputs = [...$("[data-row-modal]").querySelectorAll("input:not([readonly])")];
    const index = inputs.indexOf(event.target);
    if (index >= 0 && index < inputs.length - 1) inputs[index + 1].focus();
    else $("[data-row-confirm]").focus();
  });
  $("[data-row-confirm]").addEventListener("keydown", event => {
    if (event.key !== "ArrowUp") return;
    event.preventDefault();
    (data.enableAmountEditing ? $("[data-row-amount]") : $("[data-row-vat]")).focus();
  });
  $("[data-row-confirm]").addEventListener("click", () => {
    const modal = $("[data-row-modal]"), article = modal.article;
    const numericInputs = [
      $("[data-row-quantity]"), $("[data-row-packages]"),
      $("[data-row-price]"), $("[data-row-vat]")
    ];
    if (data.enableAmountEditing) numericInputs.push($("[data-row-amount]"));
    const invalidInput = numericInputs.find(input => !isNumeric(input.value));
    if (invalidInput) {
      invalidInput.focus();
      return;
    }
    const row = {
      article, articleCode: article.code, description: article.description, unit: article.unit || "",
      packages: Math.trunc(parse($("[data-row-packages]").value)),
      tare: round(article.tare, 3), quantity: round(parse($("[data-row-quantity]").value), 3),
      price: round(parse($("[data-row-price]").value), 2), vatRate: round(parse($("[data-row-vat]").value), 2),
      amount: round(parse($("[data-row-amount]").value), 2)
    };
    if (Math.abs(row.packages) > 32000) { $("[data-row-packages]").focus(); return; }
    if (Math.abs(row.quantity) > 999999.999) { $("[data-row-quantity]").focus(); return; }
    if (row.price < 0 || row.price > 999999.99) { $("[data-row-price]").focus(); return; }
    if (row.vatRate < 0 || row.vatRate > 100) { $("[data-row-vat]").focus(); return; }
    if (row.quantity === 0 && row.packages === 0) { $("[data-row-quantity]").focus(); return; }
    const rowTotal = rowAmounts(row).amount;
    if (Math.abs(rowTotal) > 9999999999.99) {
      window.MicronoteMessageBox?.show({
        title: "Importo riga non valido",
        message: "L'importo totale della riga supera la dimensione consentita.",
        detail: "Il valore massimo registrabile è 9.999.999.999,99.",
        variant: "error",
        onConfirm: () => window.setTimeout(() => $("[data-row-quantity]").focus(), 0)
      });
      return;
    }
    const editIndex = Number(modal.dataset.editIndex);
    if (row.price === 0 && !confirm("Il prezzo è zero. Confermare comunque?")) {
      $("[data-row-price]").focus();
      return;
    }
    if (rowTotal < 0 && !confirm("L'importo è negativo. Confermare comunque?")) {
      $("[data-row-price]").focus();
      return;
    }
    if (editIndex >= 0) state.rows[editIndex] = row; else { state.rows.push(row); state.selectedRow = state.rows.length - 1; }
    closeRow(); renderCart();
  });

  const customerLookup = $("[data-lookup-type='clienti']");
  const showCustomer = customer => {
    state.customer = customer;
    $("[data-customer-code]").textContent = customer
      ? String(customer.code).padStart(5, "0")
      : "";
    $("[data-customer-name]").textContent = customer?.name || "";
    updateButtons();
  };
  $("[data-customer-open]").addEventListener("click", () => {
    $("[data-counter-customer-lookup-open]")?.click();
  });
  customerLookup?.addEventListener("micronote:lookup-selected", event => {
    const row = event.detail?.row;
    const code = Number(row?.code) || 0;
    const customer = customers.find(item => Number(item.code) === code)
      || (code > 0 ? { code, name: row?.label || "", city: row?.detail || "", storeCode: 0 } : null);
    showCustomer(customer);
  });
  $("[data-customer-clear]").addEventListener("click", () => { state.customer = null; $("[data-customer-code]").textContent = ""; $("[data-customer-name]").textContent = ""; updateButtons(); });

  root.querySelectorAll("[data-dimension]").forEach(button => button.addEventListener("click", () => {
    root.querySelectorAll("[data-dimension]").forEach(item => item.classList.toggle("is-active", item === button));
    state.dimension = button.dataset.dimension; state.group = null; renderGroups(); renderArticles();
  }));
  $("[data-article-search]").addEventListener("input", renderArticles);
  root.querySelectorAll("[data-article-sort]").forEach(header => {
    header.addEventListener("pointerdown", event => event.preventDefault());
    header.addEventListener("click", () => {
      const key = header.dataset.articleSort;
      articleSort = {
        key,
        direction: articleSort.key === key && articleSort.direction === "asc" ? "desc" : "asc"
      };
      root.querySelectorAll("[data-article-sort]").forEach(candidate => {
        const active = candidate === header;
        candidate.classList.toggle("is-sorted", active);
        candidate.classList.toggle("is-ascending", active && articleSort.direction === "asc");
        candidate.setAttribute("aria-sort", active
          ? (articleSort.direction === "asc" ? "ascending" : "descending")
          : "none");
      });
      renderArticles();
      $("[data-article-scroll]").scrollTop = 0;
      $("[data-article-search]").focus({ preventScroll: true });
    });
  });
  $("[data-add]").addEventListener("click", () => state.selectedArticle && openRow(state.selectedArticle));
  $("[data-edit]").addEventListener("click", () => {
    if (state.selectedRow < 0) return;
    const row = state.rows[state.selectedRow];
    if (row?.article) openRow(row.article, state.selectedRow);
  });
  $("[data-remove]").addEventListener("click", () => {
    if (state.selectedRow < 0) return;
    const rowIndex = state.selectedRow;
    const row = state.rows[rowIndex];
    window.MicronoteMessageBox?.show({
      title: "Rimuovi articolo",
      message: `Rimuovere dalla vendita l'articolo ${row?.articleCode || ""}?`,
      detail: row?.description || "",
      mode: "confirm",
      variant: "confirm",
      okText: "Rimuovi",
      cancelText: "Annulla",
      onConfirm: () => {
        state.rows.splice(rowIndex, 1);
        state.selectedRow = -1;
        renderCart();
      }
    });
  });
  const articleCardModal = $("[data-article-card-modal]");
  const articleCardFrame = $("[data-article-card-frame]");
  const articleCardButton = $("[data-card]");
  const closeArticleCard = () => {
    if (articleCardModal.hidden) return;
    articleCardModal.hidden = true;
    articleCardFrame.removeAttribute("src");
    articleCardButton.focus({ preventScroll: true });
  };
  articleCardButton.addEventListener("click", () => {
    if (!state.selectedArticle) return;
    articleCardFrame.src = `/Articoli/Edit/${encodeURIComponent(state.selectedArticle.code)}?azione=101`;
    articleCardModal.hidden = false;
    articleCardFrame.focus();
  });
  window.addEventListener("message", event => {
    if (event.origin !== window.location.origin) return;
    if (event.data?.type === "micronote:article-cancel" ||
        event.data?.type === "micronote:article-saved")
      closeArticleCard();
  });
  $("[data-balance]").addEventListener("click", () => state.customer && window.open(`/EstrattoContoClientiFornitori/Index?type=C&code=${state.customer.code}`, "_blank"));
  function resetSale() {
    state.rows = [];
    state.customer = null;
    state.selectedRow = -1;
    state.selectedArticle = null;
    state.dimension = initialDimension;
    state.group = null;
    state.remainder = new Set();
    $("[data-customer-code]").textContent = "";
    $("[data-customer-name]").textContent = "";
    $("[data-article-search]").value = "";
    $("[data-article-scroll]").scrollTop = 0;
    root.querySelectorAll("[data-dimension]").forEach(button =>
      button.classList.toggle("is-active", button.dataset.dimension === initialDimension));
    root.querySelectorAll(".counter-sale-overlay").forEach(overlay => {
      overlay.hidden = true;
    });
    postDraft("DeleteDraft", draftPayload());
    state.draftId = 0;
    renderGroups();
    renderArticles();
    renderCart();
    $("[data-customer-open]").focus({ preventScroll: true });
  }
  $("[data-cancel]").addEventListener("click", () => {
    window.MicronoteMessageBox?.show({
      title: "Annulla vendita",
      message: "Annullare la vendita corrente?",
      detail: "Il cliente e tutte le righe inserite saranno eliminati.",
      mode: "confirm",
      variant: "confirm",
      okText: "Conferma",
      cancelText: "Continua vendita",
      onConfirm: resetSale
    });
  });
  let escapeConsumedUntilKeyup = false;
  document.addEventListener("keyup", event => {
    if (event.key === "Escape") escapeConsumedUntilKeyup = false;
  }, true);
  document.addEventListener("keydown", event => {
    if (event.key !== "Escape") return;
    if (document.querySelector("[data-micronote-messagebox].active")) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    if (escapeConsumedUntilKeyup || event.repeat) return;
    if (!articleCardModal.hidden) {
      escapeConsumedUntilKeyup = true;
      closeArticleCard();
      return;
    }
    const closeModal = $("[data-close-modal]");
    if (!closeModal.hidden) {
      escapeConsumedUntilKeyup = true;
      closeModal.hidden = true;
      return;
    }
    const rowModal = $("[data-row-modal]");
    if (!rowModal.hidden) {
      escapeConsumedUntilKeyup = true;
      closeRow();
      return;
    }
    $("[data-exit]").click();
  }, true);

  $("[data-close]").addEventListener("click", () => {
    const sum = totals();
    if (Math.abs(sum.total) > 9999999999.99) {
      window.MicronoteMessageBox?.show({
        title: "Totale vendita non valido",
        message: "La somma delle righe supera la dimensione consentita per il totale vendita.",
        detail: "Il valore massimo registrabile in Vendite.Totale è 9.999.999.999,99.",
        variant: "error",
        onConfirm: () => window.setTimeout(() => $("[data-close]").focus(), 0)
      });
      return;
    }
    $("[data-close-goods]").textContent = totalNumber.format(sum.net);
    $("[data-close-vat]").textContent = totalNumber.format(sum.vat);
    $("[data-close-gross]").textContent = totalNumber.format(sum.total);
    $("[data-close-discount]").value = "0,00";
    $("[data-close-paid]").value = "0,00";
    $("[data-close-paid]").disabled = false;
    updateCloseTotals();
    $("[data-close-modal]").hidden = false;
    setTimeout(() => $("[data-close-discount]").focus(), 0);
  });
  function updateCloseTotals() {
    const gross = totals().total;
    const discount = Math.min(gross, Math.max(0, parseControlledDecimal($("[data-close-discount]").value)));
    const net = round(gross - discount);
    const paid = Math.max(0, parseControlledDecimal($("[data-close-paid]").value));
    $("[data-close-total]").textContent = totalNumber.format(net);
    $("[data-close-balance]").textContent = totalNumber.format(round(net - paid));
  }
  ["[data-close-discount]", "[data-close-paid]"].forEach(selector => {
    const input = $(selector);
    input.addEventListener("focus", () => {
      input.value = editableDecimal(parseControlledDecimal(input.value), 2);
      input.select();
    });
    input.addEventListener("input", () => {
      input.value = sanitizeDecimal(input.value, 10, 2, false, 9999999999.99);
      updateCloseTotals();
    });
    input.addEventListener("blur", () => {
      input.value = formatDecimal(Math.max(0, parseControlledDecimal(input.value)), 2);
      updateCloseTotals();
    });
  });
  root.querySelectorAll("[data-close-cancel]").forEach(button => button.addEventListener("click", () => $("[data-close-modal]").hidden = true));
  $("[data-close-modal]").addEventListener("keydown", event => {
    if (event.key !== "Tab") return;
    const controls = [...$("[data-close-modal]").querySelectorAll(
      "input:not(:disabled), button:not(:disabled), [href], [tabindex]:not([tabindex='-1'])"
    )].filter(control => control.offsetParent !== null);
    if (!controls.length) return;
    const first = controls[0];
    const last = controls[controls.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    } else if (!controls.includes(document.activeElement)) {
      event.preventDefault();
      (event.shiftKey ? last : first).focus();
    }
  });
  function submitSale(printAfterSave = false) {
    const gross = totals().total;
    const discount = round(parseControlledDecimal($("[data-close-discount]").value), 2);
    const paidAmount = round(parseControlledDecimal($("[data-close-paid]").value), 2);
    if (discount < 0 || discount > gross) {
      $("[data-close-discount]").focus();
      return;
    }
    const payload = {
      draftId: state.draftId,
      customerCode: state.customer?.code || 0, discount, paidAmount, printAfterSave,
      rows: state.rows.map(row => ({ articleCode: row.articleCode, unit: row.unit, packages: row.packages, tare: row.tare, quantity: row.quantity, price: row.price, vatRate: row.vatRate, amount: rowAmounts(row).amount }))
    };
    $("[data-sale-payload]").value = JSON.stringify(payload);
    $("[data-close-confirm]").disabled = true;
    $("[data-close-confirm-print]").disabled = true;
    $("[data-sale-form]").submit();
  }
  $("[data-close-confirm]").addEventListener("click", () => submitSale(false));
  $("[data-close-confirm-print]").addEventListener("click", () => submitSale(true));
  function draftPayload() {
    return {
      draftId: state.draftId,
      customerCode: state.customer?.code || 0,
      rows: state.rows.map(row => ({
        articleCode: row.articleCode,
        unit: row.unit,
        packages: row.packages,
        tare: row.tare,
        quantity: row.quantity,
        price: row.price,
        vatRate: row.vatRate,
        amount: rowAmounts(row).amount
      }))
    };
  }
  function postDraft(handler, payload = null) {
    const body = new URLSearchParams();
    body.set("__RequestVerificationToken", antiforgeryToken);
    if (payload) body.set("SalePayload", JSON.stringify(payload));
    return fetch(`${location.pathname}?handler=${handler}`, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8" },
      body: body.toString()
    }).catch(() => null);
  }
  let exitInProgress = false;
  $("[data-exit]").addEventListener("click", async event => {
    event.preventDefault();
    if (exitInProgress) return;
    exitInProgress = true;
    const destination = event.currentTarget.href;
    const response = await postDraft("SaveDraft", draftPayload());
    if (!response?.ok) {
      exitInProgress = false;
      window.MicronoteMessageBox?.show({
        title: "Vendita sospesa",
        message: "Non è stato possibile salvare la vendita sospesa.",
        detail: "La vendita resta aperta. Riprovare prima di uscire.",
        variant: "error",
        onConfirm: () => window.setTimeout(() => $("[data-exit]").focus(), 0)
      });
      return;
    }
    window.location.href = destination;
  });
  function restoreDraft() {
    const draft = data.draft;
    if (!draft) return;
    state.draftId = Number(draft.draftId) || 0;
    state.customer = customers.find(customer =>
      Number(customer.code) === Number(draft.customerCode)) || null;
    state.rows = (draft.rows || [])
      .map(row => {
        const article = articles.find(candidate =>
          String(candidate.code) === String(row.articleCode));
        if (!article) return null;
        return {
          ...row,
          article,
          articleCode: article.code,
          description: article.description,
          unit: row.unit || article.unit || "",
          vatRate: Number(row.vatRate ?? article.vatRate ?? 0)
        };
      })
      .filter(Boolean);
    $("[data-customer-code]").textContent = state.customer
      ? String(state.customer.code).padStart(5, "0")
      : "";
    $("[data-customer-name]").textContent = state.customer?.name || "";
  }

  const scroll = $("[data-article-scroll]");
  let activePointerId = null, pointerY = 0, startY = 0, lastMoveAt = 0, velocity = 0, moved = false, suppressClick = false, inertiaFrame = 0;
  const stopInertia = () => { if (inertiaFrame) cancelAnimationFrame(inertiaFrame); inertiaFrame = 0; };
  const continueInertia = () => {
    if (Math.abs(velocity) < 0.15) { stopInertia(); return; }
    scroll.scrollTop += velocity * 16;
    velocity *= 0.94;
    inertiaFrame = requestAnimationFrame(continueInertia);
  };
  scroll.addEventListener("pointerdown", event => {
    stopInertia();
    activePointerId = event.pointerId;
    pointerY = startY = event.clientY;
    lastMoveAt = performance.now();
    velocity = 0;
    moved = false;
    suppressClick = false;
  });
  scroll.addEventListener("pointermove", event => {
    if (event.pointerId !== activePointerId) return;
    const now = performance.now();
    const delta = pointerY - event.clientY;
    const elapsed = Math.max(1, now - lastMoveAt);
    if (!moved && Math.abs(event.clientY - startY) > 8) {
      moved = true;
      scroll.setPointerCapture(event.pointerId);
    }
    if (moved) {
      event.preventDefault();
      scroll.scrollTop += delta;
      velocity = delta / elapsed;
    }
    pointerY = event.clientY;
    lastMoveAt = now;
  });
  const finishScroll = event => {
    if (event.pointerId !== activePointerId) return;
    if (scroll.hasPointerCapture(event.pointerId)) scroll.releasePointerCapture(event.pointerId);
    activePointerId = null;
    if (!moved) return;
    suppressClick = true;
    inertiaFrame = requestAnimationFrame(continueInertia);
  };
  scroll.addEventListener("pointerup", finishScroll);
  scroll.addEventListener("pointercancel", finishScroll);
  scroll.addEventListener("click", event => {
    if (!suppressClick) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    suppressClick = false;
  }, true);

  root.querySelectorAll("[data-dimension]").forEach(button =>
    button.classList.toggle("is-active", button.dataset.dimension === state.dimension));
  restoreDraft(); renderGroups(); renderArticles(); renderCart();
})();
