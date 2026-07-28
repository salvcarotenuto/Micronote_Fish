(() => {
  const root = document.querySelector("[data-counter-sale]");
  if (!root) return;
  const data = JSON.parse(root.querySelector("[data-counter-sale-data]").textContent);
  const articles = data.articles || [];
  const customers = data.customers || [];
  const euro = new Intl.NumberFormat("it-IT", { style: "currency", currency: "EUR" });
  const number = new Intl.NumberFormat("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 3 });
  const totalNumber = new Intl.NumberFormat("it-IT", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const validDimensions = new Set(["category", "group", "species", "origin"]);
  const initialDimension = validDimensions.has(data.initialGrouping) ? data.initialGrouping : "category";
  const state = { dimension: initialDimension, group: null, remainder: new Set(), selectedArticle: null, selectedRow: -1, customer: null, rows: [] };
  const $ = selector => root.querySelector(selector);
  const parse = value => Number(String(value || "").replace(/\./g, "").replace(",", ".")) || 0;
  const isNumeric = value => {
    const text = String(value ?? "").trim();
    if (!text) return true;
    return Number.isFinite(Number(text.replace(/\./g, "").replace(",", ".")));
  };
  const round = (value, digits = 2) => Math.round((value + Number.EPSILON) * 10 ** digits) / 10 ** digits;
  const key = suffix => `${state.dimension}${suffix}`;

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
    return articles.filter(article => {
      const code = Number(article[key("Code")]) || 0;
      const inGroup = state.group === "other" ? state.remainder.has(code) : code === state.group;
      return inGroup && (!query || `${article.code} ${article.description}`.toLocaleUpperCase("it-IT").includes(query));
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
      button.innerHTML = `<span>${row.articleCode}</span><strong>${row.description}</strong><span>${number.format(row.quantity || row.packages)}</span><span>${number.format(row.vatRate)}%</span><span>${number.format(row.price)}</span><b>${euro.format(amount)}</b>`;
      button.addEventListener("click", () => { state.selectedRow = index; renderCart(); updateButtons(); });
      button.addEventListener("dblclick", () => openRow(row.article, index));
      host.append(button);
    });
    const sum = totals();
    $("[data-total-net]").textContent = totalNumber.format(sum.net);
    $("[data-total-vat]").textContent = totalNumber.format(sum.vat);
    $("[data-total]").textContent = totalNumber.format(sum.total);
    updateButtons(); saveDraft();
  }

  function updateButtons() {
    const selectedSaleRow = state.selectedRow >= 0 ? state.rows[state.selectedRow] : null;
    $("[data-add]").disabled = !state.selectedArticle;
    $("[data-card]").disabled = !state.selectedArticle;
    $("[data-edit]").disabled = !selectedSaleRow?.article;
    $("[data-remove]").disabled = state.selectedRow < 0;
    $("[data-balance]").disabled = !state.customer;
    $("[data-close]").disabled = !state.customer || !state.rows.length;
    $("[data-cancel]").disabled = !state.customer && !state.rows.length;
  }

  function openRow(article, editIndex = -1) {
    const existing = editIndex >= 0 ? state.rows[editIndex] : null;
    const modal = $("[data-row-modal]"); modal.hidden = false; modal.dataset.editIndex = editIndex;
    $("[data-row-title]").textContent = existing ? "Modifica articolo" : "Inserimento articolo";
    $("[data-row-code]").textContent = article.code;
    $("[data-row-description]").textContent = article.description;
    $("[data-row-unit]").value = article.unit || "";
    $("[data-row-stock]").value = number.format(article.stock);
    $("[data-row-last-price]").value = "";
    $("[data-row-last-vat]").value = "";
    $("[data-row-tare]").value = number.format(article.tare);
    $("[data-row-quantity]").value = existing ? number.format(existing.quantity) : "";
    $("[data-row-packages]").value = existing ? existing.packages : "";
    $("[data-row-price]").value = number.format(existing?.price ?? article.price);
    $("[data-row-vat]").value = number.format(existing?.vatRate ?? article.vatRate);
    $("[data-row-amount]").readOnly = !data.enableAmountEditing;
    modal.article = article; updateRowPreview();
    if (existing?.amount != null)
      $("[data-row-amount]").value = totalNumber.format(existing.amount);
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
    $("[data-row-vat-price]").value = number.format(round(price * (1 + vat / 100), 3));
    $("[data-row-amount]").value = totalNumber.format(round(effective * price * (1 + vat / 100)));
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
    const price = round(net / effective, 3);
    $("[data-row-price]").value = number.format(price);
    $("[data-row-vat-price]").value = number.format(round(price * (1 + vat / 100), 3));
    $("[data-row-amount]").value = totalNumber.format(amount);
  }

  function closeRow() { $("[data-row-modal]").hidden = true; }
  root.querySelectorAll("[data-row-cancel]").forEach(button => button.addEventListener("click", closeRow));
  root.querySelectorAll("[data-row-quantity],[data-row-packages],[data-row-price],[data-row-vat]").forEach(input => input.addEventListener("input", updateRowPreview));
  $("[data-row-amount]").addEventListener("change", updateRowFromAmount);
  $("[data-row-modal]").addEventListener("keydown", event => {
    if (event.key === "Escape") {
      event.preventDefault();
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
      packages: Math.max(0, Math.trunc(parse($("[data-row-packages]").value))),
      tare: round(article.tare, 3), quantity: round(parse($("[data-row-quantity]").value), 3),
      price: round(parse($("[data-row-price]").value), 3), vatRate: round(parse($("[data-row-vat]").value), 2),
      amount: round(parse($("[data-row-amount]").value), 2)
    };
    if (row.quantity === 0 && row.packages === 0) { $("[data-row-quantity]").focus(); return; }
    if (row.price === 0 && !confirm("Il prezzo è zero. Confermare comunque?")) {
      $("[data-row-price]").focus();
      return;
    }
    if (rowAmounts(row).amount < 0 && !confirm("L'importo è negativo. Confermare comunque?")) {
      $("[data-row-price]").focus();
      return;
    }
    const index = Number(modal.dataset.editIndex);
    if (index >= 0) state.rows[index] = row; else { state.rows.push(row); state.selectedRow = state.rows.length - 1; }
    closeRow(); renderCart();
  });

  function renderCustomers(query = "") {
    const text = query.trim().toLocaleUpperCase("it-IT"), host = $("[data-customer-list]"); host.replaceChildren();
    customers.filter(c => !text || `${c.code} ${c.name}`.toLocaleUpperCase("it-IT").includes(text)).forEach(customer => {
      const button = document.createElement("button"); button.type = "button";
      button.innerHTML = `<strong>${String(customer.code).padStart(5, "0")}</strong><span>${customer.name}</span>`;
      button.addEventListener("click", () => {
        state.customer = customer; $("[data-customer-code]").textContent = String(customer.code).padStart(5, "0");
        $("[data-customer-name]").textContent = customer.name; $("[data-customer-modal]").hidden = true;
        updateButtons(); saveDraft();
      }); host.append(button);
    });
  }
  $("[data-customer-open]").addEventListener("click", () => { renderCustomers(); $("[data-customer-modal]").hidden = false; setTimeout(() => $("[data-customer-search]").focus(), 0); });
  $("[data-customer-close]").addEventListener("click", () => $("[data-customer-modal]").hidden = true);
  $("[data-customer-search]").addEventListener("input", event => renderCustomers(event.target.value));
  $("[data-customer-clear]").addEventListener("click", () => { state.customer = null; $("[data-customer-code]").textContent = ""; $("[data-customer-name]").textContent = ""; updateButtons(); saveDraft(); });

  root.querySelectorAll("[data-dimension]").forEach(button => button.addEventListener("click", () => {
    root.querySelectorAll("[data-dimension]").forEach(item => item.classList.toggle("is-active", item === button));
    state.dimension = button.dataset.dimension; state.group = null; renderGroups(); renderArticles();
  }));
  $("[data-article-search]").addEventListener("input", renderArticles);
  $("[data-add]").addEventListener("click", () => state.selectedArticle && openRow(state.selectedArticle));
  $("[data-edit]").addEventListener("click", () => {
    if (state.selectedRow < 0) return;
    const row = state.rows[state.selectedRow];
    if (row?.article) openRow(row.article, state.selectedRow);
  });
  $("[data-remove]").addEventListener("click", () => { if (state.selectedRow < 0) return; state.rows.splice(state.selectedRow, 1); state.selectedRow = -1; renderCart(); });
  $("[data-card]").addEventListener("click", () => state.selectedArticle && window.open(`/Articoli/Edit?code=${encodeURIComponent(state.selectedArticle.code)}&azione=visualizza`, "_blank"));
  $("[data-balance]").addEventListener("click", () => state.customer && window.open(`/EstrattoContoClientiFornitori/Index?type=C&code=${state.customer.code}`, "_blank"));
  $("[data-cancel]").addEventListener("click", () => { if (!confirm("Annullare la vendita corrente?")) return; state.rows=[]; state.customer=null; state.selectedRow=-1; localStorage.removeItem("microfish.counterSaleDraft"); location.reload(); });

  $("[data-close]").addEventListener("click", () => {
    const sum = totals(); $("[data-close-gross]").textContent = euro.format(sum.total);
    $("[data-close-discount]").value = "0,00"; $("[data-close-total]").textContent = euro.format(sum.total);
    $("[data-close-modal]").hidden = false;
  });
  $("[data-close-discount]").addEventListener("input", () => $("[data-close-total]").textContent = euro.format(Math.max(0, totals().total - parse($("[data-close-discount]").value))));
  root.querySelectorAll("[data-close-cancel]").forEach(button => button.addEventListener("click", () => $("[data-close-modal]").hidden = true));
  $("[data-close-confirm]").addEventListener("click", () => {
    const payload = {
      customerCode: state.customer.code, discount: parse($("[data-close-discount]").value),
      rows: state.rows.map(row => ({ articleCode: row.articleCode, unit: row.unit, packages: row.packages, tare: row.tare, quantity: row.quantity, price: row.price, vatRate: row.vatRate, amount: rowAmounts(row).amount }))
    };
    $("[data-sale-payload]").value = JSON.stringify(payload);
    localStorage.removeItem("microfish.counterSaleDraft");
    $("[data-close-confirm]").disabled = true; $("[data-sale-form]").submit();
  });

  function saveDraft() {
    localStorage.setItem("microfish.counterSaleDraft", JSON.stringify({ customerCode: state.customer?.code || 0, rows: state.rows.map(row => ({ ...row, article: undefined })) }));
  }
  function restoreDraft() {
    try {
      const draft = JSON.parse(localStorage.getItem("microfish.counterSaleDraft") || "null");
      if (!draft) return;
      state.customer = null;
      state.rows = (draft.rows || []).map(row => ({ ...row, article: articles.find(a => a.code === row.articleCode) })).filter(row => row.article);
      $("[data-customer-code]").textContent = "";
      $("[data-customer-name]").textContent = "";
    } catch { localStorage.removeItem("microfish.counterSaleDraft"); }
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
