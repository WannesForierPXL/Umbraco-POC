// Contracttest voor de stub-API uit docs/contentmodel.md.
// CMS-onafhankelijk: dezelfde test hoort te slagen tegen de Umbraco- én de Payload-stub (ids aanpassen).
// Gebruik (stub en CMS draaien, voorbeeldpad geladen):
//   dotnet dev-certs https -ep devcert.pem --format PEM --no-password
//   NODE_EXTRA_CA_CERTS=devcert.pem node tests/stub-contract.mjs
const S = "https://localhost:5001";
const LID = { "X-Mock-Lid": "true" };
const id = { guide: "acbfb072-94c6-45d7-b1d8-7d0a35da3d81", pub: "a1f5e7f2-32a5-4222-95d6-dc8a31d2659e", led: "d2cc20ea-8a96-4b38-b990-4e25ca00ebb6", nl: "587ee0dd-0370-4673-8e84-68426eabe2d1",
  str: "3d839363-e5b9-4e3c-85fa-5626a1ae16bb", res: "96ce0bcf-343d-4b4e-af15-3d787ed2a4a0", pdf: "9a546ef3-e22c-49d9-b84e-8bb09e9da2c9", img: "04cefec9-1d35-49e9-9bca-69f7aedc60ad" };
let ok = 0, fout = 0;
async function check(naam, pad, headers, verwacht) {
  const r = await fetch(S + pad, { headers });
  const type = r.headers.get("content-type") ?? "";
  const body = type.includes("json") ? await r.json() : await r.arrayBuffer();
  let res; try { res = verwacht(r.status, body, type); } catch (e) { res = String(e); }
  if (res === true) { ok++; console.log("  ✓", naam); } else { fout++; console.log("  ✗", naam, "→", r.status, res); }
}
await check("Guides: lijst bevat de voorbeeld-Guide", "/guides", {}, (s, b) => s === 200 && b.some(g => g.id === id.guide));
await check("Bezoeker: guide toont leden-stap als Teaser", `/guides/${id.guide}`, {}, (s, b) => s === 200 && b.hoofdstukken[0].stappen[1].afgeschermd === true);
await check("Bezoeker: leden-hoofdstuk zichtbaar maar afgeschermd", `/guides/${id.guide}`, {}, (s, b) => b.hoofdstukken[1].afgeschermd === true && b.hoofdstukken[1].stappen.length === 1);
await check("Strengste wint in de lijst (publieke stap in leden-hoofdstuk = leden)", `/guides/${id.guide}`, {}, (s, b) => b.hoofdstukken[1].stappen[0].toegangsniveau === "leden");
await check("Lid EN: niets afgeschermd, enkel-NL stap valt terug op nl", `/guides/${id.guide}?taal=en`, LID, (s, b) =>
  b.taal === "en" && b.hoofdstukken.every(h => !h.afgeschermd && h.stappen.every(x => !x.afgeschermd)) && b.hoofdstukken[0].stappen[2].taal === "nl" && b.hoofdstukken[0].stappen[0].taal === "en");
await check("Publieke stap: 200 met 3 blokken", `/guide/stappen/${id.pub}`, {}, (s, b) => s === 200 && b.blokken.map(x => x.type).join() === "tekst,afbeelding,callout");
await check("Afbeelding-URL wijst naar de stub", `/guide/stappen/${id.pub}`, {}, (s, b) => b.blokken[1].url === `/media/${id.img}`);
await check("Leden-stap als Bezoeker: 403 Teaser zonder blokken", `/guide/stappen/${id.led}`, {}, (s, b) => s === 403 && b.blokken === undefined && b.samenvatting.length > 0);
await check("Leden-stap als Lid: 200 met blokken", `/guide/stappen/${id.led}`, LID, (s, b) => s === 200 && b.blokken.length === 2);
await check("Strengste wint in detail: 403 voor Bezoeker", `/guide/stappen/${id.str}`, {}, (s, b) => s === 403 && b.toegangsniveau === "leden");
await check("Terugval: enkel-NL stap in EN → taal nl, gevraagdeTaal en", `/guide/stappen/${id.nl}?taal=en`, {}, (s, b) => s === 200 && b.taal === "nl" && b.gevraagdeTaal === "en");
await check("EN-versie van stap heeft EN-hoofdstuk en verwijst naar zijn Guide", `/guide/stappen/${id.pub}?taal=en`, {}, (s, b) => b.taal === "en" && b.hoofdstuk.titel === "Basics" && b.guide.id === id.guide);
await check("Resources als Bezoeker: leden-resource zonder bestand", "/resources", {}, (s, b) => s === 200 && b[1].afgeschermd && b[1].bestandUrl === null && b[0].url.startsWith("https://"));
await check("Leden-resource als Lid: bestandUrl via stub", `/resources/${id.res}`, LID, (s, b) => s === 200 && b.bestandUrl === `/media/${id.pdf}`);
await check("Leden-resource als Bezoeker: 403", `/resources/${id.res}`, {}, (s) => s === 403);
await check("Media-proxy pdf als Bezoeker: 403", `/media/${id.pdf}`, {}, (s) => s === 403);
await check("Media-proxy pdf als Lid: 200 pdf", `/media/${id.pdf}`, LID, (s, b, t) => s === 200 && t.includes("pdf") && Buffer.from(b).subarray(0, 5).toString() === "%PDF-");
await check("Media-proxy beeld uit publieke stap als Bezoeker: 200 png", `/media/${id.img}`, {}, (s, b, t) => s === 200 && t.includes("png"));
await check("Media-proxy onbekend id: 404", "/media/00000000-0000-0000-0000-000000000000", {}, (s) => s === 404);
await check("Onbekende stap: 404", "/guide/stappen/00000000-0000-0000-0000-000000000000", {}, (s) => s === 404);
console.log(`\n${ok} geslaagd, ${fout} gefaald`);
process.exit(fout ? 1 : 0);
