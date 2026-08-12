document.addEventListener("DOMContentLoaded", () => {
  const frame = document.querySelector(".daily-trading-grid-frame");
  const table = frame?.querySelector(".daily-trading-grid");
  const rows = Array.from(table?.tBodies[0]?.rows ?? []);
  if (!frame || !table || rows.length === 0) return;

  let selectedIndex = 0;
  const ensureVisible = (row, direction = 0) => {
    const headerHeight = table.tHead?.offsetHeight ?? 0;
    const visibleTop = frame.scrollTop + headerHeight;
    const visibleBottom = frame.scrollTop + frame.clientHeight;
    const rowTop = row.offsetTop;
    const rowBottom = rowTop + row.offsetHeight;
    if (rowTop >= visibleTop && rowBottom <= visibleBottom) return;
    if (direction >= 0 && rowBottom > visibleBottom) frame.scrollTop = rowBottom - frame.clientHeight + 1;
    else if (direction <= 0 && rowTop < visibleTop) frame.scrollTop = Math.max(rowTop - headerHeight - 1, 0);
  };
  const select = (index, focus = false, direction = 0) => {
    selectedIndex = Math.max(0, Math.min(index, rows.length - 1));
    rows.forEach((row, rowIndex) => {
      const selected = rowIndex === selectedIndex;
      row.classList.toggle("selected-row", selected);
      row.setAttribute("aria-selected", selected ? "true" : "false");
    });
    const row = rows[selectedIndex];
    if (focus) row.focus({ preventScroll: true });
    ensureVisible(row, direction);
  };

  rows.forEach((row, index) => {
    row.addEventListener("click", () => select(index, true));
    row.addEventListener("keydown", event => {
      let target = selectedIndex;
      let direction = 0;
      if (event.key === "ArrowDown") { target += 1; direction = 1; }
      else if (event.key === "ArrowUp") { target -= 1; direction = -1; }
      else if (event.key === "Home") target = 0;
      else if (event.key === "End") target = rows.length - 1;
      else return;
      event.preventDefault();
      select(target, true, direction);
    });
  });

  const visibleRows = () => {
    const frameRect = frame.getBoundingClientRect();
    const top = frameRect.top + (table.tHead?.getBoundingClientRect().height ?? 0);
    return rows.filter(row => {
      const rect = row.getBoundingClientRect();
      return rect.top >= top + 1 && rect.bottom <= frameRect.bottom - 1;
    });
  };
  let previousScrollTop = frame.scrollTop;
  let scrollFrame = 0;
  frame.addEventListener("scroll", () => {
    if (scrollFrame) cancelAnimationFrame(scrollFrame);
    scrollFrame = requestAnimationFrame(() => {
      scrollFrame = 0;
      const delta = frame.scrollTop - previousScrollTop;
      previousScrollTop = frame.scrollTop;
      const selectedRect = rows[selectedIndex].getBoundingClientRect();
      const frameRect = frame.getBoundingClientRect();
      const top = frameRect.top + (table.tHead?.getBoundingClientRect().height ?? 0);
      if (selectedRect.bottom > top && selectedRect.top < frameRect.bottom) return;
      const visible = visibleRows();
      if (visible.length > 0) select(rows.indexOf(delta > 0 ? visible[0] : visible[visible.length - 1]));
    });
  }, { passive: true });

  select(0, true);
});
