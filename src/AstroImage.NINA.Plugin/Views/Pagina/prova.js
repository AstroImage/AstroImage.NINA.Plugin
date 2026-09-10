  const $ = i => document.getElementById(i);
  const attese = new Map();
  let contatore = 0;
  /* Quale vetro fa quale banda. La chiede l'ospite al file filtri.json; se resta
     vuota valgono i nomi di banda del motore, che su una ruota vera non
     combaciano quasi mai. */
  let righeRuota = [], catalogo = [], catalogoOk = false, diSerie = [];
  /* Quale posizione si sta guardando. Vive qui e non dentro disegnaRuota perche'
     deve sopravvivere a un salvataggio e a un cambio di lingua: sarebbe seccante
     tornare ogni volta allo slot 0 dopo aver dichiarato il settimo. */
  let slotScelto = 0;
  /* Dove sei. La geometria viene dal profilo di N.I.N.A.; il resto da uno strumento
     se c'e', dalla dichiarazione se no. Qui dentro non c'e' nessun numero di serie:
     fino a ieri ce ne erano sette, ed erano Borno. */
  let sito = null, sitoScritto = {}, sitoProv = {}, sitoManca = null;

  /*  I TRE MODI DI RIPRESA — e questa pagina non sa che cosa siano.
   *
   *  Sa che esistono tre identificativi, che uno e' selezionato, e che quello
   *  selezionato va nella richiesta. Che cosa comportino — quanto lunga la posa,
   *  quale limite la lega, quanto costa la scelta — lo decide Strategy, e il ponte
   *  non ne conserva nemmeno una soglia. Se qui comparisse un numero di secondi
   *  sarebbe gia' una seconda verita' accanto a quella del motore.
   *
   *  L'ELENCO SI SCOPRE, non si scrive: arriva da GET /v1/salute attraverso il C#.
   *  Se il motore ne aggiungesse un quarto comparirebbe da solo, con l'etichetta e
   *  la spiegazione che il motore dichiara — in italiano — finche' qualcuno non
   *  scrive la traduzione. Si degrada, non si rompe: e' la stessa regola dei codici
   *  d'errore.
   *
   *  Il predefinito lo dichiara il servizio. Se non risponde si resta senza modi
   *  visibili e la richiesta non porta la chiave: e' il servizio a scegliere, come
   *  ha sempre fatto — non questa pagina al posto suo. */
  let modi = [], modoScelto = null;

  /*  Le icone: tre segni, nessun colore proprio. Prendono il colore dal testo e
   *  diventano accento quando la card e' scelta — come in AIS, dove i tre modi
   *  sono tre facce della stessa decisione e non tre prodotti. */
  const PAROLE = {
    resa:       { nome: 'Pag_Modo_resa',       nota: 'Pag_ModoNota_resa' },
    equilibrio: { nome: 'Pag_Modo_equilibrio', nota: 'Pag_ModoNota_equilibrio' },
    dinamica:   { nome: 'Pag_Modo_dinamica',   nota: 'Pag_ModoNota_dinamica' },
  };
  const ICONE = {
    resa:       '<path d="M4 19h16"/><path d="M6 16V9"/><path d="M11 16V5"/><path d="M16 16v-4"/>',
    equilibrio: '<path d="M12 4v16"/><path d="M5 8h14"/><path d="M5 8 2 15h6Z"/><path d="M19 8l-3 7h6Z"/>',
    dinamica:   '<circle cx="12" cy="12" r="8"/><circle cx="12" cy="12" r="2.5"/>',
  };

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
    /* L'OSPITE PUO' PARLARE PER PRIMO, e finora non lo faceva mai: ogni messaggio era
       la risposta a una domanda. Il cambio lingua e' l'unica cosa che succede senza che
       la pagina l'abbia chiesta, e senza questo ramo resterebbe scritta nella lingua di
       prima finche' non la si chiude e riapre — che a chi gira l'interruttore sembra un
       plugin rotto, non un limite. */
    if (r && r.evento === 'lingua') {
      if (r.voci) { window.__LOC__ = r.voci; }
      applicaVoci();
      ridisegna();
      return;
    }
    const f = attese.get(r.id); if (!f) return;
    attese.delete(r.id); f(r);
  });

  /* SI RICHIEDE, NON SI TRADUCE QUI. Le frasi che cambiano lingua nascono nel C#, e
     l'unico modo di riaverle nella lingua nuova e' chiederle di nuovo. Sito e filtri
     si possono richiedere quanto si vuole: sono letture.
     La prescrizione NO, e non e' una dimenticanza. Chiederla di nuovo vorrebbe dire
     una chiamata al motore e forse numeri diversi, per aver girato un interruttore
     della lingua. Cio' che e' gia' sullo schermo e' il resoconto di una cosa
     avvenuta: resta nella lingua in cui e' avvenuta, e la prossima esce nell'altra. */
  function ridisegna() {
    chiedi('modalita').then(r => {
      if (!r.ok || !r.modalita || !r.modalita.length) return;
      modi = r.modalita;
      modoScelto = r.diSerie && modi.some(m => m.id === r.diSerie) ? r.diSerie : modi[0].id;
      disegnaModi();
    });
    chiedi('sito').then(r => { if (r.ok) disegnaSito(r); });
    chiedi('filtri').then(r => { if (r.ok) disegnaRuota(r); });
    /*  I modi si ridisegnano soltanto: l'elenco e la scelta restano quelli, cambia
     *  la lingua delle parole. Richiederli al servizio sarebbe una chiamata in piu'
     *  per girare un interruttore. */
    if (modi.length) disegnaModi();
  }

  const stato = (t, c) => { const s = $('stato'); s.textContent = t; s.className = 'stato ' + (c || ''); };
  /*  Le virgolette si proteggono come gli angoli, e non e' pedanteria: `esc` finisce
   *  dentro un attributo ventisei volte in questo file, e li' una virgoletta nel
   *  testo chiuderebbe l'attributo e trasformerebbe il resto in markup. Nel testo
   *  normale &quot; si vede come una virgoletta, quindi non costa niente. */
  const esc = s => String(s).replace(/[&<>"]/g,
    c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;' }[c]));

  /* ── LE PAROLE NON STANNO QUI DENTRO ──────────────────────────────────────────
     Le manda l'ospite in window.__LOC__, gia' nella lingua che vale adesso, e le
     prende dagli stessi due file di risorse da cui vengono i messaggi del C#: una
     fonte sola, e le prove che verificano chiavi e segnaposto valgono anche per
     questa pagina.

     Se una chiave manca si vede il suo NOME, mai un buco: un'etichetta sparita e' un
     difetto che si scopre sotto il cielo, un nome di chiave in mezzo alla pagina si
     nota subito.

     T  — il testo cosi' com'e', per textContent e per i title: li' non serve
          proteggere niente, ci pensa il browser.
     MF — per l'HTML: prima si protegge il testo, poi *cosi'* diventa grassetto, e
          solo alla fine entrano i valori. In quest'ordine, perche' i valori arrivano
          gia' protetti da chi chiama e proteggerli due volte si vedrebbe.

     L'enfasi con gli asterischi e' l'unica marcatura ammessa dentro un testo, ed
     esiste per non dover spezzare una frase in tre chiavi solo perche' una parola va
     in grassetto. Il testo resta testo; come si vede lo decide la pagina. */
  /*  Con un valore di RISERVA, e serve a una cosa sola: un identificativo che il
   *  ponte non conosce ancora — un modo nuovo dichiarato dal motore — deve
   *  comparire con le parole che il motore manda, in italiano, invece di sparire
   *  o di mostrare il nome della chiave. Senza riserva si comporta come prima. */
  const T = (k, riserva) => { const v = (window.__LOC__ || {})[k];
    return v === undefined ? (riserva !== undefined ? riserva : k) : v; };
  const M = t => esc(t).replace(/\*([^*]+)\*/g, '<strong>$1</strong>');
  const MF = (k, ...a) => M(T(k)).replace(/\{(\d+)\}/g, (m, i) => a[+i] === undefined ? m : a[+i]);

  /* Riempie tutto quello che nel markup porta una chiave. Si richiama a ogni cambio
     di lingua: le etichette fisse non passano da un ridisegno. */
  function applicaVoci() {
    for (const el of document.querySelectorAll('[data-loc]'))
      el.innerHTML = M(T(el.getAttribute('data-loc')));
  }

  /*  UNA CARD PER MODO. Il testo tradotto vince quando c'e' — `Pag_Modo_<id>` e
   *  `Pag_ModoNota_<id>` — e si ripiega su quello che il motore dichiara quando non
   *  c'e': un modo nuovo si vede subito, in italiano, invece di sparire. */
  function disegnaModi() {
    if (!modi.length) { $('modi').innerHTML = ''; return; }
    const titolo = T('Pag_ComeRiprendere');
    /*  UN RADIOGROUP VERO, non tre pulsanti che si somigliano. E' la stessa forma
     *  di AIS: una <label> per modo con dentro un <input type=radio> nascosto. Si
     *  arriva col tab, si cambia con le frecce, e il browser garantisce da solo che
     *  ne resti scelto uno — cosa che tre pulsanti con aria-pressed non fanno.    */
    $('modi').innerHTML =
      '<fieldset class="goalbox"><legend class="hc-k">' + esc(titolo) + '</legend>' +
      '<div class="goalgrid" role="radiogroup" aria-label="' + esc(titolo) + '">' +
      modi.map(m => {
        /*  LE CHIAVI SONO LETTERALI, non composte. Comporle — `'Pag_Modo_' + id` —
         *  le rendeva invisibili alla prova che cerca le voci orfane nei resx, e una
         *  voce che nessuna prova vede e' una voce che un giorno sparisce.
         *  Questo NON e' l'elenco dei modi: l'elenco arriva dal servizio. Questa e'
         *  la traduzione per gli identificativi che conosciamo, con ripiego su quelli
         *  che non conosciamo — la stessa forma di `MessaggioDelMotore`. */
        const p = PAROLE[m.id];
        const nome = p ? T(p.nome, m.etichetta || m.id) : (m.etichetta || m.id);
        const nota = p ? T(p.nota, m.spiegazione || '') : (m.spiegazione || '');
        return '<label class="goalcard"><input type="radio" name="modo" value="' +
            esc(m.id) + '"' + (m.id === modoScelto ? ' checked' : '') + '>' +
          '<span class="gc"><span class="gc-h">' +
            '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" ' +
              'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
              (ICONE[m.id] || ICONE.resa) + '</svg>' +
            '<b>' + esc(nome) + '</b>' +
            '<span class="gc-tick" aria-hidden="true">&#10003;</span></span>' +
          '<span class="gc-d">' + esc(nota) + '</span></span></label>';
      }).join('') + '</div></fieldset>';

    Array.prototype.forEach.call($('modi').querySelectorAll('.goalcard input'), i => {
      i.addEventListener('change', () => {
        /*  Cambia solo quale identificativo partira'. Niente si ricalcola qui: la
         *  prescrizione gia' a schermo resta il resoconto di com'e' andata con il
         *  modo di allora, e la prossima uscira' con quello nuovo. E non si ridisegna
         *  niente: la spunta e il bordo li muove il foglio di stile da solo, sul
         *  :checked, e ridisegnare qui butterebbe via il fuoco della tastiera. */
        modoScelto = i.value;
      });
    });
  }

  async function vai() {
    $('vai').disabled = true;
    stato(T('Pag_StoChiedendo'));
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
      /*  L'IDENTIFICATIVO E NIENT'ALTRO. Non un numero di secondi, non una soglia:
       *  che cosa comporti questo modo lo decide Strategy. Quando l'elenco non e'
       *  arrivato la chiave non parte affatto, e il servizio applica il proprio
       *  predefinito — che e' sempre stato compito suo. */
      opzioni:   modoScelto ? { strategia: modoScelto, pannelli: 1 } : { pannelli: 1 }
    });

    $('vai').disabled = false;

    if (!r.ok) {
      stato(T('Pag_NonRiuscita'), 'no');
      $('uscita').innerHTML = '<div class="box err"><b>' + esc(r.codice || T('Pag_Errore')) +
        '</b><div style="margin-top:6px;opacity:.8">' + esc(r.messaggio || '') + '</div></div>';
      return;
    }

    stato(T('Pag_RispostaRicevuta'), 'ok');

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
          '" data-prescrizione="' + esc(r.prescrizione || '') + '">' + T('Pag_MandaANina') + '</button>'
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
      '<tr><th>' + T('Pag_ColOggetto') + '</th><td>' + esc((p.bersaglio.nomi || [p.bersaglio.id])[0]) +
        ' <span style="opacity:.55">(' + esc(p.bersaglio.via) + ')</span></td></tr>' +
      '<tr><th>' + T('Pag_ColNotte') + '</th><td>' +
        MF('Pag_NotteChiestaUsata', esc(p.notte.chiesta), esc(p.notte.usata)) +
        (p.notte.spostataDi ? ' <span style="opacity:.55">' +
          MF('Pag_NotteSpostata', p.notte.spostataDi) + '</span>' : '') +
        '</td></tr>' +
      '<tr><th>' + T('Pag_RigaOreUtili') + '</th><td>' + p.notte.oreDisponibili.toFixed(2) + ' h</td></tr>' +
      /*  SU QUALI VETRI E' STATA CALCOLATA, e non e' un dettaglio da nascondere.
         Senza questa riga «non e' cambiato niente perche' il motore avrebbe scelto
         gli stessi vetri» e «non e' cambiato niente perche' la ruota non e' partita»
         si somigliano troppo — e la prima volta ci ha fregati per mezz'ora. */
      '<tr><th>' + T('Pag_RigaCalcolataSu') + '</th><td>' +
        (r.ruotaAggiunta && r.ruotaAggiunta.length
          ? MF('Pag_CalcolataTuaRuota', r.ruotaAggiunta.map(esc).join(' · '))
          : '<span style="color:#e0a030">' + MF('Pag_CalcolataDiSerie') + '</span>') +
        '</td></tr>' +
      '<tr><th>' + T('Pag_RigaContratto') + '</th><td>' +
        MF('Pag_Contratto',
           '<code>' + esc(d.contratto) + '</code>',
           esc(d.misura ? d.misura.ms + ' ms' : '—'),
           (r.corpo.length / 1024).toFixed(0)) + '</td></tr>' +
      '</table></div>' +
      '<div class="box"><table>' +
      '<tr><th>' + T('Pag_ColNotte') + '</th><th>' + T('Pag_ColData') + '</th><th>' +
        T('Pag_ColBlocchi') + '</th><th>' + T('Pag_ColPose') + '</th><th>' +
        T('Pag_ColDurata') + '</th><th>' + T('Pag_ColFiltri') + '</th><th></th></tr>' +
      righe + '</table></div>' +
      (r.consegnabile ? '' :
        '<div class="box err"><b>' + T('Pag_NonSiPuoMandare') + '</b>' +
        '<div style="margin-top:6px;opacity:.85">' +
        esc(r.perche || T('Pag_MotivoNonDichiarato')) + '</div></div>') +
      '<div id="consegna"></div>';

    for (const b of document.querySelectorAll('button.manda'))
      b.addEventListener('click', () => manda(b));
  }

  /* CONSEGNARE. Alla richiesta va solo il numero della notte e l'identificativo:
     la sequenza ce l'ha gia' il ponte, e non deve tornare indietro da qui. */
  async function manda(tasto) {
    const notte = Number(tasto.dataset.notte);
    for (const b of document.querySelectorAll('button.manda')) b.disabled = true;
    tasto.textContent = T('Pag_StoMandando');

    const r = await chiedi('manda', null,
      { prescrizione: tasto.dataset.prescrizione || null, notte });

    for (const b of document.querySelectorAll('button.manda')) b.disabled = false;
    tasto.textContent = T('Pag_MandaANina');

    const u = $('consegna');
    if (!r.ok) {
      u.innerHTML = '<div class="box err"><b>' + esc(r.codice || T('Pag_Errore')) + '</b>' +
        '<div style="margin-top:6px;opacity:.85">' + esc(r.messaggio || '') + '</div></div>';
      return;
    }

    /* Le cose scartate si vedono. Un blocco che non si e' costruito, scoperto sotto
       il cielo, e' una banda che manca e una notte persa. */
    const elenco = (t, a) => (a && a.length)
      ? '<div style="margin-top:10px;opacity:.85"><b>' + t + '</b><ul>' +
        a.map(x => '<li>' + esc(x) + '</li>').join('') + '</ul></div>' : '';

    u.innerHTML = '<div class="box fatto">' +
      '<b>' + MF('Pag_NotteAggiunta', notte) + '</b>' +
      '<div style="margin-top:6px;opacity:.85">' + esc(r.bersaglio || '') + ' — ' +
      MF('Pag_BlocchiPose', r.blocchi, r.pose) + ' ' +
      '<span style="opacity:.7">' + T('Pag_NienteAvviato') + '</span></div>' +
      elenco(T('Pag_Scartato'), r.scartati) + elenco(T('Pag_DaSapere'), r.note) + '</div>';
  }

  $('vai').addEventListener('click', vai);

  chiedi('salute').then(r => stato(T(r.ok ? 'Pag_ServizioSu' : 'Pag_ServizioGiu'),
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
      if (id && id !== s.id) return T('Pag_FiltriDiscordi');
      id = s.id;
    }
    if (!id) return b.filtro || canali.join('+') || '—';
    if (id === '__none') return T('Pag_NessunFiltroBayer');
    const v = righeRuota.find(x => x.id === id);
    return v ? v.nina : T('Pag_FiltroNonDichiarato').replace('{0}', id);
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
      '<b>' + T('Pag_DoveRiprendi') + '</b>' +
      (r.nome ? ' — ' + esc(r.nome) : '') +
      (r.perCheNo ? '<div style="margin:.6em 0;opacity:.85">&#9888; ' + esc(r.perCheNo) + '</div>' : '') +
      (r.nota ? '<div style="margin:.6em 0;opacity:.85">&#9888; ' + esc(r.nota) + '</div>' : '') +
      (sitoManca ? '<div style="margin:.6em 0;color:#e0a030">&#9888; ' + esc(sitoManca) +
        ' ' + T('Pag_ScriviloQuiSotto') + '</div>' : '') +
      '<table style="width:100%">' +
      riga(T('Pag_Latitudine'), 'lat', '&deg;', false) +
      riga(T('Pag_Longitudine'), 'lon', '&deg;', false) +
      riga(T('Pag_Sqm'), 'sqm', 'mag/arcsec&sup2;', true) +
      riga(T('Pag_Seeing'), 'seeing', '&Prime;', true) +
      riga(T('Pag_Rms'), 'rms', '&Prime;', true) +
      riga(T('Pag_AltezzaMinima'), 'horizonMin', '&deg;', true) +
      riga(T('Pag_NottiSerene'), 'clearFrac', '', true) +
      '</table>' +
      '<div style="margin-top:.7em">' +
        '<button id="salvaSito">' + T('Pag_SalvaSito') + '</button> ' +
        '<span id="esitoSito" style="margin-left:.6em;opacity:.8"></span>' +
      '</div>' +
      '<div style="margin-top:.7em;opacity:.7;font-size:12.5px">' +
        MF('Pag_LatLonNota') +
      '</div>' +
      '<div style="margin-top:.5em;opacity:.7;font-size:12.5px">' +
        '&#9888; ' + MF('Pag_StrumentazioneNota') +
      '</div></div>';

    Array.prototype.forEach.call(document.querySelectorAll('#sito input'), i => {
      i.addEventListener('input', () => { $('esitoSito').textContent = T('Pag_NonSalvato'); });
    });
    const b = $('salvaSito');
    if (b) b.addEventListener('click', () => {
      const s = {};
      Array.prototype.forEach.call(document.querySelectorAll('#sito input'), i => {
        const v = i.value.trim().replace(',', '.');
        s[i.getAttribute('data-sito')] = v === '' ? null : Number(v);
      });
      $('esitoSito').textContent = T('Pag_Salvo');
      chiedi('salvaSito', { sito: s }).then(r2 => {
        $('esitoSito').textContent = r2.ok ? T('Pag_Salvato')
          : T('Pag_NonSalvatoPerche').replace('{0}', r2.messaggio || r2.codice || '');
        if (r2.ok) chiedi('sito').then(disegnaSito);
      });
    });
  }

  function disegnaRuota(r) {
    righeRuota = r.righe || [];
    /*  La ruota puo' essere cambiata sotto: un filtro tolto, il profilo cambiato. Se
     *  lo slot che si stava guardando non c'e' piu', si torna al primo invece di
     *  leggere fuori dall'elenco. */
    if (slotScelto >= righeRuota.length) { slotScelto = 0; }
    catalogo = r.catalogo || [];
    catalogoOk = !!r.catalogoDisponibile;
    diSerie = r.diSerie || [];

    const avvisi = [];
    if (r.ruotaVuota) avvisi.push(esc(r.ruotaVuota));
    if (r.nota) avvisi.push(esc(r.nota));
    if (!catalogoOk) avvisi.push(MF('Pag_MotoreZitto'));
    if (!r.dichiarati) avvisi.push(MF('Pag_NessunFiltroDichiarato', diSerie.map(esc).join(', ')));

    /* «nessun filtro» e' una scelta legittima, non un'assenza: su una camera a
       matrice il motore puo' dire che davanti non ci va niente. Chi ha uno slot
       vuoto o un vetro trasparente lo dichiara qui, e la consegna sa che slot
       chiedere invece di rifiutare. */
    const opzioni = (scelto) => '<option value="">' + esc(T('Pag_OpzNonDichiarato')) + '</option>' +
      '<option value="__none"' + (scelto === '__none' ? ' selected' : '') +
        '>' + esc(T('Pag_OpzNessunFiltro')) + '</option>' +
      catalogo.map(v => '<option value="' + esc(v.id) + '"' +
        (v.id === scelto ? ' selected' : '') + '>' + esc(v.nome) +
        (v.fwhm_nm ? ' — ' + v.fwhm_nm + ' nm' : '') +
        (v.bande && v.bande.length > 1 ? ' [' + v.bande.map(esc).join('+') + ']' : '') +
        '</option>').join('');

    const stati = { mappato: '&#10003;', nonmappato: '&mdash;', ignoto: '?', orfano: '!' };
    const spiega = {
      mappato: T('Pag_StatoDichiarato'), nonmappato: T('Pag_StatoDaDichiarare'),
      ignoto: T('Pag_StatoIgnoto'), orfano: T('Pag_StatoOrfano')
    };

    /*  LA BANDA IN UNA PASTIGLIA.
     *
     *  Il colore aiuta a riconoscere; la sigla e' sempre scritta, perche' chi non
     *  distingue i colori deve leggere la pagina lo stesso. Le bande sono quelle che
     *  manda il motore — Ha, OIII, SII, L, R, G, B, dual — e qui si sceglie soltanto
     *  come si vedono: nessuna banda viene calcolata o dedotta.
     *
     *  Un dual mostra le DUE righe che raccoglie, non una sola: sceglierne una
     *  sarebbe dire meno di quello che il motore ha dichiarato. Le righe stanno in
     *  `bande` sulla voce di catalogo, e si raggiungono per identificativo — e' una
     *  ricerca in una tabella ricevuta, non un'inferenza.
     */
    const dellaBanda = { Ha: 'b-ha', OIII: 'b-oiii', SII: 'b-sii',
                         L: 'b-l', R: 'b-r', G: 'b-g', B: 'b-b', dual: 'b-dual' };
    const vociCatalogo = {};
    for (const v of catalogo) { vociCatalogo[v.id] = v; }

    const pastiglia = (x) => {
      const v = x.id ? vociCatalogo[x.id] : null;
      const bande = v && v.bande && v.bande.length > 1 ? v.bande : null;
      if (bande) {
        /*  Ogni riga si tiene il suo colore: un dual non e' una terza banda, sono
         *  due bande dentro un filtro solo. */
        return '<span class="banda b-dual"><i class="r1">' + esc(bande[0]) + '</i>+' +
               '<i class="r2">' + esc(bande[1]) + '</i></span>';
      }
      if (!x.banda) return '<span class="banda vuota">' + esc(T('Pag_SenzaBanda')) + '</span>';
      const cl = dellaBanda[x.banda] || 'b-l';
      return '<span class="banda ' + cl + '">' + esc(x.banda) + '</span>';
    };

    /*  LA FILA DEGLI SLOT, nell'ordine fisico in cui stanno nella ruota. Anche quelli
     *  non dichiarati ci sono: uno slot che non si vede e' un'assenza che si scopre
     *  sotto il cielo. */
    const fila = righeRuota.map((x, i) =>
      '<button type="button" class="slot' + (x.stato === 'orfano' ? ' orfano' : '') + '"' +
        ' data-riga="' + i + '" aria-pressed="' + (i === slotScelto) + '"' +
        ' title="' + esc(MF('Pag_SlotNumero',
              x.slot === null || x.slot === undefined ? '?' : x.slot) +
            ' · ' + x.nina + ' · ' + (spiega[x.stato] || '')) + '">' +
        '<span class="slot-n">' +
          (x.slot === null || x.slot === undefined ? '&mdash;' : x.slot) + '</span>' +
        pastiglia(x) +
        '<span class="slot-nome">' + esc(x.nina) +
          (x.ambiguo ? ' <span title="' + esc(T('Pag_TipAmbiguo')) + '">&#9888;</span>' : '') +
        '</span>' +
        /*  Nella scheda solo il segno; la parola sta nel suggerimento del riquadro,
         *  e sta SEMPRE scritta per esteso nella scheda dello slot scelto. Cosi' dieci
         *  slot stanno sott'occhio insieme senza che lo stato diventi un colore muto:
         *  il suggerimento e' una comodita', non l'unico modo di saperlo. */
        '<span class="slot-stato">' + (stati[x.stato] || '') + '</span>' +
      '</button>').join('');

    /*  LA SCHEDA DI QUELLO SCELTO. Una tendina sola invece di dieci: la fila si legge,
     *  la scheda si tocca. E i dati che mancano si vedono mancare. */
    const scelto = righeRuota[slotScelto];
    const vScelto = scelto && scelto.id ? vociCatalogo[scelto.id] : null;
    const dato = (chiave, valore) =>
      '<span class="dato"><span class="k">' + esc(T(chiave)) + '</span>' +
      (valore === null || valore === undefined
        ? '<span class="assente">' + esc(T('Pag_NonDisponibile')) + '</span>'
        : '<span>' + valore + '</span>') + '</span>';

    const scheda = !scelto ? '' :
      '<div class="scheda">' +
        '<div class="scheda-titolo"><span class="slot-n">' +
          esc(MF('Pag_SlotNumero', scelto.slot === null || scelto.slot === undefined
                                    ? '&mdash;' : scelto.slot)) +
        '</span><b>' + esc(scelto.nina) + '</b></div>' +
        '<label>' + esc(T('Pag_ColEQuestoFiltro')) + ' ' +
          '<select data-riga="' + slotScelto + '"' + (catalogoOk ? '' : ' disabled') + '>' +
          opzioni(scelto.id) + '</select></label>' +
        '<div class="dati">' +
          dato('Pag_Banda', vScelto && vScelto.bande && vScelto.bande.length > 1
                 ? esc(vScelto.bande.join('+')) + ' <span style="opacity:.6">' +
                   esc(T('Pag_UnFiltroDueRighe')) + '</span>'
                 : (scelto.banda ? esc(scelto.banda) : null)) +
          dato('Pag_Fwhm', vScelto && vScelto.fwhm_nm ? esc(vScelto.fwhm_nm) + ' nm' : null) +
          dato('Pag_Stato', esc(spiega[scelto.stato] || '')) +
        '</div>' +
        (scelto.nota ? '<div style="margin-top:8px;opacity:.8">&#9888; ' +
          esc(scelto.nota) + '</div>' : '') +
        /*  Il catalogo non dichiara questo filtro per la camera del profilo: non e' un
         *  divieto, e' un dubbio, e si dice a parole invece che con un'icona muta. */
        (scelto.stato === 'mappato' && scelto.adatto !== true &&
         r.cameraAMatrice !== null && r.cameraAMatrice !== undefined
          ? '<div style="margin-top:8px;opacity:.8">&#9888; ' +
            esc(T('Pag_TipNonAdatto')) + '</div>' : '') +
      '</div>';

    $('filtri').innerHTML =
      '<div class="box">' +
      '<b>' + T('Pag_ConfigFiltri') + '</b> — ' + MF('Pag_ConfigFiltriNota') +
      (avvisi.length ? '<div style="margin:.6em 0;opacity:.85">' +
        avvisi.map(a => '<div>&#9888; ' + a + '</div>').join('') + '</div>' : '') +
      '<div class="ruota">' + fila + '</div>' +
      scheda +
      '<div style="margin-top:.7em">' +
        '<button id="salvaFiltri"' + (catalogoOk ? '' : ' disabled') + '>' +
          T('Pag_SalvaConfigurazione') + '</button> ' +
        '<span id="esitoFiltri" style="margin-left:.6em;opacity:.8"></span>' +
      '</div></div>';

    /*  Scegliere uno slot ridisegna: lo stato sta in slotScelto, non nel DOM. */
    Array.prototype.forEach.call(document.querySelectorAll('#filtri .slot'), b => {
      b.addEventListener('click', () => {
        slotScelto = +b.getAttribute('data-riga');
        disegnaRuota(r);
      });
    });

    Array.prototype.forEach.call(document.querySelectorAll('#filtri select'), sel => {
      sel.addEventListener('change', () => {
        righeRuota[+sel.getAttribute('data-riga')].id = sel.value || null;
        /*  Si ridisegna perche' cambia anche la pastiglia della fila: la scelta si
         *  deve vedere subito dove si guarda, non solo dove si e' cliccato. */
        disegnaRuota(r);
        $('esitoFiltri').textContent = T('Pag_NonSalvato');
      });
    });
    const b = $('salvaFiltri');
    if (b) b.addEventListener('click', () => {
      $('esitoFiltri').textContent = T('Pag_Salvo');
      chiedi('salvaFiltri', { vetri: righeRuota.map(x => ({ nina: x.nina, id: x.id, nota: x.nota })) })
        .then(r2 => {
          $('esitoFiltri').textContent = r2.ok
            ? T('Pag_FiltriDichiarati').replace('{0}', r2.dichiarati || 0)
            : T('Pag_NonSalvatoPerche').replace('{0}', r2.messaggio || r2.codice || '');
          if (r2.ok) chiedi('filtri').then(disegnaRuota);
        });
    });
  }

  applicaVoci();
  ridisegna();
