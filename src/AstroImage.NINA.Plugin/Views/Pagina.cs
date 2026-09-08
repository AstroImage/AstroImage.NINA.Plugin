namespace AstroImage.NINA.Plugin.Views {

    /*  LA PAGINA DI PROVA, e si chiama cosi' perche' lo e'.
     *
     *  Serve a dimostrare che il giro si chiude: la pagina chiede, il ponte va a
     *  prendere, il servizio calcola col motore vero, e la risposta torna qui. Niente
     *  di piu'. Non e' l'interfaccia di Strategy e non ci somiglia: quella esiste gia',
     *  e' quella che l'utente conosce dal browser, e il pannello la ospitera' intera.
     *
     *  Sta dentro l'assembly invece che in un file accanto per una ragione sola: cosi'
     *  la prova non dipende da dove qualcuno ha messo una cartella. Quando arrivera' la
     *  pagina vera, cambiera' la sola riga che oggi fa NavigateToString.
     *
     *  QUI DENTRO NON C'E' UN INDIRIZZO, e non e' una dimenticanza. La pagina non sa
     *  dove sia il motore e non deve saperlo: manda un messaggio all'ospite. Il giorno
     *  in cui ci sara' un'identita' da spendere, il segreto restera' nel C#, dove chi
     *  apre gli strumenti di sviluppo non lo trova.
     */
    internal static class Pagina {

        internal const string Prova = """
<!doctype html>
<meta charset="utf-8">
<title>AstroImage Strategy — prova del ponte</title>
<style>
  :root { color-scheme: dark; }
  body { margin:0; padding:20px; background:#15181d; color:#dfe4ea;
         font:14px/1.55 "Segoe UI", system-ui, sans-serif; }
  h1 { font-size:15px; font-weight:600; margin:0 0 2px; letter-spacing:.02em; }
  .sotto { opacity:.6; font-size:12.5px; margin-bottom:18px; }
  .riga { display:flex; gap:10px; align-items:center; flex-wrap:wrap; margin-bottom:16px; }
  button { background:#2b6cb0; color:#fff; border:0; border-radius:5px;
           padding:7px 15px; font-size:13px; cursor:pointer; }
  button:hover { background:#3182ce; }
  button:disabled { background:#3a4048; color:#8d949c; cursor:default; }
  input { background:#1d2127; color:#dfe4ea; border:1px solid #333a43; border-radius:5px;
          padding:6px 9px; font-size:13px; width:150px; }
  .stato { font-size:12.5px; padding:3px 9px; border-radius:99px; background:#3a4048; }
  .stato.ok { background:#1f5132; color:#c9f2d8; }
  .stato.no { background:#5b2020; color:#f6cfcf; }
  table { border-collapse:collapse; width:100%; margin-top:6px; font-size:13px; }
  th, td { text-align:left; padding:6px 10px; border-bottom:1px solid #262b32; }
  th { font-weight:600; opacity:.65; font-size:11.5px; text-transform:uppercase;
       letter-spacing:.06em; }
  td.n { text-align:right; font-variant-numeric:tabular-nums; }
  .box { background:#1a1e24; border:1px solid #262b32; border-radius:7px;
         padding:14px 16px; margin-bottom:14px; }
  .err { border-color:#6b2b2b; background:#241a1a; }
  .fatto { border-color:#2c5c3c; background:#182219; }
  code { font:12px ui-monospace, Consolas, monospace; opacity:.8; }
  button.manda { background:#2f855a; padding:4px 11px; font-size:12.5px; }
  button.manda:hover { background:#38a169; }
  ul { margin:8px 0 0 18px; padding:0; }
  li { margin:2px 0; }
</style>

<h1>Prova del ponte</h1>
<div class="sotto">La pagina chiede all'ospite, l'ospite chiede a Strategy. Qui dentro non c'e' nessun indirizzo.</div>

<div class="riga">
  <input id="oggetto" value="ngc6888" spellcheck="false">
  <input id="data" value="2026-09-15" spellcheck="false" style="width:110px">
  <button id="vai">Chiedi una prescrizione</button>
  <span id="stato" class="stato">in attesa</span>
</div>

<div id="filtri"></div>
<div id="uscita"></div>

<script>
  const $ = i => document.getElementById(i);
  const attese = new Map();
  let contatore = 0;
  /* Quale vetro fa quale banda. La chiede l'ospite al file filtri.json; se resta
     vuota valgono i nomi di banda del motore, che su una ruota vera non
     combaciano quasi mai. */
  let mappaFiltri = {}, ruotaVera = [];

  /* L'unica via verso il mondo: un messaggio all'ospite. */
  function chiedi(azione, corpo, extra) {
    const id = 'r' + (++contatore);
    return new Promise(risolvi => {
      attese.set(id, risolvi);
      window.chrome.webview.postMessage(JSON.stringify(
        Object.assign({ id, azione, corpo }, extra || {})));
    });
  }
  window.chrome.webview.addEventListener('message', ev => {
    let r; try { r = JSON.parse(ev.data); } catch (e) { return; }
    const f = attese.get(r.id); if (!f) return;
    attese.delete(r.id); f(r);
  });

  const stato = (t, c) => { const s = $('stato'); s.textContent = t; s.className = 'stato ' + (c || ''); };
  const esc = s => String(s).replace(/[&<>]/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;' }[c]));

  async function vai() {
    $('vai').disabled = true;
    stato('sto chiedendo…');
    $('uscita').innerHTML = '';

    const r = await chiedi('prescrizione', {
      sito:      { lat: 45.9, lon: 10.2, sqm: 20.8, seeing: 1.6, rms: 0.6,
                   horizonMin: 20, clearFrac: 0.35 },
      banco:     { tel: 'askar71f', red: 0.75, cam: 'asi2600mc', mnt: 'am5', bin: 1 },
      bersaglio: { id: $('oggetto').value.trim() },
      quando:    { data: $('data').value.trim(), notti: 3 },
      opzioni:   { strategia: 'equilibrio', pannelli: 1, filterNames: mappaFiltri }
    });

    $('vai').disabled = false;

    if (!r.ok) {
      stato('non riuscita', 'no');
      $('uscita').innerHTML = '<div class="box err"><b>' + esc(r.codice || 'errore') +
        '</b><div style="margin-top:6px;opacity:.8">' + esc(r.messaggio || '') + '</div></div>';
      return;
    }

    stato('risposta ricevuta', 'ok');

    /* `corpo` e' la risposta del servizio parola per parola: la si legge qui, e il
       fatto che si legga e' la prova che ha attraversato il ponte intatta. */
    const d = JSON.parse(r.corpo);
    const p = d.prodotto;

    /* L'IDENTIFICATIVO VIAGGIA CON LA RIGA, non in una variabile a parte.
       Sembra un dettaglio ed e' la differenza fra una guardia che funziona e una che
       non puo' scattare: se il tasto leggesse l'ultimo identificativo ricevuto, un
       tasto rimasto in giro da una richiesta precedente manderebbe l'oggetto NUOVO
       con l'aria di mandare il vecchio — e il ponte non avrebbe modo di accorgersene,
       perche' l'identificativo che riceve sarebbe quello giusto. Provato: succedeva. */
    let righe = '';
    for (const s of p.sequenze) {
      const m = s.modello;
      const pose = m.blocchi.reduce((a, b) => a + (b.n || 0), 0);
      const ore = m.blocchi.reduce((a, b) => a + (b.sec || 0) * (b.n || 0), 0) / 3600;
      const tasto = r.consegnabile
        ? '<button class="manda" data-notte="' + s.notte +
          '" data-prescrizione="' + esc(r.prescrizione || '') + '">Manda a N.I.N.A.</button>'
        : '<span style="opacity:.45">—</span>';
      righe += '<tr><td class="n">' + s.notte + '</td>' +
               '<td>' + esc((m.quando && m.quando.data) || '—') + '</td>' +
               '<td class="n">' + m.blocchi.length + '</td>' +
               '<td class="n">' + pose + '</td>' +
               '<td class="n">' + ore.toFixed(2) + ' h</td>' +
               '<td>' + esc(m.blocchi.map(b => b.filtro || b.canale || '—').join(' · ')) + '</td>' +
               '<td>' + tasto + '</td></tr>';
    }

    $('uscita').innerHTML =
      '<div class="box"><table>' +
      '<tr><th>oggetto</th><td>' + esc((p.bersaglio.nomi || [p.bersaglio.id])[0]) +
        ' <span style="opacity:.55">(' + esc(p.bersaglio.via) + ')</span></td></tr>' +
      '<tr><th>notte</th><td>chiesta ' + esc(p.notte.chiesta) + ', usata ' + esc(p.notte.usata) +
        (p.notte.spostataDi ? ' <span style="opacity:.55">spostata di ' + p.notte.spostataDi + '</span>' : '') +
        '</td></tr>' +
      '<tr><th>ore utili</th><td>' + p.notte.oreDisponibili.toFixed(2) + ' h</td></tr>' +
      '<tr><th>contratto</th><td><code>' + esc(d.contratto) + '</code> · motore ' +
        (d.misura ? d.misura.ms + ' ms' : '—') + ' · risposta ' +
        (r.corpo.length / 1024).toFixed(0) + ' KB intatti</td></tr>' +
      '</table></div>' +
      '<div class="box"><table>' +
      '<tr><th>notte</th><th>data</th><th>blocchi</th><th>pose</th><th>durata</th><th>filtri</th><th></th></tr>' +
      righe + '</table></div>' +
      (r.consegnabile ? '' :
        '<div class="box err"><b>Non si puo\x27 mandare a N.I.N.A.</b><div style="margin-top:6px;opacity:.85">' +
        esc(r.perche || 'motivo non dichiarato') + '</div></div>') +
      '<div id="consegna"></div>';

    for (const b of document.querySelectorAll('button.manda'))
      b.addEventListener('click', () => manda(b));
  }

  /* CONSEGNARE. Alla richiesta va solo il numero della notte e l'identificativo:
     la sequenza ce l'ha gia' il ponte, e non deve tornare indietro da qui. */
  async function manda(tasto) {
    const notte = Number(tasto.dataset.notte);
    for (const b of document.querySelectorAll('button.manda')) b.disabled = true;
    tasto.textContent = 'sto mandando…';

    const r = await chiedi('manda', null,
      { prescrizione: tasto.dataset.prescrizione || null, notte });

    for (const b of document.querySelectorAll('button.manda')) b.disabled = false;
    tasto.textContent = 'Manda a N.I.N.A.';

    const u = $('consegna');
    if (!r.ok) {
      u.innerHTML = '<div class="box err"><b>' + esc(r.codice || 'errore') + '</b>' +
        '<div style="margin-top:6px;opacity:.85">' + esc(r.messaggio || '') + '</div></div>';
      return;
    }

    /* Le cose scartate si vedono. Un blocco che non si e' costruito, scoperto sotto
       il cielo, e' una banda che manca e una notte persa. */
    const elenco = (t, a) => (a && a.length)
      ? '<div style="margin-top:10px;opacity:.85"><b>' + t + '</b><ul>' +
        a.map(x => '<li>' + esc(x) + '</li>').join('') + '</ul></div>' : '';

    u.innerHTML = '<div class="box fatto">' +
      '<b>Notte ' + notte + ' aggiunta al Sequenziatore Avanzato.</b>' +
      '<div style="margin-top:6px;opacity:.85">' + esc(r.bersaglio || '') + ' — ' +
      r.blocchi + ' blocchi, ' + r.pose + ' pose. ' +
      '<span style="opacity:.7">Non e\' stato avviato niente: a premere Riproduci sei tu.</span></div>' +
      elenco('Scartato:', r.scartati) + elenco('Da sapere:', r.note) + '</div>';
  }

  $('vai').addEventListener('click', vai);

  chiedi('salute').then(r => stato(r.ok ? 'servizio raggiungibile' : 'servizio non raggiungibile',
                                  r.ok ? 'ok' : 'no'));

  /* LA RUOTA VERA SI MOSTRA, e non per cortesia: senza sapere che vetri hai in
     ruota nessuno puo' scrivere la mappa che li lega alle bande. */
  chiedi('filtri').then(r => {
    if (!r.ok) return;
    mappaFiltri = r.mappa || {};
    ruotaVera = r.ruota || [];
    const voci = Object.entries(mappaFiltri);
    const righe = voci.length
      ? voci.map(([c, v]) => esc(c) + ' &rarr; ' + esc(v)).join(' &nbsp;·&nbsp; ')
      : '<b>nessuna</b> — il motore usera\' i suoi nomi di banda, che su una ruota vera ' +
        'non combaciano quasi mai. Scrivi <code>filtri.json</code> accanto al DLL.';
    $('filtri').innerHTML =
      '<div class="box"><table>' +
      '<tr><th>in ruota</th><td>' +
        (ruotaVera.length ? ruotaVera.map(esc).join(' · ') : '<i>nessun filtro</i>') + '</td></tr>' +
      '<tr><th>mappa</th><td>' + righe + '</td></tr>' +
      (r.nota ? '<tr><th></th><td style="opacity:.7">' + esc(r.nota) + '</td></tr>' : '') +
      '</table></div>';
  });
</script>
""";
    }
}
