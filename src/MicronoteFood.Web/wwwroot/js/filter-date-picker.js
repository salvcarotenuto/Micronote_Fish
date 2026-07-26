document.addEventListener("DOMContentLoaded", () => {
  const fields = Array.from(document.querySelectorAll("[data-filter-date-display]"));
  if (!fields.length) return;

  let panel;
  let activeField;
  let activeMonth;
  const pad = (value) => String(value).padStart(2, "0");
  const valid = (day, month, year) => {
    const date = new Date(year, month - 1, day);
    return date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day;
  };
  const iso = (value) => {
    const match = /^(\d{1,2})[/-](\d{1,2})[/-](\d{4})$/.exec(String(value ?? "").trim());
    if (!match) return "";
    const day = Number(match[1]); const month = Number(match[2]); const year = Number(match[3]);
    return valid(day, month, year) ? `${year}-${pad(month)}-${pad(day)}` : "";
  };
  const dateValue = (field) => {
    const value = iso(field.value);
    if (!value) return null;
    const [year, month, day] = value.split("-").map(Number);
    return new Date(year, month - 1, day);
  };
  const hidden = (field) => field.closest("[data-date-control]")?.querySelector("[data-filter-date-hidden]");
  const selectedDatePart = (field) => {
    const caret = field.selectionStart ?? 0;
    if (caret <= 2) return "day";
    if (caret <= 5) return "month";
    return "year";
  };
  const selectDatePart = (field, part) => {
    const ranges = { day: [0, 2], month: [3, 5], year: [6, 10] };
    const [start, end] = ranges[part];
    window.requestAnimationFrame(() => field.setSelectionRange(start, end));
  };
  const shiftDatePart = (field, direction) => {
    const current = dateValue(field);
    if (!current) return false;
    const part = selectedDatePart(field);
    const year = current.getFullYear();
    const month = current.getMonth();
    const day = current.getDate();
    let shifted;

    if (part === "day") {
      const lastDay = new Date(year, month + 1, 0).getDate();
      shifted = new Date(year, month, Math.max(1, Math.min(day + direction, lastDay)));
    } else if (part === "month") {
      const targetMonth = new Date(year, month + direction, 1);
      const lastDay = new Date(targetMonth.getFullYear(), targetMonth.getMonth() + 1, 0).getDate();
      shifted = new Date(targetMonth.getFullYear(), targetMonth.getMonth(), Math.min(day, lastDay));
    } else {
      const targetYear = year + direction;
      const lastDay = new Date(targetYear, month + 1, 0).getDate();
      shifted = new Date(targetYear, month, Math.min(day, lastDay));
    }

    field.value = `${pad(shifted.getDate())}/${pad(shifted.getMonth() + 1)}/${shifted.getFullYear()}`;
    sync(field);
    selectDatePart(field, part);
    return true;
  };
  const sync = (field) => {
    const value = iso(field.value); const target = hidden(field);
    if (!value || !target) return false;
    const [year, month, day] = value.split("-");
    field.value = `${day}/${month}/${year}`;
    if (target.value !== value) {
      target.value = value;
      target.dispatchEvent(new Event("change", { bubbles: true }));
    }
    return true;
  };
  const ensurePanel = () => {
    if (panel) return panel;
    panel = document.createElement("div");
    panel.className = "micronote-date-picker";
    panel.hidden = true;
    panel.setAttribute("role", "dialog");
    panel.setAttribute("aria-label", "Calendario");
    document.body.appendChild(panel);
    return panel;
  };
  const close = () => { if (panel) panel.hidden = true; activeField = null; };
  const position = () => {
    if (!panel || !activeField) return;
    const rect = activeField.getBoundingClientRect();
    panel.style.left = `${Math.max(8, rect.right - panel.offsetWidth + window.scrollX)}px`;
    panel.style.top = `${rect.bottom + 4 + window.scrollY}px`;
  };
  const render = () => {
    const box = ensurePanel();
    const selected = dateValue(activeField);
    const year = activeMonth.getFullYear(); const month = activeMonth.getMonth();
    const offset = (new Date(year, month, 1).getDay() + 6) % 7;
    const count = new Date(year, month + 1, 0).getDate();
    const cells = Array.from({ length: offset }, () => '<span class="micronote-date-picker-empty"></span>');
    for (let day = 1; day <= count; day += 1) {
      const chosen = selected && selected.getFullYear() === year && selected.getMonth() === month && selected.getDate() === day;
      cells.push(`<button type="button" class="${chosen ? "is-selected" : ""}" data-date-picker-day="${day}">${day}</button>`);
    }
    box.innerHTML = `<div class="micronote-date-picker-head"><button type="button" data-date-picker-prev>&lt;</button><strong>${activeMonth.toLocaleDateString("it-IT", { month: "long", year: "numeric" })}</strong><button type="button" data-date-picker-next>&gt;</button></div><div class="micronote-date-picker-weekdays">${["Lu","Ma","Me","Gi","Ve","Sa","Do"].map(day => `<span>${day}</span>`).join("")}</div><div class="micronote-date-picker-days">${cells.join("")}</div>`;
  };
  const open = (field) => {
    activeField = field;
    const base = dateValue(field) ?? new Date();
    activeMonth = new Date(base.getFullYear(), base.getMonth(), 1);
    render(); panel.hidden = false; position(); field.focus();
  };

  fields.forEach((field) => {
    field.addEventListener("input", () => {
      const digits = field.value.replace(/\D/g, "").slice(0, 8);
      field.value = digits.length > 4 ? `${digits.slice(0, 2)}/${digits.slice(2, 4)}/${digits.slice(4)}` : digits.length > 2 ? `${digits.slice(0, 2)}/${digits.slice(2)}` : digits;
    });
    field.addEventListener("change", () => sync(field));
    field.addEventListener("blur", () => sync(field));
    field.addEventListener("focus", () => {
      if (dateValue(field)) selectDatePart(field, "day");
    });
    field.addEventListener("click", () => {
      if (dateValue(field)) selectDatePart(field, selectedDatePart(field));
    });
    field.addEventListener("keydown", (event) => {
      if (event.key === "ArrowUp" || event.key === "ArrowDown") {
        event.preventDefault();
        shiftDatePart(field, event.key === "ArrowUp" ? 1 : -1);
        return;
      }
      if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
        const part = selectedDatePart(field);
        const nextPart = event.key === "ArrowLeft"
          ? part === "year" ? "month" : "day"
          : part === "day" ? "month" : "year";
        event.preventDefault();
        selectDatePart(field, nextPart);
        return;
      }
      if (event.key === "Enter") {
        event.preventDefault();
        sync(field);
      }
    });
  });
  document.addEventListener("mousedown", (event) => { if (event.target.closest?.(".micronote-date-picker")) event.preventDefault(); });
  document.addEventListener("click", (event) => {
    const target = event.target;
    const trigger = target.closest?.("[data-date-picker-button]");
    if (trigger) {
      event.preventDefault();
      const field = trigger.closest("[data-date-control]")?.querySelector("[data-filter-date-display]");
      if (field) activeField === field && panel && !panel.hidden ? close() : open(field);
      return;
    }
    if (target.closest?.(".micronote-date-picker")) {
      event.preventDefault();
      if (target.closest("[data-date-picker-prev]")) activeMonth = new Date(activeMonth.getFullYear(), activeMonth.getMonth() - 1, 1);
      else if (target.closest("[data-date-picker-next]")) activeMonth = new Date(activeMonth.getFullYear(), activeMonth.getMonth() + 1, 1);
      else {
        const dayButton = target.closest("[data-date-picker-day]");
        if (dayButton) {
          activeField.value = `${pad(dayButton.dataset.datePickerDay)}/${pad(activeMonth.getMonth() + 1)}/${activeMonth.getFullYear()}`;
          sync(activeField); close(); return;
        }
      }
      render(); position(); return;
    }
    if (!target.closest?.("[data-date-control]")) close();
  });
  document.addEventListener("keydown", (event) => { if (event.key === "Escape" && panel && !panel.hidden) { event.preventDefault(); event.stopImmediatePropagation(); close(); } }, true);
});
