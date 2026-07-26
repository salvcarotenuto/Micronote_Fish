window.MicronoteMoney = (() => {
  const defaultIntegerDigits = 9;

  const parse = (value) => {
    const text = String(value ?? "").trim();
    const commaIndex = text.lastIndexOf(",");
    const dotIndex = text.lastIndexOf(".");
    const decimalIndex = Math.max(commaIndex, dotIndex);
    const normalized = decimalIndex >= 0
      ? `${text.slice(0, decimalIndex).replace(/[.,]/g, "")}.${text.slice(decimalIndex + 1).replace(/[.,]/g, "")}`
      : text.replace(/[.,]/g, "");
    const parsed = Number.parseFloat(normalized);
    return Number.isFinite(parsed) ? parsed : 0;
  };

  const format = (value) => {
    if (value === 0) {
      return "";
    }

    const rounded = Math.round(value * 100) / 100;
    const sign = rounded < 0 ? "-" : "";
    const [integerPart, decimalPart] = Math.abs(rounded).toFixed(2).split(".");
    return `${sign}${integerPart.replace(/\B(?=(\d{3})+(?!\d))/g, ".")},${decimalPart}`;
  };

  const formatForEdit = (value) => {
    if (value === 0) {
      return "";
    }

    return String(Math.round(value * 100) / 100);
  };

  const clean = (input) => {
    if (!input) {
      return;
    }

    const maxIntegerDigits = Number(input.dataset.moneyIntegerDigits) || defaultIntegerDigits;
    const text = String(input.value ?? "").replace(/[^\d.,]/g, "");
    const separatorMatches = Array.from(text.matchAll(/[.,]/g));
    const decimalIndex = separatorMatches.length > 1
      ? separatorMatches[separatorMatches.length - 1].index
      : (separatorMatches[0]?.index ?? -1);

    let cleaned = "";
    if (decimalIndex >= 0) {
      const integerPart = text.slice(0, decimalIndex).replace(/[.,]/g, "").slice(0, maxIntegerDigits);
      const decimalPart = text.slice(decimalIndex + 1).replace(/[.,]/g, "").slice(0, 2);
      cleaned = `${integerPart}.${decimalPart}`;
    } else {
      cleaned = text.replace(/[.,]/g, "").slice(0, maxIntegerDigits);
    }

    if (input.value !== cleaned) {
      input.value = cleaned;
    }
  };

  const normalizeForSubmit = (input) => {
    if (input) {
      input.value = String(parse(input.value));
    }
  };

  const selectionContainsSeparator = (input) => {
    const value = String(input.value ?? "");
    const start = input.selectionStart ?? 0;
    const end = input.selectionEnd ?? start;

    if (start === end) {
      return false;
    }

    return /[.,]/.test(value.slice(start, end));
  };

  const wire = (input, options = {}) => {
    if (!input || input.dataset.moneyWired === "true") {
      return;
    }

    input.dataset.moneyWired = "true";
    input.addEventListener("beforeinput", (event) => {
      if (event.inputType !== "insertText" || !/[.,]/.test(event.data ?? "")) {
        return;
      }

      if (/[.,]/.test(input.value) && !selectionContainsSeparator(input)) {
        event.preventDefault();
      }
    });
    input.addEventListener("focus", () => {
      input.value = formatForEdit(parse(input.value));
      input.select?.();
      options.onFocus?.(input);
    });
    input.addEventListener("input", () => {
      clean(input);
      options.onInput?.(input);
    });
    input.addEventListener("blur", () => {
      input.value = format(parse(input.value));
      options.onBlur?.(input);
    });
  };

  document.addEventListener("DOMContentLoaded", () => {
    const fields = Array.from(document.querySelectorAll("[data-money-field]"));
    fields.forEach((field) => wire(field));

    document.querySelectorAll("form").forEach((form) => {
      form.addEventListener("submit", () => {
        fields
          .filter((field) => form.contains(field))
          .forEach(normalizeForSubmit);
      }, true);
    });
  });

  return {
    clean,
    format,
    formatForEdit,
    normalizeForSubmit,
    parse,
    wire
  };
})();
