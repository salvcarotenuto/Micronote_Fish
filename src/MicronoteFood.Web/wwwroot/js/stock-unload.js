document.addEventListener("DOMContentLoaded",()=>{const p=document.querySelector("[data-stock-unload]");if(!p)return;const f=p.querySelector("[data-unload-filters]"),b=p.querySelector("tbody"),rs=()=>[...b.querySelectorAll("[data-unload-row]")],sel=()=>b.querySelector(".selected");const select=r=>{rs().forEach(x=>x.classList.remove("selected","selected-row"));r.classList.add("selected","selected-row");r.focus({preventScroll:true})};const ret=()=>encodeURIComponent(location.pathname+location.search);const edit=r=>location.href=`/ScaricoPerditeResi/Edit?id=${r.dataset.id}&returnTo=list&returnUrl=${ret()}`;f.querySelectorAll("select").forEach(x=>x.onchange=()=>f.requestSubmit());rs().forEach((r,i)=>{r.onclick=()=>select(r);r.ondblclick=()=>edit(r);r.onkeydown=e=>{if(e.key==="ArrowDown"){e.preventDefault();select(rs()[Math.min(i+1,rs().length-1)])}if(e.key==="ArrowUp"){e.preventDefault();select(rs()[Math.max(i-1,0)])}if(e.key==="Enter"){e.preventDefault();edit(r)}}});const preview=p.querySelector("[data-unload-preview]"),doc=p.querySelector("[data-preview-document]");p.querySelectorAll("[data-unload-action]").forEach(bt=>bt.onclick=()=>{const a=bt.dataset.unloadAction;if(a==="new"){location.href=`/ScaricoPerditeResi/Edit?year=${f.querySelector("[name=year]").value}&returnTo=list&returnUrl=${ret()}`;return}if(a==="print"){const sh=document.createElement("article");sh.className="supplier-report-page stock-unload-report-page";sh.innerHTML=`<header><div><strong>SCARICO PER PERDITE E RESI</strong><span>Elenco secondo i filtri e l'ordinamento applicati</span></div><small>data di stampa: ${new Intl.DateTimeFormat("it-IT").format(new Date())}</small></header>`;sh.appendChild(p.querySelector(".stock-unload-grid").cloneNode(true));doc.replaceChildren(sh);preview.hidden=false;return}const r=sel();if(!r){window.MicronoteMessageBox?.show({title:"Scarico per perdite e resi",message:"Selezionare un movimento dalla lista."});return}if(a==="edit"){edit(r);return}if(a==="delete"&&confirm(`Cancellare la partita ${r.dataset.code.padStart(6,"0")}?`)){const d=p.querySelector("[data-unload-delete-form]");d.id.value=r.dataset.id;d.year.value=r.dataset.year;d.submit()}});p.querySelector("[data-preview-close]").onclick=()=>preview.hidden=true;p.querySelector("[data-preview-print]").onclick=()=>{document.body.classList.add("is-printing-supplier-report");print()};addEventListener("afterprint",()=>document.body.classList.remove("is-printing-supplier-report"))});

document.addEventListener("DOMContentLoaded", () => {
  const page = document.querySelector("[data-stock-unload]");
  const deleteButton = page?.querySelector("[data-unload-action='delete']");
  if (!page || !deleteButton) return;

  deleteButton.onclick = () => {
    const row = page.querySelector("tbody .selected");
    if (!row) {
      window.MicronoteMessageBox?.show({
        title: "Scarico per perdite e resi",
        message: "Selezionare un movimento dalla lista."
      });
      return;
    }

    window.MicronoteMessageBox?.show({
      title: "Scarico per perdite e resi",
      message: `Cancellare la partita ${row.dataset.code.padStart(6, "0")}?`,
      mode: "confirm",
      variant: "confirm",
      okText: "Cancella",
      onConfirm: () => {
        const form = page.querySelector("[data-unload-delete-form]");
        form.id.value = row.dataset.id;
        form.year.value = row.dataset.year;
        form.submit();
      }
    });
  };
});
