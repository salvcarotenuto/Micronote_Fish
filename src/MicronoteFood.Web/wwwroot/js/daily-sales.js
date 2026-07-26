document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-daily-sales]");
  if (!page) return;
  const form = page.querySelector("[data-daily-sales-form]");
  const display = page.querySelector("[data-daily-sales-date-display]");
  const hidden = page.querySelector("[data-daily-sales-date-hidden]");
  const button = page.querySelector("[data-date-picker-button]");
  if (!(display instanceof HTMLInputElement) || !(hidden instanceof HTMLInputElement)) return;

  const parse = () => {
    const match = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(display.value.trim());
    if (!match) return "";
    const day = Number(match[1]), month = Number(match[2]), year = Number(match[3]);
    const date = new Date(year, month - 1, day);
    return date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day
      ? `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}` : "";
  };
  const sync = () => { const iso = parse(); if (iso) hidden.value = iso; return Boolean(iso); };
  const formatDate = date => `${String(date.getDate()).padStart(2,"0")}/${String(date.getMonth()+1).padStart(2,"0")}/${date.getFullYear()}`;
  const selectPart = (part) => { const ranges = [[0,2],[3,5],[6,10]]; const range = ranges[part]; setTimeout(() => display.setSelectionRange(range[0], range[1]), 0); };
  const partAtCaret = () => (display.selectionStart ?? 0) >= 6 ? 2 : (display.selectionStart ?? 0) >= 3 ? 1 : 0;
  const submitShiftedDate = (unit, amount) => {
    const iso = parse();
    if (!iso) return;
    const [year, month, day] = iso.split("-").map(Number);
    let date;
    if (unit === "day") {
      date = new Date(year, month - 1, day + amount);
    } else {
      const targetMonth = month - 1 + amount;
      const firstOfTarget = new Date(year, targetMonth, 1);
      const lastDay = new Date(firstOfTarget.getFullYear(), firstOfTarget.getMonth() + 1, 0).getDate();
      date = new Date(firstOfTarget.getFullYear(), firstOfTarget.getMonth(), Math.min(day, lastDay));
    }
    hidden.value = `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,"0")}-${String(date.getDate()).padStart(2,"0")}`;
    display.value = formatDate(date);
    form?.submit();
  };
  page.querySelectorAll("[data-date-shift-day]").forEach(control =>
    control.addEventListener("click", () => submitShiftedDate("day", Number(control.dataset.dateShiftDay))));
  page.querySelectorAll("[data-date-shift-month]").forEach(control =>
    control.addEventListener("click", () => submitShiftedDate("month", Number(control.dataset.dateShiftMonth))));
  display.addEventListener("focus", () => selectPart(0));
  display.addEventListener("click", () => selectPart(partAtCaret()));
  display.addEventListener("blur", sync);
  display.addEventListener("keydown", event => {
    let part = partAtCaret();
    if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
      event.preventDefault(); selectPart(Math.max(0, Math.min(2, part + (event.key === "ArrowRight" ? 1 : -1)))); return;
    }
    if (event.key === "ArrowUp" || event.key === "ArrowDown") {
      event.preventDefault();
      const iso = parse(); if (!iso) return;
      const [year, month, day] = iso.split("-").map(Number); const date = new Date(year, month - 1, day);
      const delta = event.key === "ArrowUp" ? 1 : -1;
      if (part === 0) date.setDate(date.getDate() + delta); else if (part === 1) date.setMonth(date.getMonth() + delta); else date.setFullYear(date.getFullYear() + delta);
      display.value = `${String(date.getDate()).padStart(2,"0")}/${String(date.getMonth()+1).padStart(2,"0")}/${date.getFullYear()}`;
      sync(); selectPart(part); return;
    }
    if (event.key === "Enter") { event.preventDefault(); if (sync()) form?.submit(); }
  });
  let pickerPanel = null;
  let pickerMonth = null;
  const closePicker = () => { if (pickerPanel) pickerPanel.hidden = true; };
  const positionPicker = () => {
    if (!pickerPanel) return;
    const rect = display.getBoundingClientRect();
    pickerPanel.style.left = `${Math.max(8, rect.right - pickerPanel.offsetWidth + window.scrollX)}px`;
    pickerPanel.style.top = `${rect.bottom + 4 + window.scrollY}px`;
  };
  const renderPicker = () => {
    if (!pickerPanel || !pickerMonth) return;
    const year = pickerMonth.getFullYear(), month = pickerMonth.getMonth();
    const selected = hidden.value ? new Date(`${hidden.value}T00:00:00`) : null;
    const offset = (new Date(year, month, 1).getDay() + 6) % 7;
    const days = new Date(year, month + 1, 0).getDate();
    const cells = [];
    for (let index = 0; index < offset; index += 1) cells.push('<span class="micronote-date-picker-empty"></span>');
    for (let day = 1; day <= days; day += 1) {
      const selectedDay = selected && selected.getFullYear() === year && selected.getMonth() === month && selected.getDate() === day;
      cells.push(`<button type="button" class="${selectedDay ? "is-selected" : ""}" data-date-picker-day="${day}">${day}</button>`);
    }
    const label = pickerMonth.toLocaleDateString("it-IT", { month: "long", year: "numeric" });
    pickerPanel.innerHTML = `<div class="micronote-date-picker-head"><button type="button" data-date-picker-prev>&lt;</button><strong>${label}</strong><button type="button" data-date-picker-next>&gt;</button></div><div class="micronote-date-picker-weekdays">${["Lu","Ma","Me","Gi","Ve","Sa","Do"].map(day => `<span>${day}</span>`).join("")}</div><div class="micronote-date-picker-days">${cells.join("")}</div>`;
  };
  const openPicker = () => {
    if (!pickerPanel) {
      pickerPanel = document.createElement("div");
      pickerPanel.className = "micronote-date-picker";
      pickerPanel.setAttribute("role", "dialog");
      pickerPanel.setAttribute("aria-label", "Calendario");
      document.body.appendChild(pickerPanel);
    }
    const iso = parse(); const base = iso ? new Date(`${iso}T00:00:00`) : new Date();
    pickerMonth = new Date(base.getFullYear(), base.getMonth(), 1);
    renderPicker(); pickerPanel.hidden = false; positionPicker();
  };
  button?.addEventListener("click", event => {
    event.preventDefault();
    if (pickerPanel && !pickerPanel.hidden) closePicker(); else openPicker();
    display.focus();
  });
  document.addEventListener("click", event => {
    const target = event.target;
    if (!(target instanceof Element)) return;
    if (target.closest("[data-date-picker-button]")) return;
    if (target.closest(".micronote-date-picker")) {
      if (target.closest("[data-date-picker-prev]")) pickerMonth = new Date(pickerMonth.getFullYear(), pickerMonth.getMonth() - 1, 1);
      else if (target.closest("[data-date-picker-next]")) pickerMonth = new Date(pickerMonth.getFullYear(), pickerMonth.getMonth() + 1, 1);
      else {
        const dayButton = target.closest("[data-date-picker-day]");
        if (dayButton) {
          const date = new Date(pickerMonth.getFullYear(), pickerMonth.getMonth(), Number(dayButton.dataset.datePickerDay));
          display.value = formatDate(date); sync(); closePicker(); form?.submit(); return;
        }
      }
      renderPicker(); positionPicker(); return;
    }
    if (!target.closest("[data-date-control]")) closePicker();
  });
  document.addEventListener("keydown", event => { if (event.key === "Escape") closePicker(); });
  window.addEventListener("resize", positionPicker);
  window.addEventListener("scroll", positionPicker, true);
});
