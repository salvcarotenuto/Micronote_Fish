window.MicronoteDecimal = (() => {
  const defaultIntegerDigits = 7;
  const defaultDecimalDigits = 3;

  const digits = (input, name, fallback) => {
    const value = Number(input?.dataset?.[name]);
    return Number.isFinite(value) && value >= 0 ? value : fallback;
  };

  const parse = (value) => {
    const text = String(value ?? "").replace(/[^\d.,-]/g, "").trim();
    const commaIndex = text.lastIndexOf(",");
    const dotIndex = text.lastIndexOf(".");
    const decimalIndex = Math.max(commaIndex, dotIndex);
    const normalized = decimalIndex >= 0
      ? `${text.slice(0, decimalIndex).replace(/[.,]/g, "")}.${text.slice(decimalIndex + 1).replace(/[.,]/g, "")}`
      : text.replace(/[.,]/g, "");
    const parsed = Number.parseFloat(normalized);
    return Number.isFinite(parsed) ? parsed : 0;
  };

  const format = (value, decimalDigits = defaultDecimalDigits) => {
    const number = Number(value || 0);
    if (!Number.isFinite(number) || number === 0) {
      return "";
    }

    const fixed = Math.abs(number).toFixed(decimalDigits);
    const [integerPart, decimalPart = ""] = fixed.split(".");
    const sign = number < 0 ? "-" : "";
    const groupedInteger = integerPart.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
    return decimalDigits > 0
      ? `${sign}${groupedInteger},${decimalPart}`
      : `${sign}${groupedInteger}`;
  };

  const formatForEdit = (value, decimalDigits = defaultDecimalDigits) => {
    const number = Number(value || 0);
    if (!Number.isFinite(number) || number === 0) {
      return "";
    }

    const factor = 10 ** decimalDigits;
    return String(Math.round(number * factor) / factor);
  };

  const clean = (input) => {
    if (!input) {
      return;
    }

    const maxIntegerDigits = digits(input, "decimalIntegerDigits", defaultIntegerDigits);
    const maxDecimalDigits = digits(input, "decimalDigits", defaultDecimalDigits);
    const text = String(input.value ?? "").replace(/[^\d.,]/g, "");
    const separatorMatches = Array.from(text.matchAll(/[.,]/g));
    const decimalIndex = separatorMatches.length > 1
      ? separatorMatches[separatorMatches.length - 1].index
      : (separatorMatches[0]?.index ?? -1);

    const cleaned = decimalIndex >= 0
      ? `${text.slice(0, decimalIndex).replace(/[.,]/g, "").slice(0, maxIntegerDigits)}.${text.slice(decimalIndex + 1).replace(/[.,]/g, "").slice(0, maxDecimalDigits)}`
      : text.replace(/[.,]/g, "").slice(0, maxIntegerDigits);

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
    return start !== end && /[.,]/.test(value.slice(start, end));
  };

  const wire = (input, options = {}) => {
    if (!input || input.dataset.decimalWired === "true") {
      return;
    }

    input.dataset.decimalWired = "true";
    input.addEventListener("beforeinput", (event) => {
      if (event.inputType !== "insertText" || !/[.,]/.test(event.data ?? "")) {
        return;
      }

      if (/[.,]/.test(input.value) && !selectionContainsSeparator(input)) {
        event.preventDefault();
      }
    });
    input.addEventListener("focus", () => {
      input.value = formatForEdit(parse(input.value), digits(input, "decimalDigits", defaultDecimalDigits));
      input.select?.();
      options.onFocus?.(input);
    });
    input.addEventListener("input", () => {
      clean(input);
      options.onInput?.(input);
    });
    input.addEventListener("blur", () => {
      input.value = format(parse(input.value), digits(input, "decimalDigits", defaultDecimalDigits));
      options.onBlur?.(input);
    });
  };

  document.addEventListener("DOMContentLoaded", () => {
    const fields = Array.from(document.querySelectorAll("[data-decimal-field]"));
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
