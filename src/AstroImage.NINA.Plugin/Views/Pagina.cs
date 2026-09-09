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

<div id="sito"></div>
<div id="filtri"></div>
<div id="uscita"></div>

<script>
  const $ = i => document.getElementById(i);
  const attese = new Map();
  let contatore = 0;
  /* Quale vetro fa quale banda. La chiede l'ospite al file filtri.json; se resta
     vuota valgono i nomi di banda del motore, che su una ruota vera non
     combaciano quasi mai. */
  let righeRuota = [], catalogo = [], catalogoOk = false, diSerie = [];
  /* Dove sei. La geometria viene dal profilo di N.I.N.A.; il resto da uno strumento
     se c'e', dalla dichiarazione se no. Qui dentro non c'e' nessun numero di serie:
     fino a ieri ce ne erano sette, ed erano Borno. */
  let sito = null, sitoScritto = {}, sitoProv = {}, sitoManca = null;

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
      /* IL SITO E' QUELLO DEL PROFILO, non piu' sette numeri scritti qui dentro.
         Se manca qualcosa manca davvero: nessun ripiego, nessun valore di serie. */
      sito:      sito || {},
      /* IL BANCO NON VIENE ANCORA DAL PROFILO, ed e' scritto qui apposta finche' non
         verra'. N.I.N.A. sa focale, rapporto focale e passo del pixel, ma il motore ha
         bisogno di apertura, ostruzione, trasmissione, QE, rumore di lettura e pozzo:
         cose che N.I.N.A. non possiede affatto. Dedurre «askar71f» dal nome di un
         dispositivo sarebbe indovinare l'identita' fisica da un'etichetta — lo stesso
         difetto dei filtri, ripetuto sull'ottica. Serve una dichiarazione, come per la
         ruota, e finche' non c'e' la pagina lo dice invece di far finta. */
      banco:     { tel: 'askar71f', red: 0.75, cam: 'asi2600mc', mnt: 'am5', bin: 1 },
      bersaglio: { id: $('oggetto').value.trim() },
      quando:    { data: $('data').value.trim(), notti: 3 },
      opzioni:   { strategia: 'equilibrio', pannelli: 1 }
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
               /*  IL NOME CHE VEDRAI IN SEQUENZA, non l'etichetta di banda del motore.
                  «HO · L» erano i nomi dei CANALI, e leggerli accanto al tasto che
                  consegna faceva credere che quelli sarebbero finiti nel Sequenziatore.
                  Il vetro vero e' in `posa.<canale>.ex.spec.filter.id`, e la
                  dichiarazione dice come si chiama sulla tua ruota. */
               '<td>' + esc(m.blocchi.map(b => vetroDelBlocco(p, b)).join(' · ')) + '</td>' +
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
      /*  SU QUALI VETRI E' STATA CALCOLATA, e non e' un dettaglio da nascondere.
         Senza questa riga «non e' cambiato niente perche' il motore avrebbe scelto
         gli stessi vetri» e «non e' cambiato niente perche' la ruota non e' partita»
         si somigliano troppo — e la prima volta ci ha fregati per mezz'ora. */
      '<tr><th>calcolata su</th><td>' +
        (r.ruotaAggiunta && r.ruotaAggiunta.length
          ? '<b>la tua ruota</b> — ' + r.ruotaAggiunta.map(esc).join(' · ')
          : '<b style="color:#e0a030">i filtri di serie del motore</b>, non i tuoi: ' +
            'dichiara i vetri qui sopra e richiedi.') +
        '</td></tr>' +
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

    /*  L'AVVISO SUI DOPPIONI, e conta solo gli OMONIMI.
       Mettere due bersagli DIVERSI nella stessa notte e' normale e non riguarda
       questo avviso: il nome che il ponte genera porta dentro oggetto, notte e data
       — «NGC 6888 — notte 1 — 2026-09-15» — quindi due target diversi hanno nomi
       diversi, e lo stesso oggetto su notte 1 e notte 2 pure. Scatta solo quando in
       sequenza c'e' gia' ESATTAMENTE la stessa cosa, che e' il caso di chi richiede
       la prescrizione dopo aver cambiato qualcosa e riconsegna.
       Il ponte non sovrascrive e non deve: quella sequenza e' di chi riprende. Ma
       tacere che adesso ce ne sono due e' costato, perche' due bersagli identici,
       se si preme Riproduci, si riprendono ENTRAMBI. */
    const doppioni = (r.omonimiPrima > 0)
      ? '<div style="margin-top:8px;color:#e0a030">&#9888; Nel Sequenziatore c\'era gi&agrave; ' +
        (r.omonimiPrima === 1 ? 'un bersaglio' : r.omonimiPrima + ' bersagli') +
        ' con questo <b>stesso nome</b>: adesso ce ne ' +
        (r.omonimiPrima === 1 ? 'sono due' : 'sono ' + (r.omonimiPrima + 1)) +
        '. Il ponte aggiunge e non cancella mai quello che c\'&egrave;: se lasci i ' +
        'vecchi, N.I.N.A. li riprender&agrave; tutti.</div>'
      : '';

    u.innerHTML = '<div class="box fatto">' +
      '<b>Notte ' + notte + ' aggiunta al Sequenziatore Avanzato.</b>' +
      '<div style="margin-top:6px;opacity:.85">' + esc(r.bersaglio || '') + ' — ' +
      r.blocchi + ' blocchi, ' + r.pose + ' pose. ' +
      '<span style="opacity:.7">Non e\' stato avviato niente: a premere Riproduci sei tu.</span></div>' +
      doppioni +
      elenco('Scartato:', r.scartati) + elenco('Da sapere:', r.note) + '</div>';
  }

  $('vai').addEventListener('click', vai);

  chiedi('salute').then(r => stato(r.ok ? 'servizio raggiungibile' : 'servizio non raggiungibile',
                                  r.ok ? 'ok' : 'no'));

  /* LA RUOTA VIRTUALE: che cosa sono, fisicamente, i vetri che hai in ruota.
     N.I.N.A. da' i nomi e gli slot; il motore da' l'elenco dei vetri che conosce;
     in mezzo ci sei tu, che dichiari quale e' quale. Nessuna regola puo' indovinarlo:
     «HA» puo' stare davanti a un L-Ultimate, e solo chi l'ha comprato lo sa. */
  /* Quale vetro finira' davvero in sequenza per questo blocco: lo dice il motore in
     `posa.<canale>.ex.spec.filter.id`, e la dichiarazione lo traduce nel nome che hai
     scritto tu sulla ruota. Se non e' dichiarato lo si dice qui, invece di lasciare
     credere che andra' bene: e' lo stesso rifiuto che poi farebbe la consegna. */
  function vetroDelBlocco(p, b) {
    const canali = b.canali || [];
    let id = null;
    for (const c of canali) {
      const s = p.posa && p.posa[c] && p.posa[c].ex && p.posa[c].ex.spec && p.posa[c].ex.spec.filter;
      if (!s || !s.id) continue;
      if (id && id !== s.id) return '?? vetri discordi';
      id = s.id;
    }
    if (!id) return b.filtro || canali.join('+') || '—';
    if (id === '__none') return 'nessun filtro (Bayer)';
    const v = righeRuota.find(x => x.id === id);
    return v ? v.nina : id + ' — non dichiarato';
  }

  const num = v => (v === null || v === undefined) ? '—' : v;

  function disegnaSito(r) {
    sito = r.sito || null;
    sitoScritto = r.dichiarato || {};
    sitoProv = r.provenienza || {};
    sitoManca = r.manca || null;

    /* La provenienza si vede accanto al numero: un SQM misurato e uno scritto a mano
       valgono lo stesso per il motore, ma non per chi guarda. */
    const riga = (etichetta, campo, unita, scrivibile) => {
      const v = sito ? sito[campo] : null;
      const p = sitoProv[campo] || 'non disponibile';
      const colore = p.indexOf('non disponibile') === 0 ? 'color:#e0a030'
                   : p.indexOf('dichiarato') === 0 ? 'opacity:.75' : 'opacity:.6';
      return '<tr><th>' + etichetta + '</th><td>' +
        (scrivibile
          ? '<input data-sito="' + campo + '" value="' + (sitoScritto[campo] === null ||
              sitoScritto[campo] === undefined ? '' : sitoScritto[campo]) +
            '" style="width:70px" spellcheck="false"> ' +
            (v === null || v === undefined ? '' : '<b>' + num(v) + '</b> ' + unita)
          : '<b>' + num(v) + '</b> ' + unita) +
        ' <span style="font-size:12px;' + colore + '">' + esc(p) + '</span></td></tr>';
    };

    $('sito').innerHTML =
      '<div class="box">' +
      '<b>Dove stai riprendendo</b>' +
      (r.nome ? ' — ' + esc(r.nome) : '') +
      (r.perCheNo ? '<div style="margin:.6em 0;opacity:.85">&#9888; ' + esc(r.perCheNo) + '</div>' : '') +
      (r.nota ? '<div style="margin:.6em 0;opacity:.85">&#9888; ' + esc(r.nota) + '</div>' : '') +
      (sitoManca ? '<div style="margin:.6em 0;color:#e0a030">&#9888; ' + esc(sitoManca) +
        ' Scrivilo qui sotto: senza, il motore calcola le ore ma non la posa.</div>' : '') +
      '<table style="width:100%">' +
      riga('latitudine', 'lat', '&deg;', false) +
      riga('longitudine', 'lon', '&deg;', false) +
      riga('cielo (SQM)', 'sqm', 'mag/arcsec&sup2;', true) +
      riga('seeing', 'seeing', '&Prime;', true) +
      riga('guida (RMS)', 'rms', '&Prime;', true) +
      riga('altezza minima', 'horizonMin', '&deg;', true) +
      riga('notti serene', 'clearFrac', '', true) +
      '</table>' +
      '<div style="margin-top:.7em">' +
        '<button id="salvaSito">Salva i dati del sito</button> ' +
        '<span id="esitoSito" style="margin-left:.6em;opacity:.8"></span>' +
      '</div>' +
      '<div style="margin-top:.7em;opacity:.7;font-size:12.5px">' +
        'Latitudine e longitudine vengono dal profilo di N.I.N.A. e non si scrivono qui. ' +
        'L&rsquo;altezza minima resta dichiarata: N.I.N.A. ha un orizzonte per azimut, ' +
        'il motore un numero solo, e ridurre l&rsquo;uno all&rsquo;altro sceglierebbe di ' +
        'nascosto quale met&agrave; del cielo buttare via.' +
      '</div>' +
      '<div style="margin-top:.5em;opacity:.7;font-size:12.5px">' +
        '&#9888; La <b>strumentazione</b> &egrave; ancora una configurazione Strategy ' +
        'scritta nel ponte, <b>non</b> letta dal profilo di N.I.N.A.: Askar 71F 0.75&times; ' +
        'con ASI2600MC. N.I.N.A. non possiede apertura, ostruzione, trasmissione, QE n&eacute; ' +
        'rumore di lettura, quindi il collegamento richieder&agrave; una dichiarazione come ' +
        'quella dei filtri.' +
      '</div></div>';

    Array.prototype.forEach.call(document.querySelectorAll('#sito input'), i => {
      i.addEventListener('input', () => { $('esitoSito').textContent = 'non salvato'; });
    });
    const b = $('salvaSito');
    if (b) b.addEventListener('click', () => {
      const s = {};
      Array.prototype.forEach.call(document.querySelectorAll('#sito input'), i => {
        const v = i.value.trim().replace(',', '.');
        s[i.getAttribute('data-sito')] = v === '' ? null : Number(v);
      });
      $('esitoSito').textContent = 'salvo…';
      chiedi('salvaSito', { sito: s }).then(r2 => {
        $('esitoSito').textContent = r2.ok ? 'salvato' : 'NON salvato: ' + (r2.messaggio || r2.codice || '');
        if (r2.ok) chiedi('sito').then(disegnaSito);
      });
    });
  }

  function disegnaRuota(r) {
    righeRuota = r.righe || [];
    catalogo = r.catalogo || [];
    catalogoOk = !!r.catalogoDisponibile;
    diSerie = r.diSerie || [];

    const avvisi = [];
    if (r.ruotaVuota) avvisi.push(esc(r.ruotaVuota));
    if (r.nota) avvisi.push(esc(r.nota));
    if (!catalogoOk) avvisi.push('Il motore non ha risposto: senza il suo elenco non si ' +
      'puo\' dichiarare niente. Accendi il servizio e ricarica.');
    if (!r.dichiarati) avvisi.push('<b>Nessun vetro dichiarato.</b> Finche\' e\' cosi\', ' +
      'il motore calcola sui suoi filtri di serie (' + diSerie.map(esc).join(', ') +
      '), non sui tuoi: la prescrizione che leggi non e\' fatta sul tuo equipaggiamento.');

    /* «nessun filtro» e' una scelta legittima, non un'assenza: su una camera a
       matrice il motore puo' dire che davanti non ci va niente. Chi ha uno slot
       vuoto o un vetro trasparente lo dichiara qui, e la consegna sa che slot
       chiedere invece di rifiutare. */
    const opzioni = (scelto) => '<option value="">(non dichiarato)</option>' +
      '<option value="__none"' + (scelto === '__none' ? ' selected' : '') +
        '>nessun filtro / vetro trasparente</option>' +
      catalogo.map(v => '<option value="' + esc(v.id) + '"' +
        (v.id === scelto ? ' selected' : '') + '>' + esc(v.nome) +
        (v.fwhm_nm ? ' — ' + v.fwhm_nm + ' nm' : '') +
        (v.bande && v.bande.length > 1 ? ' [' + v.bande.map(esc).join('+') + ']' : '') +
        '</option>').join('');

    const stati = { mappato: '&#10003;', nonmappato: '&mdash;', ignoto: '?', orfano: '!' };
    const spiega = {
      mappato: 'dichiarato', nonmappato: 'da dichiarare',
      ignoto: 'dichiarato, ma il motore non conosce questo identificativo',
      orfano: 'non e\' piu\' in ruota: rinominato o tolto. Ripuntalo o lascialo perdere.'
    };

    $('filtri').innerHTML =
      '<div class="box">' +
      '<b>Configurazione dei filtri</b> — che cosa sono, fisicamente, i vetri della tua ruota. ' +
      'Si dichiara una volta per profilo.' +
      (avvisi.length ? '<div style="margin:.6em 0;opacity:.85">' +
        avvisi.map(a => '<div>&#9888; ' + a + '</div>').join('') + '</div>' : '') +
      '<table style="width:100%"><tr>' +
        '<th>slot</th><th>nome in N.I.N.A.</th><th>e\' questo vetro</th><th></th></tr>' +
      righeRuota.map((x, i) =>
        '<tr' + (x.stato === 'orfano' ? ' style="opacity:.6"' : '') + '>' +
        '<td>' + (x.slot === null || x.slot === undefined ? '&mdash;' : x.slot) + '</td>' +
        '<td>' + esc(x.nina) +
          (x.ambiguo ? ' <span title="due slot hanno questo stesso nome: N.I.N.A. ' +
            'risolve per nome e prende il primo">&#9888;</span>' : '') + '</td>' +
        '<td><select data-riga="' + i + '"' + (catalogoOk ? '' : ' disabled') + '>' +
          opzioni(x.id) + '</select>' +
          (x.adatto === null || x.adatto === undefined
            ? '' : '') +
          (x.stato === 'mappato' && x.adatto !== true && r.cameraAMatrice !== null &&
           r.cameraAMatrice !== undefined
            ? ' <span style="opacity:.7" title="il catalogo non dichiara questo vetro ' +
              'per la camera di questo profilo — non e\' un divieto, e\' un dubbio">&#9888;</span>' : '') +
        '</td>' +
        '<td title="' + esc(spiega[x.stato] || '') + '">' + (stati[x.stato] || '') + '</td>' +
        '</tr>').join('') +
      '</table>' +
      '<div style="margin-top:.7em">' +
        '<button id="salvaFiltri"' + (catalogoOk ? '' : ' disabled') + '>Salva configurazione</button> ' +
        '<span id="esitoFiltri" style="margin-left:.6em;opacity:.8"></span>' +
      '</div></div>';

    Array.prototype.forEach.call(document.querySelectorAll('#filtri select'), s => {
      s.addEventListener('change', () => {
        righeRuota[+s.getAttribute('data-riga')].id = s.value || null;
        $('esitoFiltri').textContent = 'non salvato';
      });
    });
    const b = $('salvaFiltri');
    if (b) b.addEventListener('click', () => {
      $('esitoFiltri').textContent = 'salvo…';
      chiedi('salvaFiltri', { vetri: righeRuota.map(x => ({ nina: x.nina, id: x.id, nota: x.nota })) })
        .then(r2 => {
          $('esitoFiltri').textContent = r2.ok
            ? (r2.dichiarati || 0) + ' vetri dichiarati'
            : 'NON salvato: ' + (r2.messaggio || r2.codice || '');
          if (r2.ok) chiedi('filtri').then(disegnaRuota);
        });
    });
  }

  chiedi('sito').then(r => { if (r.ok) disegnaSito(r); });
  chiedi('filtri').then(r => { if (r.ok) disegnaRuota(r); });
</script>
""";
    }
}
