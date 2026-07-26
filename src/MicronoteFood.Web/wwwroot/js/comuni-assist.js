document.addEventListener("DOMContentLoaded", () => {
  const cityInput = document.querySelector("[data-comune-city]");
  const provinceInput = document.querySelector("[data-comune-province]");
  const postalCodeInput = document.querySelector("[data-comune-postal-code]");
  const comuniList = document.getElementById("ComuniList");

  if (!cityInput || !comuniList) {
    return;
  }

  let comuniCache = [];
  let requestTimer = null;
  let requestController = null;

  const normalize = (value) => (value || "").trim().toLocaleUpperCase("it-IT");

  const escapeAttr = (value) =>
    String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll('"', "&quot;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;");

  const clearSuggestions = () => {
    comuniCache = [];
    comuniList.innerHTML = "";
  };

  const renderSuggestions = (items) => {
    comuniList.innerHTML = items
      .map((item) => {
        const label = `${item.name} (${item.province}) ${item.postalCode}`;
        return `<option value="${escapeAttr(item.name)}" label="${escapeAttr(label)}"></option>`;
      })
      .join("");
  };

  const fillLocation = (item) => {
    if (!item) {
      return;
    }

    cityInput.value = item.name || "";
    if (provinceInput) {
      provinceInput.value = item.province || "";
    }
    if (postalCodeInput) {
      postalCodeInput.value = item.postalCode || "";
    }
  };

  const findSelectedComune = () => {
    const city = normalize(cityInput.value);
    if (!city) {
      return null;
    }

    return comuniCache.find((item) => normalize(item.name) === city) ?? null;
  };

  const applySelectedComune = () => {
    fillLocation(findSelectedComune());
  };

  const loadComuni = async () => {
    const query = cityInput.value.trim();
    if (query.length < 2) {
      clearSuggestions();
      return;
    }

    if (requestController) {
      requestController.abort();
    }

    requestController = new AbortController();

    try {
      const response = await fetch(
        `/api/comuni?q=${encodeURIComponent(query)}`,
        {
          headers: {
            Accept: "application/json"
          },
          signal: requestController.signal
        }
      );

      if (!response.ok) {
        clearSuggestions();
        return;
      }

      comuniCache = await response.json();
      renderSuggestions(comuniCache);
      applySelectedComune();
    } catch (error) {
      if (error.name !== "AbortError") {
        clearSuggestions();
      }
    }
  };

  cityInput.addEventListener("input", () => {
    window.clearTimeout(requestTimer);
    requestTimer = window.setTimeout(loadComuni, 180);
  });

  cityInput.addEventListener("change", applySelectedComune);
  cityInput.addEventListener("blur", applySelectedComune);
});
