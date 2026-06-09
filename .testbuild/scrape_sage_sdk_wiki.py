import urllib.parse
import urllib.request
import re
import json

BASE = "https://developerzone.pastel.co.za/index.php?title="
START = ["User_Guide", "Accounts_Receivable", "General_Ledger", "Inventory", "Order_Entry", "Job_Costing", "Additional_Functionality"]

visited = set()
queue = list(START)
results = {}

def fetch(title):
    url = BASE + urllib.parse.quote(title.replace(" ", "_"))
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=25) as r:
        return r.read().decode("utf-8", "replace")

def strip_html(html):
    html = re.sub(r"(?is)<script.*?>.*?</script>", " ", html)
    html = re.sub(r"(?is)<style.*?>.*?</style>", " ", html)
    m = re.search(r'(?is)<div class="mw-parser-output">(.*?)</div>\s*<div', html)
    if not m:
        m = re.search(r'(?is)<div class="mw-parser-output">(.*)', html)
    body = m.group(1) if m else html
    body = re.sub(r"(?is)<h2[^>]*>(.*?)</h2>", r"\n## \1\n", body)
    body = re.sub(r"(?is)<h3[^>]*>(.*?)</h3>", r"\n### \1\n", body)
    body = re.sub(r"(?is)<li[^>]*>(.*?)</li>", r"\n- \1", body)
    body = re.sub(r"<[^>]+>", " ", body)
    body = re.sub(r"\s+", " ", body)
    return body.strip()

while queue and len(visited) < 80:
    title = queue.pop(0)
    if title in visited:
        continue
    visited.add(title)
    try:
        html = fetch(title)
    except Exception as e:
        results[title] = {"error": str(e)}
        continue
    text = strip_html(html)
    links = re.findall(r'/index.php\?title=([^"#&]+)', html)
    sdk_links = []
    for l in links:
        t = urllib.parse.unquote(l.replace("_", " "))
        if t.startswith(("Special:", "Talk:", "Help:", "File:")):
            continue
        sdk_links.append(l.replace(" ", "_"))
        if l.replace("_", " ") not in visited and l.replace("_", " ") not in [q.replace("_", " ") for q in queue]:
            if any(k in l.lower() for k in ["customer", "supplier", "inventory", "general", "ledger", "order", "journal", "cashbook", "warehouse", "job", "batch", "transaction", "account", "sales", "purchase", "credit", "allocation", "requirement", "connection", "licens", "troubleshoot", "additional", "contact", "python", "delphi", "php"]):
                queue.append(l.replace("_", " "))
    results[title] = {"text": text[:4000], "links": sorted(set(sdk_links))[:30]}

out = r"c:\Users\pj\WizAccountant\.testbuild\sage_sdk_wiki.json"
with open(out, "w", encoding="utf-8") as f:
    json.dump(results, f, indent=2)
print("pages", len(results), "out", out)
for t, d in results.items():
    if "error" in d:
        print(t, "ERR", d["error"][:60])
    else:
        print(t, "OK", len(d["text"]), "chars")
