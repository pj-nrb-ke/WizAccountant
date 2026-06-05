/** Sales Order form — lookups via WizConnector (SDK), line grid + totals */
(function () {
  const state = {
    customers: [],
    items: [],
    warehouses: [],
    taxRates: [],
    projects: [],
    reps: [],
    settlement: [],
    orderStatus: [],
    priorities: [],
    currencies: [],
    customerAddresses: {},
    itemStockCache: {},
    itemUnitsCache: {},
    itemPriceCache: {},
    warehouseFlags: null,
    selectedLine: null,
    addrTab: "delivery",
    discountSource: null,
    orderNumber: null,
    isSaving: false,
    documentSaved: false,
  };

  /** Sales screen: WhseMst.bAllowToSellFrom = 1. Purchase screen uses bAllowToBuyInto = 1. */
  const STOCK_DOCUMENT_TYPE = "sales";

  const REQUIRED_LOOKUPS = [
    { key: "customers", op: "customer.list", params: { top: "2000" } },
    { key: "warehouses", op: "warehouse.list", params: { top: "500" } },
    { key: "taxRates", op: "taxrate.list", params: { top: "100" } },
    { key: "currencies", op: "currency.list", params: { top: "50" } },
  ];

  /** Loaded silently — empty dropdown if table missing (per pilot scope). */
  const OPTIONAL_LOOKUPS = [
    { key: "projects", op: "project.list", params: { top: "500" } },
    { key: "reps", op: "salesrepresentative.list", params: { top: "200" } },
    { key: "settlement", op: "settlementterms.list", params: { top: "100" } },
    { key: "orderStatus", op: "orderstatus.list", params: { top: "100" } },
    { key: "priorities", op: "priority.list", params: { top: "50" } },
  ];

  function todayIso() {
    return new Date().toISOString().slice(0, 10);
  }

  function parseNum(v) {
    const n = parseFloat(String(v ?? "").replace(/,/g, ""));
    return Number.isFinite(n) ? n : 0;
  }

  function round2(n) {
    return Math.round((parseNum(n) + Number.EPSILON) * 100) / 100;
  }

  function money(n) {
    return round2(n).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  function setLoadStatus(text, kind) {
    const el = document.getElementById("so-load-status");
    if (!el) return;
    el.textContent = text;
    el.classList.remove("error", "ok", "muted");
    el.classList.add(kind || "muted");
  }

  async function fetchLookup({ key, op, params }, optional) {
    try {
      const job = await WizShell.runWait(op, params);
      if (job.status === 3 || job.status === "Failed") {
        if (!optional) throw new Error(job.error || "failed");
        state[key] = [];
        return null;
      }
      state[key] = WizShell.parseJobItems(job);
      return null;
    } catch (e) {
      if (!optional) return `${op}: ${e.message}`;
      state[key] = [];
      return null;
    }
  }

  function isJobOk(job) {
    if (!job?.resultJson) return false;
    const s = job.status;
    return s !== 3 && s !== "Failed" && s !== "failed";
  }

  async function allocateOrderNumber() {
    const el = document.getElementById("so-order-no");
    if (!el) return;
    el.value = "";
    el.placeholder = "Allocating…";
    try {
      const job = await WizShell.runWait("salesorder.nextnumber", { module: "0" });
      if (!isJobOk(job)) throw new Error(job.error || "Order number allocation failed");
      const data = JSON.parse(job.resultJson);
      if (data.error) throw new Error(data.message || data.error);
      const orderNo =
        data.orderNumber ||
        `${data.orderPrefix ?? ""}${data.nextNumber ?? ""}`;
      el.value = orderNo;
      el.placeholder = "";
      state.orderNumber = orderNo;
    } catch (e) {
      el.placeholder = "(failed)";
      console.warn("salesorder.nextnumber failed:", e.message);
      setLoadStatus(`Order number: ${e.message}`, "error");
    }
  }

  function setDocumentStatus(text) {
    const el = document.getElementById("so-status-doc");
    if (el) el.textContent = text;
  }

  function setSaveButtonsEnabled(enabled) {
    ["btn-place-order", "btn-quote", "btn-process"].forEach((id) => {
      const btn = document.getElementById(id);
      if (btn) btn.disabled = !enabled || state.isSaving;
    });
  }

  function collectFormLines() {
    const lines = [];
    document.querySelectorAll("#so-lines-body tr").forEach((tr) => {
      const itemCode = getLineItemCode(tr);
      if (!itemCode) return;
      lines.push({
        module: tr.querySelector(".so-module").value || "ST",
        itemCode,
        description: tr.querySelector(".so-desc").value.trim(),
        warehouseCode: tr.querySelector(".so-wh").value.trim(),
        quantity: parseNum(tr.querySelector(".so-qty").value),
        confirmQty: parseNum(tr.querySelector(".so-confirm").value),
        unitPrice: parseNum(tr.querySelector(".so-price").value),
        taxCode: tr.querySelector(".so-tax").value.trim(),
        discountPercent: parseNum(tr.querySelector(".so-disc").value),
      });
    });
    return lines;
  }

  function validateFormForSave(documentType) {
    const customer = document.getElementById("so-customer").value.trim();
    if (!customer) return "Select a customer before saving.";
    const orderNo = document.getElementById("so-order-no").value.trim();
    if (!orderNo) return "Order number is not allocated yet — wait or click New.";

    const lines = collectFormLines();
    if (!lines.length) return "Add at least one line with an item.";

    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      if (line.quantity <= 0) return `Line ${i + 1}: order quantity must be greater than zero.`;
      if (line.unitPrice <= 0) return `Line ${i + 1}: enter a unit price.`;
      if (!line.taxCode) return `Line ${i + 1}: select a tax type.`;
      if (line.module === "ST" && !line.warehouseCode)
        return `Line ${i + 1}: select a warehouse for stock items.`;
      if (documentType === "invoice" && line.confirmQty <= 0)
        return `Line ${i + 1}: confirm quantity must be greater than zero to process an invoice.`;
    }

    if (!validateAllLineTax()) {
      const n = linesMissingTaxCount();
      return n ? `${n} line(s) missing tax type.` : "Fix tax type on each line before saving.";
    }

    return null;
  }

  function buildSavePayload(documentType) {
    const discountSource = state.discountSource || "pct";
    return {
      documentType,
      customerCode: document.getElementById("so-customer").value.trim(),
      orderNo: document.getElementById("so-order-no").value.trim(),
      externalOrderNo: document.getElementById("so-ext-order").value.trim(),
      orderDate: document.getElementById("so-order-date").value,
      dueDate: document.getElementById("so-due-date").value,
      invoiceDate: document.getElementById("so-invoice-date").value,
      description: document.getElementById("so-description").value.trim(),
      deliverTo: document.getElementById("so-address").value.trim(),
      projectCode: document.getElementById("so-project").value.trim(),
      representativeCode: document.getElementById("so-rep").value.trim(),
      currencyCode: document.getElementById("so-currency").value.trim(),
      discountPercent: parseNum(document.getElementById("so-discount-pct").value),
      discountAmount: parseNum(document.getElementById("so-discount-amt").value),
      discountSource,
      taxInclusive: document.getElementById("so-tax-inclusive").checked,
      lines: collectFormLines(),
    };
  }

  async function saveDocument(documentType) {
    if (state.isSaving) return;
    const validationError = validateFormForSave(documentType);
    if (validationError) {
      setLoadStatus(validationError, "error");
      return;
    }

    const labels = { quote: "quotation", order: "sales order", invoice: "invoice" };
    const label = labels[documentType] || documentType;
    if (!window.confirm(`Save this document as a ${label} in Sage?`)) return;

    state.isSaving = true;
    setSaveButtonsEnabled(false);
    setLoadStatus(`Saving ${label}…`, "muted");

    try {
      const payload = buildSavePayload(documentType);
      const job = await WizShell.runWait("salesorder.save", {
        payload: JSON.stringify(payload),
      });

      if (!isJobOk(job)) throw new Error(job.error || "Save failed");

      const data = JSON.parse(job.resultJson || "{}");
      if (data.error) throw new Error(data.message || data.error);
      if (data.simulated)
        throw new Error(data.message || "Connector writes are disabled or simulated.");

      state.documentSaved = true;
      const orderNo = data.orderNo || payload.orderNo;
      if (orderNo) {
        document.getElementById("so-order-no").value = orderNo;
        state.orderNumber = orderNo;
      }

      if (documentType === "invoice" && data.invoiceReference) {
        document.getElementById("so-invoice-no").value = data.invoiceReference;
        setDocumentStatus("Processed");
        document.getElementById("so-window-title").textContent = `Sales Invoice ${data.invoiceReference}`;
      } else if (documentType === "quote") {
        setDocumentStatus("Quote");
        document.getElementById("so-window-title").textContent = `Sales Quotation ${orderNo}`;
      } else {
        setDocumentStatus("Unprocessed");
        document.getElementById("so-window-title").textContent = `Sales Order ${orderNo}`;
      }

      setLoadStatus(data.message || `${label} saved successfully.`, "ok");
    } catch (e) {
      console.warn("salesorder.save failed:", e.message);
      setLoadStatus(`Save failed: ${e.message}`, "error");
      setSaveButtonsEnabled(true);
    } finally {
      state.isSaving = false;
      if (!state.documentSaved) setSaveButtonsEnabled(true);
    }
  }

  async function loadSiteDatabaseStatus() {
    const companyEl = document.getElementById("so-status-company-db");
    const commonEl = document.getElementById("so-status-common-db");
    if (!companyEl || !commonEl) return;
    companyEl.textContent = "";
    commonEl.textContent = "";
    let companyDb = "";
    let commonDb = "";

    for (const op of ["site.health", "site.diagnostics"]) {
      try {
        const job = await WizShell.runWait(op, {});
        if (!isJobOk(job)) continue;
        const data = JSON.parse(job.resultJson);
        companyDb = companyDb || data.companyDatabase || "";
        commonDb = commonDb || data.commonDatabase || "";
        if (companyDb && commonDb) break;
      } catch (e) {
        console.warn(`${op} failed:`, e.message);
      }
    }

    if (!companyDb) {
      try {
        const res = await WizShell.runSql("SELECT DB_NAME() AS CompanyDatabase", 1);
        companyDb = res.grid?.rows?.[0]?.CompanyDatabase || res.grid?.rows?.[0]?.companyDatabase || "";
      } catch (e) {
        console.warn("DB_NAME() fallback failed:", e.message);
      }
    }

    if (companyDb) companyEl.textContent = `Sage DB: ${companyDb}`;
    if (commonDb) commonEl.textContent = `Common DB: ${commonDb}`;
  }

  async function loadLookups() {
    setLoadStatus("Loading Sage lookups…", "muted");
    state.itemStockCache = {};
    state.itemUnitsCache = {};
    state.itemPriceCache = {};
    state.warehouseFlags = null;
    const errors = [];
    for (const spec of REQUIRED_LOOKUPS) {
      const err = await fetchLookup(spec, false);
      if (err) errors.push(err);
    }
    const itemErr = await loadItemsForOrder();
    if (itemErr) errors.push(itemErr);
    await Promise.all(OPTIONAL_LOOKUPS.map((spec) => fetchLookup(spec, true)));
    await loadWarehouseDocumentFlags();

    bindLookups();
    initCustomerSearch();
    if (!document.querySelector("#so-lines-body tr")) addLine();
    recalcTotals();

    if (errors.length) {
      const detail = errors.join("; ");
      setLoadStatus(`${errors.length} lookup(s) failed: ${detail}`, "error");
      console.warn("Sales order lookup errors:", errors);
    } else {
      setLoadStatus(`Live data — ${state.customers.length} customers`, "ok");
    }
    document.getElementById("so-status-data").textContent =
      `Customers ${state.customers.length} · Items ${state.items.length} · WH ${state.warehouses.length}`;
    setSaveButtonsEnabled(true);
  }

  function customerLabel(c) {
    return `${c.code || ""} — ${c.name || ""}`.trim();
  }

  function escapeSqlLiteral(value) {
    return String(value ?? "").replace(/'/g, "''");
  }

  function itemCodeOf(i) {
    return (i.code || i.itemCode || i.Code || "").trim();
  }

  function itemDescription(i) {
    return (
      i.description_1 ||
      i.Description_1 ||
      i.description ||
      i.Description ||
      i.description1 ||
      i.name ||
      ""
    ).trim();
  }

  function warehouseDocumentSqlFilter() {
    return STOCK_DOCUMENT_TYPE === "purchase" ? "w.bAllowToBuyInto = 1" : "w.bAllowToSellFrom = 1";
  }

  function itemsForDocumentSql(top = 2000) {
    const whFilter = warehouseDocumentSqlFilter();
    return (
      `SELECT TOP ${top} s.Code, s.Description_1 FROM StkItem s ` +
      "WHERE s.ItemActive = 1 AND EXISTS (" +
      "SELECT 1 FROM _etblStockQtys q INNER JOIN WhseMst w ON q.WhseID = w.WhseLink " +
      `WHERE q.StockID = s.StockLink AND ${whFilter}) ORDER BY s.Code`
    );
  }

  function sellableItemCodesSql() {
    return (
      "SELECT DISTINCT s.Code FROM StkItem s " +
      "INNER JOIN _etblStockQtys q ON s.StockLink = q.StockID " +
      "INNER JOIN WhseMst w ON q.WhseID = w.WhseLink " +
      `WHERE s.ItemActive = 1 AND ${warehouseDocumentSqlFilter()}`
    );
  }

  async function loadItemsForOrder() {
    try {
      const res = await WizShell.runSql(itemsForDocumentSql(2000), 2000);
      const rows = res.grid?.rows || [];
      if (rows.length) {
        state.items = rows
          .map((row) => {
            const code = row.Code || row.code || "";
            const desc = row.Description_1 || row.description_1 || "";
            return { code, description_1: desc, description: desc };
          })
          .filter((i) => i.code);
        return null;
      }
    } catch (e) {
      console.warn("StkItem sales-item SQL load failed:", e.message);
    }
    const err = await fetchLookup({ key: "items", op: "inventoryitem.list", params: { top: "500" } }, false);
    if (state.items.length) {
      try {
        const res = await WizShell.runSql(sellableItemCodesSql(), 2000);
        const allowed = new Set((res.grid?.rows || []).map((row) => row.Code || row.code).filter(Boolean));
        if (allowed.size) {
          state.items = state.items.filter((i) => allowed.has(itemCodeOf(i)));
        }
      } catch (e) {
        console.warn("Sellable item code filter failed:", e.message);
      }
    }
    return err;
  }

  function parseSageBool(v) {
    if (v == null || v === "") return false;
    if (typeof v === "boolean") return v;
    const s = String(v).trim().toLowerCase();
    return s === "1" || s === "true" || s === "yes";
  }

  async function loadWarehouseDocumentFlags() {
    try {
      const res = await WizShell.runSql(
        "SELECT Code, bAllowToBuyInto, bAllowToSellFrom FROM WhseMst ORDER BY Code",
        500
      );
      const map = new Map();
      for (const row of res.grid?.rows || []) {
        const code = row.Code || row.code;
        if (!code) continue;
        map.set(code, {
          allowToBuyInto: parseSageBool(row.bAllowToBuyInto ?? row.BAllowToBuyInto),
          allowToSellFrom: parseSageBool(row.bAllowToSellFrom ?? row.BAllowToSellFrom),
        });
      }
      state.warehouseFlags = map.size ? map : null;
    } catch (e) {
      console.warn("WhseMst document flags load failed:", e.message);
      state.warehouseFlags = null;
    }
  }

  function applyDocumentWarehouseFilter(rows) {
    const flags = state.warehouseFlags;
    if (!flags?.size) return rows;
    return rows.filter((r) => {
      const wh = flags.get(r.warehouseCode);
      if (!wh) return false;
      return STOCK_DOCUMENT_TYPE === "purchase" ? wh.allowToBuyInto : wh.allowToSellFrom;
    });
  }

  function stockCacheKey(itemCode) {
    return `${STOCK_DOCUMENT_TYPE}:${itemCode}`;
  }

  function stockQtyJobParams(itemCode, extra = {}) {
    return { code: itemCode, documentType: STOCK_DOCUMENT_TYPE, ...extra };
  }

  function normalizeStockRow(r) {
    return {
      warehouseCode: r.warehouseCode || r.WarehouseCode || "",
      warehouseName: r.warehouseName || r.WarehouseName || "",
      qtyOnHand: parseNum(r.qtyOnHand ?? r.QtyOnHand),
      qtyOnSo: parseNum(r.qtyOnSo ?? r.QtyOnSO),
      qtyOnPo: parseNum(r.qtyOnPo ?? r.QtyOnPO),
    };
  }

  async function fetchItemStockRowsViaSql(itemCode) {
    const safe = escapeSqlLiteral(itemCode.trim());
    const sql =
      "SELECT w.Code AS WarehouseCode, w.Name AS WarehouseName, q.QtyOnHand, q.QtyOnSO, q.QtyOnPO " +
      "FROM StkItem s INNER JOIN _etblStockQtys q ON s.StockLink = q.StockID " +
      "INNER JOIN WhseMst w ON q.WhseID = w.WhseLink " +
      `WHERE s.ItemActive = 1 AND s.Code = '${safe}' AND ${warehouseDocumentSqlFilter()} ORDER BY w.Code`;
    const res = await WizShell.runSql(sql, 100);
    return (res.grid?.rows || [])
      .map((row) =>
        normalizeStockRow({
          WarehouseCode: row.WarehouseCode || row["Warehouse Code"],
          WarehouseName: row.WarehouseName || row["Warehouse Name"],
          QtyOnHand: row.QtyOnHand,
          QtyOnSO: row.QtyOnSO,
          QtyOnPO: row.QtyOnPO,
        })
      )
      .filter((r) => r.warehouseCode);
  }

  async function fetchItemStockRows(itemCode) {
    let rows = [];
    try {
      const job = await WizShell.runWait("inventoryitem.stock.qty", stockQtyJobParams(itemCode));
      if (job.status !== 3 && job.status !== "Failed" && job.resultJson) {
        const data = JSON.parse(job.resultJson);
        if (Array.isArray(data.rows) && data.rows.length) {
          rows = data.rows.map(normalizeStockRow).filter((r) => r.warehouseCode);
        }
      }
    } catch (e) {
      console.warn("inventoryitem.stock.qty failed:", e.message);
    }
    if (!rows.length) {
      try {
        rows = await fetchItemStockRowsViaSql(itemCode);
      } catch (e) {
        console.warn("Item warehouse SQL fallback failed:", e.message);
      }
    }
    return applyDocumentWarehouseFilter(rows);
  }

  function itemLabel(i) {
    const code = itemCodeOf(i);
    return `${code} — ${itemDescription(i).slice(0, 60)}`.trim();
  }

  function filterCustomers(query) {
    const t = query.trim().toLowerCase();
    if (!t) return state.customers.slice(0, 40);
    return state.customers
      .filter((c) => c.code?.toLowerCase().includes(t) || c.name?.toLowerCase().includes(t))
      .slice(0, 40);
  }

  function filterItems(query) {
    const t = query.trim().toLowerCase();
    if (!t) return state.items.slice(0, 40);
    return state.items
      .filter((i) => {
        const code = itemCodeOf(i).toLowerCase();
        const desc = itemDescription(i).toLowerCase();
        return code.includes(t) || desc.includes(t);
      })
      .slice(0, 40);
  }

  function renderSearchList(listEl, rows, onPick, options = {}) {
    const twoColumn = options.twoColumn === true;
    listEl.classList.toggle("so-search-list--cols", twoColumn);

    if (!rows.length) {
      listEl.innerHTML = twoColumn
        ? '<li class="so-search-list-header muted"><span>Code</span><span>Description</span></li><li class="muted"><span>No matches</span><span></span></li>'
        : '<li class="muted">No matches</li>';
      listEl.classList.remove("hidden");
      return;
    }

    if (twoColumn) {
      listEl.innerHTML =
        '<li class="so-search-list-header" aria-hidden="true"><span>Code</span><span>Description</span></li>' +
        rows
          .map((r, idx) => {
            const desc = WizShell.escapeHtml(r.description ?? r.label.replace(/^[^—]+—\s*/, ""));
            return `<li data-idx="${idx}" role="option"><span class="so-search-code">${WizShell.escapeHtml(r.code)}</span><span class="so-search-desc">${desc}</span></li>`;
          })
          .join("");
    } else {
      listEl.innerHTML = rows
        .map(
          (r, idx) =>
            `<li data-idx="${idx}" role="option"><span class="so-search-code">${WizShell.escapeHtml(r.code)}</span>${WizShell.escapeHtml(r.label.replace(/^[^—]+—\s*/, ""))}</li>`
        )
        .join("");
    }

    listEl.classList.remove("hidden");
    listEl.querySelectorAll("li[data-idx]").forEach((li) => {
      li.addEventListener("mousedown", (e) => {
        e.preventDefault();
        onPick(rows[Number(li.dataset.idx)]);
      });
    });
  }

  function initCustomerSearch() {
    const input = document.getElementById("so-customer-search");
    const hidden = document.getElementById("so-customer");
    const list = document.getElementById("so-customer-list");
    if (!input || !hidden || !list) return;

    const show = () => {
      const rows = filterCustomers(input.value).map((c) => ({ code: c.code, label: customerLabel(c), raw: c }));
      renderSearchList(list, rows, (row) => {
        hidden.value = row.code;
        input.value = customerLabel(row.raw);
        list.classList.add("hidden");
        loadCustomerDetail(row.code);
      });
    };

    input.addEventListener("focus", show);
    input.addEventListener("input", show);
    input.addEventListener("blur", () => {
      setTimeout(() => {
        list.classList.add("hidden");
        const t = input.value.trim();
        if (!t) {
          hidden.value = "";
          return;
        }
        const exact = state.customers.find(
          (c) => c.code?.toLowerCase() === t.toLowerCase() || customerLabel(c).toLowerCase() === t.toLowerCase()
        );
        const partial = filterCustomers(t)[0];
        const pick = exact || partial;
        if (pick) {
          hidden.value = pick.code;
          input.value = customerLabel(pick);
          loadCustomerDetail(pick.code);
        }
      }, 150);
    });
  }

  function bindItemSearch(tr) {
    const input = tr.querySelector(".so-item-search");
    const hidden = tr.querySelector(".so-item-code");
    const list = tr.querySelector(".so-item-list");
    if (!input || !hidden || !list) return;

    const show = () => {
      const rows = filterItems(input.value).map((i) => ({
        code: itemCodeOf(i),
        label: itemLabel(i),
        description: itemDescription(i),
        raw: i,
      }));
      renderSearchList(
        list,
        rows,
        (row) => {
          hidden.value = row.code;
          input.value = row.code;
          list.classList.add("hidden");
          onItemChange(tr, row.raw);
        },
        { twoColumn: true }
      );
    };

    input.addEventListener("focus", show);
    input.addEventListener("input", show);
    input.addEventListener("blur", () => {
      setTimeout(() => {
        list.classList.add("hidden");
        const t = input.value.trim();
        if (!t) {
          hidden.value = "";
          return;
        }
        const exact = state.items.find((i) => itemCodeOf(i).toLowerCase() === t.toLowerCase());
        const partial = filterItems(t)[0];
        const pick = exact || partial;
        if (pick) {
          hidden.value = itemCodeOf(pick);
          input.value = itemCodeOf(pick);
          onItemChange(tr, pick);
        }
      }, 150);
    });
  }

  function getLineItemCode(tr) {
    return tr.querySelector(".so-item-code")?.value || tr.querySelector(".so-item-search")?.value?.trim() || "";
  }

  function bindLookups() {
    WizShell.fillSelect(document.getElementById("so-project"), state.projects, "code", (p) => `${p.code} — ${p.name || ""}`, "—");
    WizShell.fillSelect(document.getElementById("so-rep"), state.reps, "code", (r) => `${r.code} — ${r.name || ""}`, "—");
    WizShell.fillSelect(document.getElementById("so-settlement"), state.settlement, "code", (s) => `${s.code} — ${s.description || ""}`, "—");
    WizShell.fillSelect(document.getElementById("so-order-status"), state.orderStatus, "code", (s) => `${s.code} — ${s.description || ""}`, "—");
    WizShell.fillSelect(document.getElementById("so-priority"), state.priorities, "code", (p) => `${p.code} — ${p.description || ""}`, "—");
    WizShell.fillSelect(
      document.getElementById("so-currency"),
      state.currencies,
      "code",
      (c) => `${c.code} — ${c.description || c.symbol || ""}`,
      "Home currency"
    );
  }

  function normalizeConversionRow(row) {
    return {
      unitAId: parseInt(row.iUnitAID ?? row.unitAId ?? row.UnitAID ?? 0, 10),
      unitAQty: parseNum(row.fUnitAQty ?? row.unitAQty ?? row.UnitAQty ?? 1) || 1,
      unitBId: parseInt(row.iUnitBID ?? row.unitBId ?? row.UnitBID ?? 0, 10),
      unitBQty: parseNum(row.fUnitBQty ?? row.unitBQty ?? row.UnitBQty ?? 1) || 1,
    };
  }

  function buildStockingUnitFactors(stockingUnitId, conversions) {
    const factors = new Map();
    if (!stockingUnitId) return factors;
    factors.set(stockingUnitId, 1);
    let changed = true;
    while (changed) {
      changed = false;
      for (const c of conversions) {
        if (factors.has(c.unitBId) && !factors.has(c.unitAId) && c.unitAQty > 0) {
          factors.set(c.unitAId, (factors.get(c.unitBId) * c.unitBQty) / c.unitAQty);
          changed = true;
        }
        if (factors.has(c.unitAId) && !factors.has(c.unitBId) && c.unitBQty > 0) {
          factors.set(c.unitBId, (factors.get(c.unitAId) * c.unitAQty) / c.unitBQty);
          changed = true;
        }
      }
    }
    return factors;
  }

  function convertQtyFromStocking(qtyStocking, stockingUnitId, targetUnitId, conversions) {
    if (!Number.isFinite(qtyStocking)) return 0;
    if (!targetUnitId || targetUnitId === stockingUnitId) return qtyStocking;
    const factors = buildStockingUnitFactors(stockingUnitId, conversions);
    const k = factors.get(targetUnitId);
    if (!k || k <= 0) return qtyStocking;
    return qtyStocking / k;
  }

  function convertPriceBetweenUnits(price, sourceUomId, targetUomId, stockingUnitId, conversions) {
    if (!Number.isFinite(price) || price <= 0) return price;
    if (!sourceUomId || !targetUomId || sourceUomId === targetUomId) return price;
    const factors = buildStockingUnitFactors(stockingUnitId, conversions);
    const kSource = factors.get(sourceUomId);
    const kTarget = factors.get(targetUomId);
    if (!kSource || !kTarget || kSource <= 0 || kTarget <= 0) return price;
    return round2(price * (kTarget / kSource));
  }

  function effectiveDefaultPriceUomId(sellUnitId, stockingUnitId) {
    const sell = parseInt(sellUnitId ?? 0, 10) || 0;
    const stock = parseInt(stockingUnitId ?? 0, 10) || 0;
    return sell > 0 ? sell : stock;
  }

  function enrichPriceRows(rows, sellUnitId, stockingUnitId) {
    const defaultUom = effectiveDefaultPriceUomId(sellUnitId, stockingUnitId);
    return rows.map((p) => {
      if (p.priceUomIdRaw === 0 && defaultUom > 0) {
        return { ...p, priceUomId: defaultUom };
      }
      return p;
    });
  }

  function normalizePriceRow(row) {
    const priceUomIdRaw = parseInt(row.priceUomIdRaw ?? row.PriceUomIdRaw ?? row.iUOMID ?? 0, 10) || 0;
    const priceUomId = parseInt(row.priceUomId ?? row.PriceUomId ?? row.EffectivePriceUomId ?? 0, 10) || 0;
    return {
      priceUomIdRaw,
      priceUomId,
      exclusivePrice: parseNum(row.exclusivePrice ?? row.ExclusivePrice ?? row.fExclPrice),
      inclusivePrice: parseNum(row.inclusivePrice ?? row.InclusivePrice ?? row.fInclPrice),
      isDefaultPriceList: !!(row.isDefaultPriceList ?? row.IsDefaultPriceList ?? row.bDefault),
    };
  }

  function resolvePriceForUnit(profile, unitId) {
    if (!profile?.prices?.length) return null;
    const defaultUom = effectiveDefaultPriceUomId(profile.sellUnitId, profile.stockingUnitId);
    const targetId = parseInt(unitId || defaultUom || 0, 10);
    const exact = profile.prices.find((p) => p.priceUomId === targetId);
    if (exact && (exact.inclusivePrice > 0 || exact.exclusivePrice > 0)) return { ...exact, fromConversion: false };

    const base =
      profile.prices.find((p) => p.isDefaultPriceList && p.priceUomIdRaw === 0) ||
      profile.prices.find((p) => p.priceUomIdRaw === 0) ||
      (defaultUom > 0 ? profile.prices.find((p) => p.priceUomId === defaultUom) : null) ||
      profile.prices.find((p) => p.isDefaultPriceList) ||
      profile.prices[0];
    if (!base) return null;
    if (!targetId || base.priceUomId === targetId) return { ...base, fromConversion: false };

    const unitsProfile = state.itemUnitsCache[profile.itemCode];
    const stockingUnitId =
      unitsProfile?.stockingUnitId || profile.stockingUnitId || profile.sellUnitId || base.priceUomId;
    const conversions = unitsProfile?.conversions || [];
    const inclusive = convertPriceBetweenUnits(
      base.inclusivePrice,
      base.priceUomId,
      targetId,
      stockingUnitId,
      conversions
    );
    const exclusive = convertPriceBetweenUnits(
      base.exclusivePrice,
      base.priceUomId,
      targetId,
      stockingUnitId,
      conversions
    );
    return {
      priceUomId: targetId,
      inclusivePrice: inclusive,
      exclusivePrice: exclusive,
      fromConversion: true,
    };
  }

  async function fetchItemPriceViaSql(itemCode) {
    const safe = escapeSqlLiteral(itemCode.trim());
    const priceUomCase =
      "CASE WHEN p.iUOMID = 0 THEN CASE WHEN ISNULL(s.iUOMDefSellUnitID, 0) > 0 " +
      "THEN s.iUOMDefSellUnitID ELSE s.iUOMStockingUnitID END ELSE p.iUOMID END";
    const sql =
      "SELECT s.iUOMStockingUnitID AS StockingUnitId, s.iUOMDefSellUnitID AS SellUnitId, p.iUOMID AS PriceUomIdRaw, " +
      priceUomCase +
      " AS PriceUomId, p.fExclPrice AS ExclusivePrice, p.fInclPrice AS InclusivePrice, n.bDefault AS IsDefaultPriceList " +
      "FROM StkItem s INNER JOIN _etblPriceListPrices p ON s.StockLink = p.iStockID " +
      "INNER JOIN _etblPriceListName n ON p.iPriceListNameID = n.IDPriceListName " +
      "WHERE s.Code = '" +
      safe +
      "' ORDER BY n.bDefault DESC, n.cName";
    const res = await WizShell.runSql(sql, 20);
    const rawRows = res.grid?.rows || [];
    if (!rawRows.length) return null;
    const sellUnitId = parseInt(rawRows[0].SellUnitId ?? rawRows[0].sellUnitId ?? 0, 10) || 0;
    const stockingUnitId = parseInt(rawRows[0].StockingUnitId ?? rawRows[0].stockingUnitId ?? 0, 10) || 0;
    const rows = enrichPriceRows(rawRows.map(normalizePriceRow), sellUnitId, stockingUnitId).filter(
      (p) => p.priceUomId > 0
    );
    if (!rows.length) return null;
    return { itemCode, sellUnitId, stockingUnitId, prices: rows };
  }

  async function fetchItemPrice(itemCode, unitId) {
    const cacheKey = `${itemCode}:${unitId || 0}`;
    if (state.itemPriceCache[cacheKey]) return state.itemPriceCache[cacheKey];

    let profile = null;
    const params = { code: itemCode };
    if (unitId) params.unitId = String(unitId);
    try {
      const job = await WizShell.runWait("inventoryitem.sellingprice", params);
      if (isJobOk(job)) {
        const data = JSON.parse(job.resultJson);
        profile = {
          itemCode,
          sellUnitId: parseInt(data.sellUnitId ?? 0, 10) || 0,
          stockingUnitId: parseInt(data.stockingUnitId ?? 0, 10) || 0,
          priceUomId: parseInt(data.priceUomId ?? 0, 10) || 0,
          exclusivePrice: parseNum(data.exclusivePrice),
          inclusivePrice: parseNum(data.inclusivePrice),
          prices: enrichPriceRows(
            (data.prices || []).map(normalizePriceRow),
            parseInt(data.sellUnitId ?? 0, 10) || 0,
            parseInt(data.stockingUnitId ?? 0, 10) || 0
          ).filter((p) => p.priceUomId > 0),
        };
        if (!profile.prices.length && (profile.inclusivePrice > 0 || profile.exclusivePrice > 0)) {
          const defaultUom = effectiveDefaultPriceUomId(profile.sellUnitId, profile.stockingUnitId);
          profile.prices = [
            {
              priceUomIdRaw: 0,
              priceUomId: profile.priceUomId || defaultUom,
              exclusivePrice: profile.exclusivePrice,
              inclusivePrice: profile.inclusivePrice,
              isDefaultPriceList: true,
            },
          ];
        }
      }
    } catch (e) {
      console.warn("inventoryitem.sellingprice failed:", e.message);
    }

    if (!profile?.prices?.length) {
      try {
        profile = await fetchItemPriceViaSql(itemCode);
      } catch (e) {
        console.warn("Item price SQL fallback failed:", e.message);
      }
    }

    if (profile) state.itemPriceCache[cacheKey] = profile;
    return profile;
  }

  function applyLinePriceForUom(tr, profile) {
    const uomSel = tr.querySelector(".so-uom");
    const unitId = parseInt(uomSel.value || tr.dataset.stockingUnitId || profile?.sellUnitId || 0, 10);
    const resolved = resolvePriceForUnit(profile, unitId);
    if (!resolved) return;

    const inclusive = document.getElementById("so-tax-inclusive").checked;
    const price = inclusive
      ? parseNum(resolved.inclusivePrice)
      : parseNum(resolved.exclusivePrice);
    if (price > 0) {
      tr.querySelector(".so-price").value = price;
      tr.dataset.priceUomId = String(resolved.priceUomId || unitId);
    }
  }

  function formatAvailQty(qty) {
    return formatQtyOnHand(qty);
  }

  async function fetchItemUnitsViaSql(itemCode) {
    const safe = escapeSqlLiteral(itemCode.trim());
    const headerSql =
      "SELECT si.iUOMStockingUnitID AS StockingUnitId, si.iUOMDefPurchaseUnitID AS PurchaseUnitId, " +
      "si.iUOMDefSellUnitID AS SellUnitId FROM StkItem si WHERE si.Code = '" +
      safe +
      "'";
    const unitsSql =
      "SELECT u.idUnits AS UnitId, u.cUnitCode AS UnitCode, u.cUnitDescription AS UnitDescription " +
      "FROM StkItem si INNER JOIN _etblUnits u ON u.idUnits IN " +
      "(si.iUOMStockingUnitID, si.iUOMDefPurchaseUnitID, si.iUOMDefSellUnitID) " +
      "WHERE si.Code = '" +
      safe +
      "' AND u.idUnits > 0 ORDER BY u.cUnitCode";

    const headerRes = await WizShell.runSql(headerSql, 5);
    const header = (headerRes.grid?.rows || [])[0];
    if (!header) return null;

    const stockingUnitId = parseInt(header.StockingUnitId ?? header.stockingUnitId ?? 0, 10) || 0;
    const sellUnitId = parseInt(header.SellUnitId ?? header.sellUnitId ?? 0, 10) || 0;
    const purchaseUnitId = parseInt(header.PurchaseUnitId ?? header.purchaseUnitId ?? 0, 10) || 0;

    const unitsRes = await WizShell.runSql(unitsSql, 10);
    const units = (unitsRes.grid?.rows || [])
      .map((row) => ({
        id: parseInt(row.UnitId ?? row.unitId ?? 0, 10),
        code: row.UnitCode || row.unitCode || "",
        description: row.UnitDescription || row.unitDescription || "",
      }))
      .filter((u) => u.id > 0);

    if (!units.length) return null;

    const idList = units.map((u) => u.id).join(",");
    const convSql =
      "SELECT uc.iUnitAID, uc.fUnitAQty, uc.iUnitBID, uc.fUnitBQty FROM _etblUnitConversion uc " +
      `WHERE uc.iUnitAID IN (${idList}) AND uc.iUnitBID IN (${idList})`;
    const convRes = await WizShell.runSql(convSql, 50);
    const conversions = (convRes.grid?.rows || []).map(normalizeConversionRow).filter((c) => c.unitAId && c.unitBId);

    return { stockingUnitId, sellUnitId, purchaseUnitId, units, conversions };
  }

  async function fetchItemUnits(itemCode) {
    if (state.itemUnitsCache[itemCode]) return state.itemUnitsCache[itemCode];
    let profile = null;
    try {
      const job = await WizShell.runWait("inventoryitem.units", { code: itemCode });
      if (job.status !== 3 && job.status !== "Failed" && job.resultJson) {
        const data = JSON.parse(job.resultJson);
        profile = {
          stockingUnitId: parseInt(data.stockingUnitId ?? 0, 10) || 0,
          sellUnitId: parseInt(data.sellUnitId ?? 0, 10) || 0,
          purchaseUnitId: parseInt(data.purchaseUnitId ?? 0, 10) || 0,
          units: (data.units || []).map((u) => ({
            id: parseInt(u.id ?? u.idUnits ?? 0, 10),
            code: u.code || u.unitCode || "",
            description: u.description || u.unitDescription || "",
          })).filter((u) => u.id > 0),
          conversions: (data.conversions || []).map(normalizeConversionRow),
        };
      }
    } catch (e) {
      console.warn("inventoryitem.units failed:", e.message);
    }
    if (!profile?.units?.length) {
      try {
        profile = await fetchItemUnitsViaSql(itemCode);
      } catch (e) {
        console.warn("Item units SQL fallback failed:", e.message);
      }
    }
    if (profile) state.itemUnitsCache[itemCode] = profile;
    return profile;
  }

  function uomOptionsHtml(units, selectedId) {
    const opts = ['<option value="">—</option>'];
    for (const u of units) {
      const id = String(u.id);
      opts.push(
        `<option value="${WizShell.escapeHtml(id)}"${id === String(selectedId) ? " selected" : ""}>${WizShell.escapeHtml(u.code)}${u.description ? ` — ${WizShell.escapeHtml(u.description)}` : ""}</option>`
      );
    }
    return opts.join("");
  }

  async function refreshItemUnits(tr) {
    const itemCode = getLineItemCode(tr);
    const uomSel = tr.querySelector(".so-uom");
    if (!itemCode) {
      uomSel.innerHTML = '<option value="">—</option>';
      return;
    }
    const profile = await fetchItemUnits(itemCode);
    if (!profile?.units?.length) {
      uomSel.innerHTML = '<option value="">—</option>';
      return;
    }
    const defaultId = profile.sellUnitId || profile.stockingUnitId || profile.units[0].id;
    const prev = uomSel.value;
    uomSel.innerHTML = uomOptionsHtml(profile.units, prev || defaultId);
    if (prev && profile.units.some((u) => String(u.id) === prev)) uomSel.value = prev;
    else uomSel.value = String(defaultId);
    tr.dataset.stockingUnitId = String(profile.stockingUnitId || "");
  }

  function displayAvailQty(tr, qtyStocking) {
    const itemCode = getLineItemCode(tr);
    const profile = state.itemUnitsCache[itemCode];
    const uomSel = tr.querySelector(".so-uom");
    const targetUnitId = parseInt(uomSel.value || tr.dataset.stockingUnitId || profile?.stockingUnitId || 0, 10);
    if (!profile || !targetUnitId) return formatAvailQty(qtyStocking);
    const converted = convertQtyFromStocking(
      qtyStocking,
      profile.stockingUnitId,
      targetUnitId,
      profile.conversions
    );
    return formatAvailQty(converted);
  }

  function formatQtyOnHand(qty) {
    const n = parseNum(qty);
    if (!Number.isFinite(n)) return "0";
    return n.toFixed(6).replace(/\.?0+$/, "") || "0";
  }

  function whLabel(row) {
    const code = row.warehouseCode || "";
    const wh = state.warehouses.find((w) => w.code === code);
    const name = row.warehouseName || wh?.name || "";
    const qty = formatQtyOnHand(row.qtyOnHand);
    return name ? `${code} — ${name} · Avail ${qty}` : `${code} · Avail ${qty}`;
  }

  function whOptionsHtml(selected, stockRows) {
    const opts = ['<option value="">—</option>'];
    if (stockRows === undefined) {
      for (const w of state.warehouses) {
        const code = w.code || "";
        opts.push(
          `<option value="${WizShell.escapeHtml(code)}"${code === selected ? " selected" : ""}>${WizShell.escapeHtml(code)}${w.name ? ` — ${WizShell.escapeHtml(w.name)}` : ""}</option>`
        );
      }
      return opts.join("");
    }
    for (const row of stockRows) {
      const code = row.warehouseCode || "";
      if (!code) continue;
      opts.push(
        `<option value="${WizShell.escapeHtml(code)}"${code === selected ? " selected" : ""}>${WizShell.escapeHtml(whLabel(row))}</option>`
      );
    }
    return opts.join("");
  }

  function repopulateWarehouseSelect(tr, stockRows, selected) {
    const sel = tr.querySelector(".so-wh");
    const prev = selected || sel.value;
    sel.innerHTML = whOptionsHtml(prev, stockRows);
    const codes = stockRows.map((r) => r.warehouseCode);
    if (prev && codes.includes(prev)) sel.value = prev;
    else if (stockRows.length === 1) sel.value = stockRows[0].warehouseCode;
  }

  function taxRateValue(t) {
    return t?.rate ?? t?.Rate ?? t?.taxRate ?? t?.TaxRate ?? "";
  }

  function taxRateForCode(code) {
    if (!code) return 0;
    const t = state.taxRates.find((x) => String(x.code) === String(code));
    return parseNum(taxRateValue(t));
  }

  function taxOptionsHtml(selected) {
    const opts = ['<option value="">— Select tax —</option>'];
    for (const t of state.taxRates) {
      const code = t.code || "";
      const rate = taxRateValue(t);
      const label = `${code} (${rate !== "" ? rate : "?"}%)`;
      opts.push(
        `<option value="${WizShell.escapeHtml(code)}" data-rate="${WizShell.escapeHtml(rate)}"${code === selected ? " selected" : ""}>${WizShell.escapeHtml(label)}</option>`
      );
    }
    return opts.join("");
  }

  function applyLineTaxType(tr, taxCode, taxRate) {
    const sel = tr.querySelector(".so-tax");
    if (!taxCode) return false;
    const known = state.taxRates.some((t) => String(t.code) === String(taxCode));
    if (known) sel.value = taxCode;
    else return false;
    const rateEl = tr.querySelector(".so-taxrate");
    rateEl.value = taxRate != null && taxRate !== "" ? String(taxRate) : String(taxRateForCode(taxCode));
    syncTaxRate(tr);
    return true;
  }

  async function fetchItemSalesTaxViaSql(itemCode) {
    const safe = escapeSqlLiteral(itemCode.trim());
    const sql =
      "SELECT TOP 1 t1.Code AS SalesTaxCode, t1.TaxRate AS SalesTaxRate " +
      "FROM StkItem s INNER JOIN _etblStockDetails d ON s.StockLink = d.StockID " +
      "INNER JOIN TaxRate t1 ON d.TTInvID = t1.idTaxRate " +
      `WHERE s.Code = '${safe}' ORDER BY d.idStockDetails`;
    const res = await WizShell.runSql(sql, 5);
    const row = (res.grid?.rows || [])[0];
    if (!row) return null;
    return {
      salesTaxCode: row.SalesTaxCode || row.salesTaxCode || row.Code || row.code,
      salesTaxRate: row.SalesTaxRate ?? row.salesTaxRate ?? row.TaxRate ?? row.taxRate,
    };
  }

  async function refreshLineTax(tr, forceDefault = true) {
    const itemCode = getLineItemCode(tr);
    const sel = tr.querySelector(".so-tax");
    if (!itemCode) {
      validateLineTax(tr);
      return;
    }
    if (!forceDefault && sel.value) {
      syncTaxRate(tr);
      return;
    }
    let taxCode = null;
    let taxRate = null;
    try {
      const job = await WizShell.runWait("inventoryitem.salestax", { code: itemCode });
      if (job.status !== 3 && job.status !== "Failed" && job.resultJson) {
        const data = JSON.parse(job.resultJson);
        taxCode = data.salesTaxCode;
        taxRate = data.salesTaxRate;
      }
    } catch (e) {
      console.warn("inventoryitem.salestax failed:", e.message);
    }
    if (!taxCode) {
      try {
        const row = await fetchItemSalesTaxViaSql(itemCode);
        if (row) {
          taxCode = row.salesTaxCode;
          taxRate = row.salesTaxRate;
        }
      } catch (e) {
        console.warn("Item sales tax SQL fallback failed:", e.message);
      }
    }
    if (taxCode) applyLineTaxType(tr, taxCode, taxRate);
    else syncTaxRate(tr);
    validateLineTax(tr);
  }

  function validateLineTax(tr) {
    const sel = tr.querySelector(".so-tax");
    const itemCode = getLineItemCode(tr);
    const missing = Boolean(itemCode) && !sel.value;
    sel.classList.toggle("so-required-missing", missing);
    sel.title = missing ? "Select a tax type (required for each line with an item)" : "";
    return !missing;
  }

  function validateAllLineTax() {
    let ok = true;
    document.querySelectorAll("#so-lines-body tr").forEach((tr) => {
      if (!validateLineTax(tr)) ok = false;
    });
    return ok;
  }

  function linesMissingTaxCount() {
    return [...document.querySelectorAll("#so-lines-body tr")].filter((tr) => {
      return getLineItemCode(tr) && !tr.querySelector(".so-tax").value;
    }).length;
  }

  function formatDiscPct(pct) {
    if (!Number.isFinite(pct) || pct === 0) return "0";
    return pct.toFixed(4).replace(/\.?0+$/, "");
  }

  function lineDiscBaseIncl(tr, qtyClass = "so-confirm") {
    return parseNum(tr.querySelector(".so-price").value) * parseNum(tr.querySelector(`.${qtyClass}`).value);
  }

  function syncLineDiscFromPct(tr) {
    const base = lineDiscBaseIncl(tr);
    const pct = parseNum(tr.querySelector(".so-disc").value);
    tr.querySelector(".so-disc-amt").value = base > 0 ? (base * pct / 100).toFixed(2) : "0.00";
  }

  function syncLineDiscFromAmt(tr) {
    const base = lineDiscBaseIncl(tr);
    const amt = parseNum(tr.querySelector(".so-disc-amt").value);
    tr.querySelector(".so-disc").value = formatDiscPct(base > 0 ? (amt / base) * 100 : 0);
  }

  function resyncLineDiscount(tr) {
    if (tr.dataset.lineDiscSource === "amount") syncLineDiscFromAmt(tr);
    else syncLineDiscFromPct(tr);
  }

  function addLine(data = {}) {
    const tbody = document.getElementById("so-lines-body");
    const idx = tbody.rows.length + 1;
    const tr = document.createElement("tr");
    tr.innerHTML = `
      <td class="so-line-no">${idx}</td>
      <td><select class="so-module"><option value="ST" selected>ST</option><option value="GL">GL</option></select></td>
      <td class="so-cell-item">
        <input type="text" class="so-item-search so-search-input" value="${WizShell.escapeHtml(data.item || "")}" placeholder="Code or name…" autocomplete="off" spellcheck="false" />
        <input type="hidden" class="so-item-code" value="${WizShell.escapeHtml(data.item || "")}" />
        <ul class="so-search-list so-item-list hidden" role="listbox"></ul>
      </td>
      <td><input class="so-desc" type="text" value="${WizShell.escapeHtml(data.description || "")}" title="Defaults from item; editable" /></td>
      <td><select class="so-wh">${whOptionsHtml(data.warehouse || "", [])}</select></td>
      <td><input class="so-avail" type="text" readonly tabindex="-1" /></td>
      <td><input class="so-qty" type="number" step="0.000001" min="0" value="${data.qty ?? 1}" /></td>
      <td><select class="so-uom"><option value="">—</option></select></td>
      <td><input class="so-proc" type="number" readonly tabindex="-1" value="0" /></td>
      <td><input class="so-confirm" type="number" step="0.000001" min="0" value="${data.confirmQty ?? data.qty ?? 1}" /></td>
      <td><input class="so-price" type="number" step="0.01" min="0" value="${data.price ?? 0}" /></td>
      <td><select class="so-tax">${taxOptionsHtml(data.taxType || "")}</select></td>
      <td><input class="so-taxrate" type="text" readonly tabindex="-1" /></td>
      <td><input class="so-disc" type="text" inputmode="decimal" autocomplete="off" value="${data.discPct ?? 0}" /></td>
      <td><input class="so-disc-amt" type="text" inputmode="decimal" autocomplete="off" value="${data.discAmt ?? "0.00"}" /></td>
      <td><input class="so-linetotal" type="text" readonly tabindex="-1" /></td>
      <td><button type="button" class="so-btn so-row-del" title="Delete line">×</button></td>`;
    tbody.appendChild(tr);
    bindLineEvents(tr);
    bindItemSearch(tr);
    if (data.item) onItemChange(tr);
    else {
      syncTaxRate(tr);
      validateLineTax(tr);
      recalcLine(tr);
      recalcTotals();
    }
    renumberLines();
  }

  function renumberLines() {
    document.querySelectorAll("#so-lines-body tr").forEach((tr, i) => {
      tr.querySelector(".so-line-no").textContent = String(i + 1);
    });
  }

  function bindLineEvents(tr) {
    tr.addEventListener("click", () => {
      document.querySelectorAll("#so-lines-body tr").forEach((r) => r.classList.remove("selected"));
      tr.classList.add("selected");
      state.selectedLine = tr;
    });
    tr.querySelector(".so-row-del").addEventListener("click", (e) => {
      e.stopPropagation();
      tr.remove();
      renumberLines();
      recalcTotals();
    });
    tr.querySelector(".so-wh").addEventListener("change", () => refreshLineStock(tr));
    tr.querySelector(".so-uom").addEventListener("change", async () => {
      refreshLineStock(tr);
      await refreshLinePrice(tr);
      recalcLine(tr);
      recalcTotals();
    });
    tr.querySelector(".so-desc").addEventListener("input", () => {
      tr.dataset.descEdited = "1";
    });
    tr.querySelector(".so-tax").addEventListener("change", () => {
      syncTaxRate(tr);
      validateLineTax(tr);
    });
    tr.querySelector(".so-qty").addEventListener("input", () => {
      resyncLineDiscount(tr);
      recalcLine(tr);
      recalcTotals();
    });
    tr.querySelector(".so-confirm").addEventListener("focus", () => {
      tr.dataset.confirmBeforeEdit = tr.querySelector(".so-confirm").value;
    });
    tr.querySelector(".so-confirm").addEventListener("input", () => {
      const confirmEl = tr.querySelector(".so-confirm");
      const confirm = parseNum(confirmEl.value);
      const before = parseNum(tr.dataset.confirmBeforeEdit ?? confirmEl.value);
      if (confirm > before) {
        tr.querySelector(".so-qty").value = confirmEl.value;
      }
      tr.dataset.confirmBeforeEdit = confirmEl.value;
      resyncLineDiscount(tr);
      recalcLine(tr);
      recalcTotals();
    });
    tr.querySelector(".so-price").addEventListener("input", () => {
      resyncLineDiscount(tr);
      recalcLine(tr);
      recalcTotals();
    });
    tr.querySelector(".so-disc").addEventListener("input", () => {
      tr.dataset.lineDiscSource = "pct";
      syncLineDiscFromPct(tr);
      recalcLine(tr);
      recalcTotals();
    });
    tr.querySelector(".so-disc-amt").addEventListener("input", () => {
      tr.dataset.lineDiscSource = "amount";
      syncLineDiscFromAmt(tr);
      recalcLine(tr);
      recalcTotals();
    });
  }

  async function onItemChange(tr, itemOverride) {
    const code = getLineItemCode(tr);
    const item =
      itemOverride ||
      state.items.find((i) => itemCodeOf(i) === code);
    if (item) {
      tr.querySelector(".so-desc").value = itemDescription(item);
      tr.dataset.descEdited = "";
    }
    await refreshItemWarehouses(tr);
    await refreshItemUnits(tr);
    await refreshLineTax(tr, true);
    refreshLineStock(tr);
    await refreshLinePrice(tr);
    recalcLine(tr);
    recalcTotals();
    validateAllLineTax();
  }

  async function refreshItemWarehouses(tr) {
    const itemCode = getLineItemCode(tr);
    const whSel = tr.querySelector(".so-wh");
    if (!itemCode) {
      whSel.innerHTML = whOptionsHtml("", []);
      return;
    }
    const cacheKey = stockCacheKey(itemCode);
    if (state.itemStockCache[cacheKey]) {
      repopulateWarehouseSelect(tr, state.itemStockCache[cacheKey], whSel.value);
      return;
    }
    whSel.innerHTML = whOptionsHtml("", []);
    const rows = await fetchItemStockRows(itemCode);
    state.itemStockCache[cacheKey] = rows;
    repopulateWarehouseSelect(tr, rows, whSel.value);
  }

  async function refreshLineStock(tr) {
    const itemCode = getLineItemCode(tr);
    const warehouse = tr.querySelector(".so-wh").value;
    const availEl = tr.querySelector(".so-avail");
    if (!itemCode) {
      availEl.value = "";
      return;
    }
    availEl.value = "…";
    try {
      const cacheKey = stockCacheKey(itemCode);
      let rows = state.itemStockCache[cacheKey];
      if (!rows) {
        rows = await fetchItemStockRows(itemCode);
        state.itemStockCache[cacheKey] = rows;
      }
      if (warehouse) {
        const row = rows.find((r) => r.warehouseCode === warehouse);
        availEl.value = row ? displayAvailQty(tr, parseNum(row.qtyOnHand)) : "0";
      } else if (rows.length) {
        const total = rows.reduce((s, r) => s + parseNum(r.qtyOnHand), 0);
        availEl.value = displayAvailQty(tr, total) + " (all WH)";
      } else {
        availEl.value = "0";
      }
    } catch {
      availEl.value = "—";
    }
  }

  async function refreshLinePrice(tr) {
    const itemCode = getLineItemCode(tr);
    if (!itemCode) return;
    const priceEl = tr.querySelector(".so-price");
    priceEl.value = "";
    try {
      const unitId = parseInt(tr.querySelector(".so-uom").value || tr.dataset.stockingUnitId || 0, 10);
      const profile = await fetchItemPrice(itemCode, unitId);
      if (!profile) return;
      applyLinePriceForUom(tr, profile);
    } catch (e) {
      console.warn("refreshLinePrice failed:", e.message);
    }
  }

  function syncTaxRate(tr) {
    const sel = tr.querySelector(".so-tax");
    const code = sel.value;
    const opt = sel.selectedOptions[0];
    let rate = opt?.dataset?.rate ?? "";
    if ((!rate || rate === "?") && code) rate = String(taxRateForCode(code));
    tr.querySelector(".so-taxrate").value = rate;
    recalcLine(tr);
    recalcTotals();
  }

  function lineAmounts(tr) {
    const qty = parseNum(tr.querySelector(".so-qty").value);
    const confirmQty = parseNum(tr.querySelector(".so-confirm").value);
    const priceIncl = parseNum(tr.querySelector(".so-price").value);
    const disc = parseNum(tr.querySelector(".so-disc").value);
    const taxRate = parseNum(tr.querySelector(".so-taxrate").value);
    const inclusive = document.getElementById("so-tax-inclusive").checked;

    const calc = (q) => {
      let lineIncl = priceIncl * q;
      if (disc > 0) lineIncl *= 1 - disc / 100;
      let excl, tax;
      if (inclusive && taxRate > 0) {
        excl = lineIncl / (1 + taxRate / 100);
        tax = lineIncl - excl;
      } else {
        excl = lineIncl;
        tax = excl * (taxRate / 100);
        lineIncl = excl + tax;
      }
      return { excl, tax, incl: lineIncl };
    };

    return { ordered: calc(qty), confirmed: calc(confirmQty) };
  }

  function recalcLine(tr) {
    tr.querySelector(".so-linetotal").value = money(lineAmounts(tr).confirmed.incl);
  }

  function documentInclBeforeDiscount() {
    let total = 0;
    document.querySelectorAll("#so-lines-body tr").forEach((tr) => {
      total += lineAmounts(tr).confirmed.incl;
    });
    return total;
  }

  function syncDiscountFromAmount() {
    const totalIncl = documentInclBeforeDiscount();
    const amt = parseNum(document.getElementById("so-discount-amt").value);
    const pct = totalIncl > 0 ? (amt / totalIncl) * 100 : 0;
    document.getElementById("so-discount-pct").value = pct === 0 ? "0" : formatDiscPct(pct);
  }

  function syncDiscountFromPct() {
    const totalIncl = documentInclBeforeDiscount();
    const pct = parseNum(document.getElementById("so-discount-pct").value);
    document.getElementById("so-discount-amt").value = round2(totalIncl * pct / 100).toFixed(2);
  }

  /** Apply footer document discount; keep incl = excl + tax after rounding. */
  function applyDocumentDiscount(excl, tax, incl, confirmedInclBase) {
    if (incl <= 0) return { excl: 0, tax: 0, incl: 0 };

    const docDiscAmt = parseNum(document.getElementById("so-discount-amt").value);
    const docDiscPct = parseNum(document.getElementById("so-discount-pct").value);
    let finalIncl;

    if (state.discountSource === "amount" && docDiscAmt > 0) {
      const base = confirmedInclBase > 0 ? confirmedInclBase : incl;
      const scaledDisc = round2(docDiscAmt * (incl / base));
      finalIncl = round2(Math.max(0, incl - scaledDisc));
    } else if (docDiscPct > 0) {
      finalIncl = round2(incl * (1 - docDiscPct / 100));
    } else {
      return { excl: round2(excl), tax: round2(tax), incl: round2(incl) };
    }

    const ratio = incl > 0 ? finalIncl / incl : 0;
    const finalEx = round2(excl * ratio);
    const finalTax = round2(finalIncl - finalEx);
    return { excl: finalEx, tax: finalTax, incl: round2(finalEx + finalTax) };
  }

  function recalcTotals() {
    let oEx = 0, oTax = 0, oIn = 0, cEx = 0, cTax = 0, cIn = 0;
    document.querySelectorAll("#so-lines-body tr").forEach((tr) => {
      const a = lineAmounts(tr);
      oEx += a.ordered.excl;
      oTax += a.ordered.tax;
      oIn += a.ordered.incl;
      cEx += a.confirmed.excl;
      cTax += a.confirmed.tax;
      cIn += a.confirmed.incl;
    });

    if (state.discountSource === "amount") syncDiscountFromAmount();
    else if (state.discountSource === "pct") syncDiscountFromPct();

    const o = applyDocumentDiscount(oEx, oTax, oIn, cIn);
    const c = applyDocumentDiscount(cEx, cTax, cIn, cIn);
    document.getElementById("tot-excl-ordered").textContent = money(o.excl);
    document.getElementById("tot-tax-ordered").textContent = money(o.tax);
    document.getElementById("tot-incl-ordered").textContent = money(o.incl);
    document.getElementById("tot-excl-confirmed").textContent = money(c.excl);
    document.getElementById("tot-tax-confirmed").textContent = money(c.tax);
    document.getElementById("tot-incl-confirmed").textContent = money(c.incl);

    const missingTax = linesMissingTaxCount();
    const taxHint = document.getElementById("so-status-tax-hint");
    if (taxHint) {
      taxHint.textContent = missingTax
        ? `${missingTax} line(s) missing tax — select VAT on each item line`
        : "";
      taxHint.classList.toggle("error", missingTax > 0);
    }
  }

  async function loadCustomerDetail(code) {
    if (!code) {
      document.getElementById("so-address").value = "";
      return;
    }
    state.customerAddresses[code] = { delivery: "", postal: "", contact: "" };

    try {
      const job = await WizShell.runWait("customer.address", { code });
      if (job.resultJson) {
        const c = JSON.parse(job.resultJson);
        state.customerAddresses[code] = {
          delivery: c.deliveryAddress || "",
          postal: c.physicalAddress || c.postalAddress || "",
          contact: "",
        };
      }
    } catch (e) {
      console.warn("customer.address failed:", e.message);
    }

    try {
      const job = await WizShell.runWait("customer.get", { code });
      if (job.resultJson) {
        const c = JSON.parse(job.resultJson);
        state.customerAddresses[code].contact = [c.telephone, c.email].filter(Boolean).join("\n");
        if (!state.customerAddresses[code].delivery && c.name)
          state.customerAddresses[code].delivery = c.name;
      }
    } catch { /* optional */ }

    if (!state.customerAddresses[code].delivery) {
      const cust = state.customers.find((x) => x.code === code);
      state.customerAddresses[code].delivery = cust?.name || code;
      state.customerAddresses[code].postal = cust?.name || code;
    }
    showAddressTab(state.addrTab);
  }

  function showAddressTab(tab) {
    state.addrTab = tab;
    document.querySelectorAll(".so-addr-tab").forEach((b) => b.classList.toggle("active", b.dataset.addr === tab));
    const code = document.getElementById("so-customer").value;
    document.getElementById("so-address").value = state.customerAddresses[code]?.[tab] || "";
  }

  function resetForm() {
    document.getElementById("so-form").reset();
    document.getElementById("so-customer").value = "";
    document.getElementById("so-customer-search").value = "";
    document.getElementById("so-order-no").value = "";
    state.orderNumber = null;
    state.documentSaved = false;
    state.isSaving = false;
    setDocumentStatus("Unprocessed");
    document.getElementById("so-invoice-date").value = todayIso();
    document.getElementById("so-due-date").value = todayIso();
    document.getElementById("so-discount-amt").value = "0";
    state.discountSource = null;
    state.itemStockCache = {};
    state.itemUnitsCache = {};
    state.itemPriceCache = {};
    state.warehouseFlags = null;
    document.getElementById("so-lines-body").innerHTML = "";
    document.getElementById("so-invoice-no").value = "";
    document.getElementById("so-window-title").textContent = "Editing Sales Order (new)";
    document.getElementById("so-order-date").value = todayIso();
    addLine();
    recalcTotals();
    setSaveButtonsEnabled(true);
    allocateOrderNumber();
  }

  function runAiAssist() {
    const prompt = document.getElementById("so-ai-prompt").value.trim();
    const out = document.getElementById("so-ai-result");
    if (!prompt) {
      out.textContent = "Enter a description first.";
      return;
    }
    out.textContent =
      "AI draft (Phase 1 preview):\n\n" +
      "• Parses customer, items, quantities from your text\n" +
      "• Checks credit limit + stock availability via connector\n" +
      "• Suggests prices from Sage price lists\n" +
      "• Flags margin / below-cost lines before posting\n\n" +
      "Full NL → line population will call Insight chat + Act propose in Phase 2.\n\n" +
      `Your request: "${prompt}"`;
  }

  document.addEventListener("DOMContentLoaded", () => {
    document.getElementById("so-order-date").value = todayIso();
    document.getElementById("so-invoice-date").value = todayIso();
    document.getElementById("so-due-date").value = todayIso();

    document.getElementById("site-select")?.addEventListener("change", () => {
      state.itemStockCache = {};
      state.itemUnitsCache = {};
      state.itemPriceCache = {};
      state.warehouseFlags = null;
      loadLookups();
      loadSiteDatabaseStatus();
      allocateOrderNumber();
    });
    document.getElementById("so-discount-pct").addEventListener("input", () => {
      state.discountSource = "pct";
      syncDiscountFromPct();
      recalcTotals();
    });
    document.getElementById("so-discount-amt").addEventListener("input", () => {
      state.discountSource = "amount";
      syncDiscountFromAmount();
      recalcTotals();
    });
    document.getElementById("so-tax-inclusive").addEventListener("change", () => {
      document.getElementById("so-status-tax").textContent =
        document.getElementById("so-tax-inclusive").checked ? "Tax Inclusive (Line)" : "Tax Exclusive (Line)";
      document.querySelectorAll("#so-lines-body tr").forEach((tr) => {
        refreshLinePrice(tr);
        recalcLine(tr);
      });
      recalcTotals();
    });

    document.querySelectorAll(".so-addr-tab").forEach((b) =>
      b.addEventListener("click", () => showAddressTab(b.dataset.addr))
    );

    document.querySelectorAll(".so-htab").forEach((tab) => {
      tab.addEventListener("click", () => {
        if (tab.disabled) return;
        document.querySelectorAll(".so-htab").forEach((t) => t.classList.remove("active"));
        tab.classList.add("active");
        const id = tab.dataset.htab;
        document.getElementById("so-panel-delivery").classList.toggle("hidden", id !== "delivery");
        document.getElementById("so-panel-currency").classList.toggle("hidden", id !== "currency");
        if (id === "document") {
          document.getElementById("so-panel-delivery").classList.add("hidden");
          document.getElementById("so-panel-currency").classList.add("hidden");
        }
      });
    });
    document.getElementById("so-panel-delivery").classList.add("hidden");
    document.getElementById("so-panel-currency").classList.add("hidden");

    document.getElementById("btn-add-line").addEventListener("click", () => addLine());
    document.getElementById("btn-remove-line").addEventListener("click", () => {
      if (state.selectedLine) {
        state.selectedLine.remove();
        state.selectedLine = null;
        renumberLines();
        recalcTotals();
      }
    });
    document.getElementById("btn-new").addEventListener("click", resetForm);
    document.getElementById("btn-quote").addEventListener("click", () => saveDocument("quote"));
    document.getElementById("btn-place-order").addEventListener("click", () => saveDocument("order"));
    document.getElementById("btn-process").addEventListener("click", () => saveDocument("invoice"));
    document.getElementById("btn-close").addEventListener("click", () => { window.location.href = "../index.html"; });

    document.getElementById("btn-ai-assist").addEventListener("click", () => document.getElementById("so-ai-dialog").showModal());
    document.getElementById("so-ai-cancel").addEventListener("click", () => document.getElementById("so-ai-dialog").close());
    document.getElementById("so-ai-run").addEventListener("click", runAiAssist);

    setTimeout(() => {
      try {
        if (WizShell.siteId()) {
          loadLookups();
          loadSiteDatabaseStatus();
          allocateOrderNumber();
        } else {
          setLoadStatus("Select online site in left panel", "error");
        }
      } catch {
        setLoadStatus("Select online site in left panel", "error");
      }
    }, 400);
  });
})();
