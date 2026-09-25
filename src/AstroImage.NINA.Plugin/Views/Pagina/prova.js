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
  /*  LA CAMERA COM'E', quando c'e'. Nulla se non e' collegata, e allora la richiesta
   *  porta l'identificativo di catalogo come ha sempre fatto. Qui dentro non c'e'
   *  nessun nome di sensore e nessuna tabella: solo quello che il driver dichiara. */
  let camera = null;

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
  /*  LE POLITICHE DI SESSIONE, con la stessa regola dei modi: l'elenco e il predefinito li dichiara Strategy su
   *  /v1/salute, e questa pagina sa solo che una e' scelta e che va nella richiesta. */
  let politiche = [], politicaScelta = null;
  /*  LA STRADA SCELTA NEL MENU. Nulla vuol dire «sceglie il motore», e la richiesta non porta la chiave. Si azzera
   *  quando cambia l'oggetto, la data o le notti: una strada e' di una scheda e di una notte. */
  let stradaScelta = null;
  /*  Il banco: i campi che il servizio pubblica, i codici delle sue divergenze, quello che il C# ha letto e dichiarato
   *  per il profilo attivo, e il banco che l'ultima prescrizione ha usato. */
  let campiDelBanco = [], divergenzeDelBanco = [], bancoLetto = null, bancoUsato = null;
  /*  I campi del sito che il servizio pubblica: l'unita', il valore che il motore assume, la spiegazione. */
  let campiDelSito = [];
  /*  I dati che l'ultima prescrizione ha assunto: il blocco del sito li scrive accanto ai suoi campi. */
  let parzialeUsato = null;

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

  /* LA PRESCRIZIONE A SCHERMO SI RITIRA quando cambia quello su cui era calcolata: il profilo di N.I.N.A. (l'ospite lo
     dice da se'), oppure una ruota, un banco o un sito salvati diversi (lo dice la risposta del salvataggio — il ritiro
     generalizzato, 16 settembre 2026). Non si rietichetta e non si richiede da sola: si toglie, si dimentica la strada
     scelta, e si dice perche'. Le chiavi sono scritte per intero. */
  const FRASE_DEL_RITIRO = {
    profilo: 'Pag_ProfiloCambiato', ruota: 'Pag_RitirataPerRuota',
    banco: 'Pag_RitirataPerBanco', sito: 'Pag_RitirataPerSito',
    camera: 'Pag_RitirataPerCamera',
  };
  function ritiraDalloSchermo(perche) {
    if (!perche) return;
    stradaScelta = null;
    bancoUsato = null;
    parzialeUsato = null;
    $('dettagli').innerHTML = '';
    $('uscita').innerHTML = '<div class="box" style="border-color:#e0a030"><span style="color:#e0a030">' +
      esc(T(FRASE_DEL_RITIRO[perche] || 'Pag_ProfiloCambiato')) + '</span></div>';
    aggiornaProvenienzeDelSito();
  }

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
      riempiElencoOggetti();
      return;
    }
    /* IL PROFILO DI N.I.N.A. E' CAMBIATO. La prescrizione a schermo era calcolata sul banco e sul sito dell'altro
       profilo: si ritira — non si rietichetta e non si richiede da sola — e si dice perche'. Sito, filtri e camera si
       rileggono, perche' adesso sono quelli del profilo nuovo, o nessuno. */
    if (r && r.evento === 'profilo') {
      ritiraDalloSchermo('profilo');
      ridisegna();
      return;
    }
    /* LA CAMERA SI E' COLLEGATA O SCOLLEGATA (16 settembre 2026): il banco e' cambiato, e il pannello ha ritirato la
       prescrizione in mano. Si toglie dallo schermo e si rilegge il banco. */
    if (r && r.evento === 'camera') {
      ritiraDalloSchermo('camera');
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
  /*  IL MOTORE NON RISPONDE, E SI DEVE VEDERE.
   *
   *  L'avviso va dove sarebbero comparsi i tre riquadri, perche' e' li' che manca
   *  qualcosa: un messaggio in fondo alla pagina lo leggerebbe chi lo sta gia'
   *  cercando. E porta l'INDIRIZZO, che l'ospite ha gia' composto: «non risponde»
   *  senza dire dove non dice dove andare a guardare, ed e' la differenza fra un
   *  minuto e un pomeriggio.
   *
   *  Non inventa niente: i tre modi li dichiara Strategy, e finche' non risponde la
   *  pagina dice che non risponde invece di mostrare tre riquadri suoi.         */
  function motoreGiu(r) {
    modi = []; modoScelto = null;
    $('modi').innerHTML = '';
    /*  IN CIMA, E CON UN TASTO (17 settembre 2026): l'avviso e' la prima cosa che vede chi ha appena installato il
     *  plugin, quindi sta sopra la domanda e non sotto; dice dove si scrive l'indirizzo, perche' «non risponde» senza
     *  dire dove si cambia lascia fermi; e si riprova senza chiudere il pannello, che era l'unico modo. */
    const dove = (r && r.messaggio) ? '<div class="mg-dove">' + esc(r.messaggio) + '</div>' : '';
    $('avviso').innerHTML =
      '<div class="box err motore-giu">' +
      '<b>' + esc(T('Pag_StrategyNonRisponde')) + '</b>' + dove +
      '<div class="mg-perche">' + esc(T('Pag_PercioNienteModi')) + '</div>' +
      '<div class="mg-perche">' + esc(T('Pag_DoveSiScriveIndirizzo')) + '</div>' +
      '<div style="margin-top:10px"><button id="riprova">' + esc(T('Pag_Riprova')) + '</button></div></div>';
    const b = $('riprova');
    if (b) b.addEventListener('click', riprovaIlMotore);
    stato((r && r.messaggio) ? r.messaggio : T('Pag_ServizioGiu'), 'no');
  }

  function riprovaIlMotore() {
    stato(T('Pag_InAttesa'));
    ridisegna();
    chiedi('salute').then(r => stato(r.ok ? T('Pag_ServizioSu') : (r.messaggio || T('Pag_ServizioGiu')), r.ok ? 'ok' : 'no'));
  }

  function ridisegna() {
    chiedi('modalita').then(r => {
      /*  Qui c'era `return`, e basta: senza modi la pagina non disegnava i riquadri
          e non diceva niente. Adesso lo dice, con l'indirizzo. */
      if (!r.ok || !r.modalita || !r.modalita.length) { motoreGiu(r); return; }
      $('avviso').innerHTML = '';
      modi = r.modalita;
      modoScelto = r.diSerie && modi.some(m => m.id === r.diSerie) ? r.diSerie : modi[0].id;
      disegnaModi();
      politiche = r.politiche || [];
      politicaScelta = r.politicaDiSerie && politiche.some(x => x.id === r.politicaDiSerie) ? r.politicaDiSerie
        : (politiche.length ? politiche[0].id : null);
      disegnaPolitiche();
      campiDelBanco = r.campiDelBanco || [];
      divergenzeDelBanco = r.divergenzeDelBanco || [];
      disegnaBanco();
      /*  e i campi del sito, che possono arrivare prima o dopo il sito: si applicano a quello che e' a schermo */
      campiDelSito = r.campiDelSito || [];
      spiegaIlSito();
      riconosciLaCamera();
    });
    chiedi('camera').then(r => { if (r.ok) { camera = r.camera || null; riconosciLaCamera(); } });
    chiedi('banco').then(r => { if (r.ok) { bancoLetto = r; disegnaBanco(); riconosciLaCamera(); } });
    chiedi('voci').then(r => { if (r.ok) riempiVoci(r); });
    chiedi('sito').then(r => { if (r.ok) disegnaSito(r); });
    chiedi('filtri').then(r => { if (r.ok) disegnaRuota(r); });
    /*  I modi si ridisegnano soltanto: l'elenco e la scelta restano quelli, cambia
     *  la lingua delle parole. Richiederli al servizio sarebbe una chiamata in piu'
     *  per girare un interruttore. */
    if (modi.length) disegnaModi();
    if (politiche.length) disegnaPolitiche();
  }

  /*  Uno stato scritto e' il resoconto di una cosa avvenuta: resta nella lingua in cui e' avvenuta, e il cambio lingua
      non lo riporta al testo iniziale (lo faceva, e lasciava il colore di prima: «in attesa» in verde). */
  const stato = (t, c) => { const s = $('stato'); s.removeAttribute('data-loc'); s.textContent = t; s.className = 'stato ' + (c || ''); };
  /*  Le virgolette si proteggono come gli angoli, e non e' pedanteria: `esc` finisce
   *  dentro un attributo ventisei volte in questo file, e li' una virgoletta nel
   *  testo chiuderebbe l'attributo e trasformerebbe il resto in markup. Nel testo
   *  normale &quot; si vede come una virgoletta, quindi non costa niente. */
  const esc = s => String(s).replace(/[&<>"]/g,
    c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;' }[c]));
  /*  IL NUMERO DA LEGGERE (regia, 16 settembre 2026): il separatore decimale della lingua di chi guarda — una parola del
   *  dizionario, virgola o punto — e, da cinque cifre intere in su, lo spazio fine delle migliaia, come la pagina del
   *  motore. `decimali` fissa le cifre dopo il separatore; senza, il numero resta come e' arrivato. Non si ricava
   *  niente: si scrive. Quello che non e' un numero si scrive com'e', protetto. */
  const MIGLIAIA = new RegExp(String.raw`\B(?=(\d{3})+(?!\d))`, 'g');
  const cifra = (v, decimali) => {
    if (v === null || v === undefined || v === '') return '—';
    if (typeof v !== 'number' || !isFinite(v)) return esc(v);
    const s = decimali == null ? String(v) : v.toFixed(decimali);
    if (s.toLowerCase().indexOf('e') >= 0) return esc(s);
    const parti = s.split('.');
    const intere = parti[0].replace('-', '');
    return (parti[0].charAt(0) === '-' ? '-' : '') + (intere.length >= 5 ? intere.replace(MIGLIAIA, '\u202f') : intere) +
      (parti.length > 1 ? esc(T('Pag_SeparatoreDecimale')) + parti[1] : '');
  };
  /*  Un campo che il motore non ha mandato si scrive come niente, non come «undefined»: e' un ripiego sul vuoto, non
   *  su un valore. */
  const escOVuoto = s => s == null ? '' : esc(s);

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
  /*  UNA CHIAVE CHIESTA E NON TROVATA FA RUMORE, una volta per chiave. Alla pagina arrivano solo le voci Pag_
   *  (Pagina.Prefisso): una chiave scritta senza quel prefisso non arriva, e senza questo avviso sarebbe
   *  un'etichetta che manca senza un errore. Con una riserva si ripiega in silenzio, perche' li' e' voluto. */
  const chiaviMancanti = new Set();
  const T = (k, riserva) => { const v = (window.__LOC__ || {})[k];
    if (v === undefined && riserva === undefined && !chiaviMancanti.has(k)) {
      chiaviMancanti.add(k);
      console.warn('[AstroImage] chiave assente dal dizionario della pagina: ' + k +
        (k.indexOf('Pag_') === 0 ? '' : ' (senza il prefisso Pag_ non arriva alla pagina)'));
    }
    return v === undefined ? (riserva !== undefined ? riserva : k) : v; };
  const M = t => esc(t).replace(/\*([^*]+)\*/g, '<strong>$1</strong>');
  const MF = (k, ...a) => M(T(k)).replace(/\{(\d+)\}/g, (m, i) => a[+i] === undefined ? m : a[+i]);

  /* Riempie tutto quello che nel markup porta una chiave. Si richiama a ogni cambio
     di lingua: le etichette fisse non passano da un ridisegno. */
  function applicaVoci() {
    for (const el of document.querySelectorAll('[data-loc]'))
      el.innerHTML = M(T(el.getAttribute('data-loc')));
    /*  i suggerimenti e il segnaposto della domanda (17 settembre 2026): testo semplice, senza il grassetto */
    for (const el of document.querySelectorAll('[data-loc-title]'))
      el.title = T(el.getAttribute('data-loc-title')).replace(/\*/g, '');
    for (const el of document.querySelectorAll('[data-loc-placeholder]'))
      el.placeholder = T(el.getAttribute('data-loc-placeholder')).replace(/\*/g, '');
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

  /*  IL SECONDO CONTROLLO: COME SI DISTRIBUISCONO LE NOTTI. Stessa forma dei modi e stessa regola: l'elenco arriva
   *  dal servizio, la traduzione vince quando conosce l'identificativo e ripiega sulle parole del motore quando no. */
  const PAROLE_POLITICA = {
    sessione: { nome: 'Pag_Politica_sessione', nota: 'Pag_PoliticaNota_sessione' },
    progetto: { nome: 'Pag_Politica_progetto', nota: 'Pag_PoliticaNota_progetto' },
  };
  function disegnaPolitiche() {
    if (!politiche.length) { $('politiche').innerHTML = ''; return; }
    const titolo = T('Pag_ComePianificare');
    $('politiche').innerHTML =
      '<fieldset class="goalbox"><legend class="hc-k">' + esc(titolo) + '</legend>' +
      '<div class="goalgrid" role="radiogroup" aria-label="' + esc(titolo) + '">' +
      politiche.map(x => {
        const p = PAROLE_POLITICA[x.id];
        const nome = p ? T(p.nome, x.etichetta || x.id) : (x.etichetta || x.id);
        const nota = p ? T(p.nota, x.spiegazione || '') : (x.spiegazione || '');
        return '<label class="goalcard"><input type="radio" name="politica" value="' + esc(x.id) + '"' +
            (x.id === politicaScelta ? ' checked' : '') + '>' +
          '<span class="gc"><span class="gc-h"><b>' + esc(nome) + '</b>' +
            '<span class="gc-tick" aria-hidden="true">&#10003;</span></span>' +
          '<span class="gc-d">' + esc(nota) + '</span></span></label>';
      }).join('') + '</div></fieldset>';
    Array.prototype.forEach.call($('politiche').querySelectorAll('.goalcard input'), i => {
      i.addEventListener('change', () => { politicaScelta = i.value; });
    });
  }

  /* ── IL BANCO ─────────────────────────────────────────────────────────────────
     Tre pezzi, ognuno col suo nome sopra: quello che N.I.N.A. tiene — focale e rapporto, letti ogni volta, e da qui non
     si cambiano —, quello che il catalogo del motore propone per la voce riconosciuta, e quello che dichiari tu perche'
     non ce l'ha nessuno dei due, e che vince. Il blocco si costruisce dalla lista che il servizio pubblica in
     /v1/salute: un campo per cui il Ponte non ha una parola non si mostra, e la guardia del motore lo dice in rosso.
     Le divergenze fra due sorgenti le calcola il motore; qui si scrivono coi due numeri e le due provenienze, e un
     codice che qui non ha una parola si scrive com'e', invece di tacerlo. Le chiavi delle parole sono letterali. */
  const PAROLA_CAMPO_BANCO = {
    'tel': 'Pag_Banco_tel', 'tel.apertura_mm': 'Pag_Banco_tel_apertura_mm',
    'tel.ostruzione_pct': 'Pag_Banco_tel_ostruzione_pct', 'tel.trasmissione': 'Pag_Banco_tel_trasmissione',
    'tel.focale_mm': 'Pag_Banco_tel_focale_mm', 'tel.rapporto': 'Pag_Banco_tel_rapporto',
    'red': 'Pag_Banco_red', 'mnt': 'Pag_Banco_mnt', 'cam': 'Pag_Banco_cam',
    'mnt.rms_caratteristico_arcsec': 'Pag_Banco_mnt_rms_caratteristico_arcsec',
    'mnt.posa_massima_s': 'Pag_Banco_mnt_posa_massima_s',
    'luna.riferimento_deg': 'Pag_Banco_luna_riferimento_deg',
    /* la camera fuori catalogo, descritta a mano (17 settembre 2026) */
    'cam.nome': 'Pag_Banco_Descritta_nome', 'cam.matrice': 'Pag_Banco_Descritta_matrice', 'cam.pixel_um': 'Pag_Banco_Descritta_pixel_um',
    'cam.width_px': 'Pag_Banco_Descritta_width_px', 'cam.height_px': 'Pag_Banco_Descritta_height_px',
    'cam.rumore_lettura_e': 'Pag_Banco_Descritta_rumore_lettura_e', 'cam.qe_picco_pct': 'Pag_Banco_Descritta_qe_picco_pct',
    'cam.pozzo_e': 'Pag_Banco_Descritta_pozzo_e' };
  const PAROLA_MATRICE = { colore: 'Pag_Banco_Matrice_colore', mono: 'Pag_Banco_Matrice_mono' };

  const PAROLA_DIVERGENZA_BANCO = {
    'dichiarato_diverso_dal_catalogo': 'Pag_Banco_Div_dichiarato_diverso_dal_catalogo',
    'focale_diversa_dal_catalogo': 'Pag_Banco_Div_focale_diversa_dal_catalogo',
    'apertura_diversa_da_nina': 'Pag_Banco_Div_apertura_diversa_da_nina',
    'geometria_diversa_dal_catalogo': 'Pag_Banco_Div_geometria_diversa_dal_catalogo',
    'descrizione_non_usata': 'Pag_Banco_Div_descrizione_non_usata' };
  /*  I campi della geometria della camera, per la divergenza fra il driver e la voce riconosciuta. */
  const PAROLA_CAMPO_CAMERA = { pixel_um: 'Pag_Banco_Cam_pixel_um', width_px: 'Pag_Banco_Cam_width_px',
    height_px: 'Pag_Banco_Cam_height_px', matrice: 'Pag_Banco_Cam_matrice' };
  const PAROLA_FONTE_BANCO = { nina: 'Pag_Banco_Fonte_nina', catalogo: 'Pag_Banco_Fonte_catalogo',
    dichiarato: 'Pag_Banco_Fonte_dichiarato', riferimento: 'Pag_Banco_Fonte_riferimento' };
  /*  La camera c'e' dal 16 settembre 2026: la geometria la dice il driver, la voce di catalogo porta la fisica. */
  /*  La Luna c'e' dal 16 settembre 2026: la distanza di riferimento da cui Strategy ricava la soglia di ogni filtro. */
  const PEZZI_DEL_BLOCCO = ['ottica', 'riduttore', 'camera', 'montatura', 'luna'];

  /*  QUELLO CHE IL BANCO NON DICEVA (regia, 16 settembre 2026): `parziale` del prodotto, in giallo, una parola per tipo
   *  coi numeri che il motore manda. Un tipo senza parola si scrive col suo nome: si vede e non si perde, anche prima che
   *  il dizionario impari a dirlo. Le chiavi sono letterali; i campi, nell'ordine dei segnaposto. */
  const PAROLA_PARZIALE = {
    'rumore_di_lettura_non_noto': ['Pag_Parziale_rumore_di_lettura_non_noto', ['assunto']],
    'qe_non_nota': ['Pag_Parziale_qe_non_nota', ['voce', 'assunto']],
    'posa_massima_non_nota': ['Pag_Parziale_posa_massima_non_nota', ['tettoUsato']],
    'rumore_sotto_il_pavimento': ['Pag_Parziale_rumore_sotto_il_pavimento', ['dichiarato', 'dove', 'pavimento']],
    'rms_caratteristico_non_noto': ['Pag_Parziale_rms_caratteristico_non_noto', ['rmsUsato']],
    'buio_non_noto': ['Pag_Parziale_buio_non_noto', ['assunto', 'temperatura_c']],
    'buio_senza_fonte': ['Pag_Parziale_buio_senza_fonte', ['voce', 'valore']],
    'camera_non_riconosciuta': ['Pag_Parziale_camera_non_riconosciuta', ['nome', 'pozzo']],
    'coppia_pozzo_rumore_incerta': ['Pag_Parziale_coppia_pozzo_rumore_incerta', ['voce', 'rumore', 'pozzo']],
    'pozzo_non_noto': ['Pag_Parziale_pozzo_non_noto', ['assunto']],
    'filtro_davanti_non_dichiarato': ['Pag_Parziale_filtro_davanti_non_dichiarato', ['voce']],
    'bin_non_dichiarato': ['Pag_Parziale_bin_non_dichiarato', []],
    'orizzonte_non_dichiarato': ['Pag_Parziale_orizzonte_non_dichiarato', ['assunto']],
    'seeing_non_dichiarato': ['Pag_Parziale_seeing_non_dichiarato', ['assunto']],
    'notti_serene_non_dichiarate': ['Pag_Parziale_notti_serene_non_dichiarate', ['assunto']],
    'ruota_non_dichiarata': ['Pag_Parziale_ruota_non_dichiarata', ['usata']],
    'convenzioni_di_posa': ['Pag_Parziale_convenzioni_di_posa', ['assunte.download', 'assunte.settle', 'assunte.ditherEvery']],
  };
  const campoDelDato = (d, via) => { let o = d; for (const k of via.split('.')) o = (o == null ? o : o[k]); return o; };
  /*  IL GIALLO E' PER CIO' CHE PUO' CAMBIARE UNA DECISIONE ED E' DIVERSO DAL SOLITO (regia, 18 settembre 2026). Tre
   *  righe gialle fisse su ogni prescrizione insegnano a ignorare il giallo. Due cose non ci vanno:
   *  · un'assunzione del sito che ha il suo posto nel blocco del sito — la riga del campo la scrive accanto al numero —
   *    si legge li', e qui non si ripete: si dice una volta, dove si vede (16 settembre 2026);
   *  · un'assunzione su un campo del sito che il motore dichiara che non decide (`decide` in `limiti.sito`: seeing, guida,
   *    notti serene) e' una nota, non un avviso: va in `notaDeiRiferimenti`, quieta, col valore e la provenienza.
   *  Senza il blocco del sito a schermo, o con un campo che il motore non descrive, si dice qui.
   *  Sorvegliato da PaginaTests, che legge la pagina. */
  /*  IL CAMPIONAMENTO (regia, 18 settembre 2026): il giudizio con la sua parola e la sua spiegazione, in due lingue dal
   *  motore; l'intervallo, la scala del pixel e la FWHM consegnata, coi numeri del motore. Il giudizio e' sulle stelle —
   *  il seeing con la guida caratteristica della montatura, la stessa della posa — e non contiene il pixel, perche' si
   *  giudica proprio lui; la FWHM consegnata ha il pixel, ed e' quella che si misura nel sub: due numeri della stessa
   *  origine, mai uno al posto dell'altro. Quando il giudizio e la posa hanno contato due seeing diversi, si dicono tutti
   *  e due. Senza i numeri del motore la riga non c'e'. Sorvegliata da PaginaTests. */
  function rigaDelCampionamento(p) {
    const s = p && p.valutazione && p.valutazione.samp;
    if (!s || s.lo == null || s.hi == null || s.scala == null || s.consegnata == null) return '';
    const lingua = T('Pag_CodiceLingua');
    const nella = o => (o && (o[lingua] || o.it || o.en)) || '';
    const parola = nella(s.etichetta) || s.k || '';
    const spiega = nella(s.spiegazione), spiegaConsegnata = nella(s.spiegazione_consegnata);
    /*  perche' la posa non segue il seeing del sito, dal motore (19 settembre 2026): arriva quando serve */
    const spiegaDueSeeing = nella(s.spiegazione_due_seeing);
    const posa = Object.values(p.posa || {}).find(v => v && v.ex && v.ex.ipotesi && typeof v.ex.ipotesi.seeing === 'number');
    const seeingPosa = posa ? posa.ex.ipotesi.seeing : null;
    /*  i due numeri vengono dal motore, dalla stessa costante quando il seeing e' quello di riferimento: si confrontano */
    const dueSeeing = typeof s.seeing === 'number' && seeingPosa != null && s.seeing !== seeingPosa;
    return '<tr><th>' + esc(T('Pag_RigaCampionamento')) + '</th><td>' +
      '<span class="pill ' + esc(s.cls || 'p-dim') + '"' + (spiega ? ' title="' + esc(spiega) + '"' : '') + '>' + esc(parola) + '</span> ' +
      MF('Pag_Camp_Intervallo', cifra(s.lo, 2), cifra(s.hi, 2)) + ' · ' + MF('Pag_Camp_Scala', cifra(s.scala, 2)) + ' · ' +
      '<span' + (spiegaConsegnata ? ' title="' + esc(spiegaConsegnata) + '"' : '') + '>' +
        MF('Pag_Camp_Consegnata', cifra(s.consegnata, 2)) + '</span>' +
      (dueSeeing ? '<div style="font-size:12px;opacity:.75"' + (spiegaDueSeeing ? ' title="' + esc(spiegaDueSeeing) + '"' : '') + '>' +
        MF('Pag_Camp_DueSeeing', cifra(s.seeing, 1), cifra(seeingPosa, 1)) + '</div>' : '') +
      '</td></tr>';
  }
  /*  IL PROFILO DEL PROGETTO (regia, 19 settembre 2026). L'unita' non e' la notte, e' il progetto: il motore
   *  propone un profilo — modo, pose, strada, temperatura — e la prima consegna lo congela. Il pannello lo dice PRIMA,
   *  perche' chi apre un progetto per sbaglio si chiederebbe poi perche' i numeri non si muovono piu'. Con un progetto
   *  aperto dice quando si e' aperto, i dark che chiede, le premesse cambiate e il loro costo — tutte frasi del motore —,
   *  i canali che stanotte saltano, e «apri un progetto nuovo» a un clic, con scritto quanti dark nuovi costa. */
  function bloccoDelProgetto(p, r) {
    const pf = p && p.profilo;
    if (!pf) return '';
    const lingua = T('Pag_CodiceLingua');
    const nella = o => (o && (o[lingua] || o.it || o.en)) || '';
    const dark = nella(p.dark && p.dark.frase);
    const riga = (t, stile) => '<div style="margin-top:6px' + (stile ? ';' + stile : '') + '">' + t + '</div>';
    let h = '<b>' + T('Pag_Progetto_Titolo') + '</b>';
    if (pf.stato !== 'congelato') {
      h += riga(MF('Pag_Progetto_ConsegnaApre'));
      if (dark) h += riga(MF('Pag_Progetto_Dark', esc(dark)), 'opacity:.85');
      for (const x of pf.pareggio || []) h += riga(esc(nella(x.frase)), 'opacity:.75;font-size:12.5px');
      return '<div class="box" id="progetto">' + h + '</div>';
    }
    const pr = p.progetto || {};
    const d = pr.decisione_di_adesso || {};
    h += riga(MF('Pag_Progetto_Aperto', esc((r && r.progetto && r.progetto.apertoIl) || '—')));
    if (dark) h += riga(MF('Pag_Progetto_Dark', esc(dark)), 'opacity:.85');
    if (pr.frase) h += riga(esc(nella(pr.frase)), 'color:#e0a030');
    else if (pr.nota) h += riga(esc(nella(pr.nota)), 'opacity:.7;font-size:12.5px');
    for (const s of p.saltati_stanotte || []) h += riga(MF('Pag_Progetto_Saltato', esc(s.canale), esc(s.filtro)), 'color:#e0a030');
    const costo = d.disponibile && d.dark_nuovi ? MF('Pag_Progetto_NuovoCosto', esc(nella(d.dark_nuovi.frase)))
      : d.disponibile && d.uguale ? MF('Pag_Progetto_NuovoSenzaDark') : d.disponibile ? MF('Pag_Progetto_NuovoNessunDark') : '';
    h += '<div style="margin-top:8px"><button id="nuovoProgetto">' + T('Pag_Progetto_Nuovo') + '</button> ' +
      '<span style="opacity:.75;font-size:12.5px">' + costo + '</span> ' + esito('esitoProgetto') + '</div>';
    return '<div class="box" id="progetto">' + h + '</div>';
  }
  function dettaNelSito(p) {
    if (!p || p.pezzo !== 'sito' || !p.campo) return false;
    return Array.prototype.some.call(document.querySelectorAll('#sito [data-prov-sito]'),
      el => el.getAttribute('data-prov-sito') === p.campo);
  }
  function eUnaNota(p) {
    const c = p && p.pezzo === 'sito' && p.campo ? campoDelSito(p.campo) : null;
    return !!c && c.decide === false;
  }
  function notaDeiRiferimenti(lista) {
    /*  un'assunzione che ha il suo campo a schermo si dice accanto al campo, non anche qui (il seeing, 19 settembre) */
    const note = (lista || []).filter(p => eUnaNota(p) && !dettaNelSito(p)).map(p => {
      const w = PAROLA_PARZIALE[p.tipo];
      const valori = w ? w[1].map(via => { const v = campoDelDato(p.dati || {}, via); return v == null ? '—' : cifra(v); }) : [];
      const s = spiegazioneDi(campoDelSito(p.campo));
      return '<span' + (s ? ' title="' + esc(s) + '"' : '') + '>' +
        (w ? MF(w[0], ...valori) : esc(p.tipo + ' — ' + p.effetto)) + '</span>';
    });
    return note.length ? '<div class="nota-rif">' + note.join(' · ') + '</div>' : '';
  }
  function parzialeDelProdotto(lista) {
    const daDire = (lista || []).filter(p => !dettaNelSito(p) && !eUnaNota(p));
    if (!daDire.length) return '';
    const righe = daDire.map(p => {
      const w = PAROLA_PARZIALE[p.tipo];
      if (!w) return '<li>' + esc(p.tipo + ' — ' + p.effetto) + '</li>';
      const valori = w[1].map(via => { const v = campoDelDato(p.dati || {}, via);
        return v == null ? '—' : Array.isArray(v) ? esc(v.join(', ')) : cifra(v); });
      return '<li>' + MF(w[0], ...valori) + '</li>';
    });
    return '<div class="box" style="color:#e0a030"><b>' + esc(T('Pag_ParzialeTitolo')) + '</b>' +
      '<ul style="margin:.4em 0 0 1.1em;padding:0">' + righe.join('') + '</ul></div>';
  }

  /*  LA LUNA DENTRO LA NOTTE (regia, 17 settembre 2026). Il riquadro di prima aveva cinque righe di spiegazione e pallini
   *  che dicevano tutti ×1,0: un avviso che compare sempre non si legge il giorno che conta. Adesso una riga per canale,
   *  nella notte a cui si riferisce e nella forma della pagina del motore, solo quando Strategy non la dice trascurabile
   *  — la regola e' sua, e qui non si ripete. Le tre parti restano, col moltiplicatore in evidenza; il perche'
   *  sta al passaggio del mouse, e si spiega a chi chiede. La prescrizione non si vieta: il canale resta nella notte. */
  function righeDellaLuna(luna, notte) {
    const righe = ((luna && luna.penalizzazioni) || []).filter(f => f.notte === notte && !f.trascurabile && f.moltiplicatore != null);
    return righe.map(f => {
      const perche = T(f.limiteInferiore ? 'Pag_LunaPercheSotto' : 'Pag_LunaPercheSopra').replace('{0}', cifra(f.soglia, 0)) +
        (f.congiunto ? ' ' + T('Pag_LunaPenaCongiunto') : '');
      const testo = f.limiteInferiore
        ? MF('Pag_LunaRigaMinima', esc(f.id), cifra(f.moltiplicatore, 1), cifra(f.fasePercento), cifra(f.distanza, 0), cifra(f.soglia, 0))
        : MF('Pag_LunaRiga', esc(f.id), cifra(f.moltiplicatore, 1), cifra(f.fasePercento), cifra(f.distanza, 0));
      return '<div class="luna-riga' + (f.limiteInferiore ? ' sotto' : '') + '" title="' + esc(perche) + '">' + testo + '</div>';
    }).join('');
  }

  /*  UNA DATA DEI DATI, SCRITTA NELLA LINGUA DI CHI GUARDA. Una lingua che il browser non conosce lascia la data com'e'. */
  const dataScritta = (iso, forma) => {
    const d = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso || '');
    if (!d) return iso || '';
    try {
      return new Date(Date.UTC(+d[1], +d[2] - 1, +d[3])).toLocaleDateString(T('Pag_Men_FormatoData'),
        Object.assign({ timeZone: 'UTC' }, forma));
    } catch (e) { return iso; }
  };

  /*  LE NOTTI GIUSTE NON SONO QUESTE (regia, 17 settembre 2026): la cosa piu' utile che il motore sa, perche' dice
   *  quando smettere di pagare invece di quanto si paga. Strategy manda la data da cui le stesse notti rendono di piu',
   *  e niente quando spostarsi non conviene; il tasto mette la data e rifa' la domanda. */
  function nottiGiuste(p) {
    const g = p.notte && p.notte.meglio;
    if (!g) return '';
    return '<div class="box avviso">' +
      MF(p.notte.nottiUsate === 1 ? 'Pag_NottiGiusteUna' : 'Pag_NottiGiuste', esc(dataScritta(g.data, { day: 'numeric', month: 'long' })),
        cifra(g.spostataDi), cifra(p.notte.nottiUsate), cifra(g.resa, 1), esc(g.canale)) +
      ' <button id="spostaData" data-data="' + esc(g.data) + '">' +
      MF('Pag_SpostaAl', esc(dataScritta(g.data, { day: 'numeric', month: 'short' }))) + '</button></div>';
  }

  /*  L'IDENTITA' DELL'OGGETTO (regia, 17 settembre 2026): il nome, la pastiglia che dice quanto fidarsi della classe, e
   *  la classe, dove sta, quanto e' grande e quanto e' luminoso. La classe si scrive con la parola del Ponte quando la
   *  conosce, altrimenti con quella del motore. Le chiavi sono letterali. */
  const PAROLA_AFFIDABILITA = { scheda: 'Pag_Affidabilita_scheda', curato_senza_scheda: 'Pag_Affidabilita_curato_senza_scheda',
    certo: 'Pag_Affidabilita_certo', dedotto: 'Pag_Affidabilita_dedotto', da_collaudare: 'Pag_Affidabilita_da_collaudare' };
  const PAROLA_CLASSE = { hii_classic: 'Pag_Classe_hii_classic', hii_faint_he: 'Pag_Classe_hii_faint_he',
    wr_bubble: 'Pag_Classe_wr_bubble', snr: 'Pag_Classe_snr', pn_bright: 'Pag_Classe_pn_bright', pn_faint: 'Pag_Classe_pn_faint',
    reflection: 'Pag_Classe_reflection', dark_molecular: 'Pag_Classe_dark_molecular', spiral_hii: 'Pag_Classe_spiral_hii',
    elliptical_group: 'Pag_Classe_elliptical_group', tidal_ifn: 'Pag_Classe_tidal_ifn',
    cluster_globular: 'Pag_Classe_cluster_globular', cluster_open: 'Pag_Classe_cluster_open' };
  function identita(b, s) {
    s = s || {};
    const nome = esc((b.nomi || [b.id])[0]);
    const pa = PAROLA_AFFIDABILITA[s.affidabilita];
    const pastiglia = pa ? ' <span class="pastiglia ' + esc(s.affidabilita) + '">' + esc(T(pa)) + '</span>' : '';
    const classe = s.classe && PAROLA_CLASSE[s.classe] ? T(PAROLA_CLASSE[s.classe]) : (s.etichetta || s.classe);
    const dimensioni = Array.isArray(s.dimensioni_arcmin) && s.dimensioni_arcmin.length === 2
      ? cifra(s.dimensioni_arcmin[0], 1) + '′ × ' + cifra(s.dimensioni_arcmin[1], 1) + '′' : null;
    const parti = [classe ? esc(classe) : null, s.costellazione ? esc(s.costellazione) : null, dimensioni,
      s.magnitudine != null ? MF('Pag_Magnitudine', cifra(s.magnitudine, 1)) : null].filter(Boolean);
    return '<b>' + nome + '</b>' + pastiglia + (parti.length ? '<div class="identita">' + parti.join(' · ') + '</div>' : '');
  }

  /*  IL VERDETTO INTERO (regia, 16 e 17 settembre 2026): quanto del progetto coprono le notti chieste e quante ne
   *  servono per il minimo; le ore prescritte contro il costo intero, al minimo e al livello-obiettivo, e quante ne
   *  mancano; e il canale che decide l'immagine. I numeri li fa Strategy; senza, la parte non c'e'. */
  function verdettoIntero(pr) {
    const v = pr && pr.verdetto;
    if (!v || v.coperturaPercento == null) return '';
    const notti = v.perIlMinimo == null
      ? MF('Pag_Verdetto_MinimoOltre', cifra(v.notti), cifra(v.coperturaPercento), cifra(v.massimo))
      : v.perIlMinimo > v.notti
        ? MF('Pag_Verdetto_Minimo', cifra(v.notti), cifra(v.coperturaPercento), cifra(v.perIlMinimo))
        : MF('Pag_Verdetto_Coperto', cifra(v.notti), cifra(v.coperturaPercento));
    const o = pr.ore_di_progetto || {};
    const ore = o.soglie == null ? ''
      : '<div>' + (o.mancano > 0
        ? MF('Pag_Verdetto_Ore', cifra(o.spese, 1), cifra(o.soglie, 1), cifra(o.pieno, 1), cifra(o.mancano, 1))
        : MF('Pag_Verdetto_OreCoperte', cifra(o.spese, 1), cifra(o.soglie, 1), cifra(o.pieno, 1))) + '</div>';
    const canale = pr.critGroup ? '<div>' + MF('Pag_Verdetto_Canale', esc(pr.critGroup)) + '</div>' : '';
    return notti + ore + canale;
  }

  /*  LA RICHIESTA: ogni campo della lista col valore che gli spetta — dal profilo di N.I.N.A. se e' suo, dalla
   *  dichiarazione se e' dichiarabile o e' un riconoscimento. Nessun valore di serie: quello che manca manca, e il
   *  servizio dice che cosa. */
  function bancoDaMandare(soloVoceScritta) {
    const b = {};
    const metti = (chiave, valore) => {
      const parti = chiave.split('.');
      if (parti.length === 1) { b[parti[0]] = valore; return; }
      if (!b[parti[0]] || typeof b[parti[0]] !== 'object') b[parti[0]] = {};
      b[parti[0]][parti[1]] = valore;
    };
    const dichiarato = (bancoLetto && bancoLetto.dichiarato) || {};
    const nina = (bancoLetto && bancoLetto.nina) || {};
    campiDelBanco.forEach(c => {
      if (c.provenienza === 'nina') { if (nina[c.chiave] != null) metti(c.chiave, nina[c.chiave]); }
      else if ((c.provenienza === 'dichiarabile' || c.provenienza === 'riconoscimento') && dichiarato[c.chiave] != null)
        metti(c.chiave, dichiarato[c.chiave]);
    });
    /*  LA CAMERA IN DUE: la descrizione del driver e, accanto, la voce dichiarata. Prima la descrizione sovrascriveva
     *  tutto; e con la camera spenta la voce dichiarata basta da sola. */
    /*  LA CAMERA FUORI CATALOGO (17 settembre 2026): con la camera scollegata parte la descrizione scritta a mano, come
     *  camera dichiarata; con la camera collegata la geometria la dice il driver, e della descrizione parte solo la
     *  fisica, che il driver non sa. Se accanto c'e' una voce, decide il motore: riconosciuta, la fisica e' la sua. */
    const descritta = {};
    campiDelBanco.forEach(c => {
      if (c.provenienza === 'descrizione' && dichiarato[c.chiave] != null) descritta[c.chiave.split('.')[1]] = dichiarato[c.chiave];
    });
    const idCamera = (b.cam && typeof b.cam === 'object') ? b.cam.id : null;
    /*  LA SOLA VOCE SCRITTA, quando chi riprende lo chiede per una richiesta (18 settembre 2026): la camera collegata
     *  vince di serie, e questa e' la strada per simulare un'altra camera senza scollegare quella vera. */
    if (soloVoceScritta && idCamera) b.cam = { id: idCamera };
    else if (camera && typeof camera === 'object') {
      b.cam = Object.assign({}, camera, idCamera ? { id: idCamera } : {});
      ['rumore_lettura_e', 'qe_picco_pct', 'pozzo_e'].forEach(k => { if (descritta[k] != null) b.cam[k] = descritta[k]; });
    }
    else if (camera && !idCamera) b.cam = camera;
    else if (Object.keys(descritta).length) b.cam = Object.assign({}, b.cam || {}, descritta, { origine: 'dichiarata' });
    b.bin = 1;
    return b;
  }

  /*  Il numero come lo scrivi: il servizio giudica. Un testo che non e' un numero parte come testo, e il servizio lo
   *  rifiuta nominando il campo, invece di diventare in silenzio un'assenza. */
  const valoreScritto = (v, numerico) => {
    const s = String(v == null ? '' : v).trim();
    if (s === '') return null;
    if (!numerico) return s;
    const n = Number(s.replace(',', '.'));
    return isFinite(n) ? n : s;
  };

  /*  LE VOCI DEL BANCO, sotto i campi dove si scrivono (17 settembre 2026): le prescrizioni si preparano giorni prima, col
   *  pezzo scollegato, e la voce si sceglie. L'elenco e' quello del servizio: il valore e' l'identificativo, accanto il
   *  nome. Scritta a mano, la voce la riconosce il motore anche senza l'identificativo esatto. */
  function riempiVoci(r) {
    let voci;
    try { voci = JSON.parse(r.corpo); } catch (e) { return; }
    for (const pezzo of ['ottica', 'camera', 'montatura']) {
      const dl = $('voci-' + pezzo);
      if (dl) dl.innerHTML = (voci[pezzo] || []).map(x =>
        '<option value="' + esc(x.id) + '">' + esc(x.nome) + '</option>').join('');
    }
  }

  /*  L'ESITO DI UN SALVATAGGIO RESTA SCRITTO (17 settembre 2026). Dopo un salvataggio riuscito il banco e il sito si
   *  ridisegnano, e l'esito scritto dentro il riquadro spariva con lui: chi salvava non vedeva niente. Adesso e' uno
   *  stato della pagina che il disegno rilegge: «corso» mentre si salva, «ok» in verde con l'ora, «no» in rosso col
   *  perche', «attesa» appena si tocca un campo dopo. Resta nella lingua in cui e' avvenuto, come gli altri stati
   *  scritti. */
  const esiti = {};
  function esito(id) {
    const e = esiti[id];
    return '<span id="' + id + '" class="esito' + (e ? ' ' + e.tipo : '') + '">' +
      (e ? (e.tipo === 'ok' ? '&#10003; ' : '') + esc(e.testo) : '') + '</span>';
  }
  function segnaEsito(id, tipo, testo) {
    esiti[id] = testo ? { tipo: tipo, testo: testo } : null;
    const s = $(id);
    if (s) s.outerHTML = esito(id);
  }
  const oraDiAdesso = () => {
    const d = new Date(), due = n => (n < 10 ? '0' : '') + n;
    return due(d.getHours()) + ':' + due(d.getMinutes());
  };

  /*  LA CAMERA DEL BANCO, RICONOSCIUTA SUBITO (17 settembre 2026). Il riconoscimento c'era solo nella prescrizione, e
   *  prima di chiederne una il banco non sapeva quale camera avesse davanti. Adesso si chiede a Strategy con la camera
   *  che la domanda manderebbe — il driver, la voce scritta, o la camera descritta — appena ci sono la camera, il banco e
   *  i suoi campi, e dopo ogni salvataggio. Vince l'ultima domanda. */
  let ultimoRiconoscimento = 0, cameraDelBanco = null;
  function riconosciLaCamera() {
    const mia = ++ultimoRiconoscimento;
    const cam = bancoDaMandare().cam;
    if (!cam || typeof cam !== 'object' || !Object.keys(cam).length) { cameraDelBanco = null; disegnaBanco(); return; }
    chiedi('riconosciCamera', null, { cam: cam }).then(r => {
      if (mia !== ultimoRiconoscimento) return;
      let c = null;
      try { c = r.ok ? JSON.parse(r.corpo).camera : null; } catch (e) { c = null; }
      cameraDelBanco = c || null;
      disegnaBanco();
    });
  }

  function disegnaBanco() {
    const box = $('banco');
    if (!box) return;
    if (!campiDelBanco.length) { box.innerHTML = ''; return; }
    const dichiarato = (bancoLetto && bancoLetto.dichiarato) || {};
    const nina = (bancoLetto && bancoLetto.nina) || {};
    const pb = bancoUsato;
    const campi = campiDelBanco.filter(c => PEZZI_DEL_BLOCCO.indexOf(c.pezzo) >= 0 &&
      !/\.id$/.test(c.chiave) && PAROLA_CAMPO_BANCO[c.chiave]);
    const primaDescritta = (campi.find(c => c.provenienza === 'descrizione') || {}).chiave;
    /*  LE CHIAVI RIMASTE NEL PROFILO (18 settembre 2026): un valore dichiarato per un campo che il servizio non elenca
     *  piu' — l'ostruzione, che ha cambiato nome con l'unita' — non parte nella richiesta. Si dice, invece di sparire
     *  senza che nessuno lo sappia; salvando il banco va via. */
    const orfane = Object.keys(dichiarato).filter(k => !campiDelBanco.some(c => c.chiave === k));
    const avvisoOrfane = orfane.length ? '<div style="margin:.4em 0;font-size:12.5px;color:#e0a030">' +
      MF('Pag_Banco_ChiaviOrfane', orfane.map(k => esc(k) + ' = ' + escOVuoto(dichiarato[k])).join(', ')) + '</div>' : '';
    /*  una camera riconosciuta spegne i campi della camera fuori catalogo, e ci mostra i suoi dati */
    const spenta = !!(cameraDelBanco && cameraDelBanco.id);
    const datiCamera = (spenta && cameraDelBanco.dati) || {};
    const delProdotto = c => {
      const parti = c.chiave.split('.');
      const pezzo = pb && pb[c.pezzo];
      return (parti.length === 2 && pezzo && pezzo.campi) ? (pezzo.campi[parti[1]] || null) : null;
    };
    const fonte = x => {
      if (!x) return '';
      /*  La voce riconosciuta propone un numero, e il numero si vede: «proposto dal catalogo» da solo non diceva quale. */
      if (x.fonte === 'catalogo' && x.valore != null)
        return ' <span style="font-size:12px;opacity:.7">' + MF('Pag_Banco_CatalogoPropone', cifra(x.valore)) + '</span>';
      const parola = PAROLA_FONTE_BANCO[x.fonte];
      return ' <span style="font-size:12px;opacity:.7">' + esc(parola ? T(parola) : x.fonte) +
        (x.fonte === 'dichiarato' && x.catalogo != null ? ' · ' + MF('Pag_Banco_CatalogoProponeva', cifra(x.catalogo)) : '') +
        '</span>';
    };
    const riga = c => {
      const etichetta = esc(T(PAROLA_CAMPO_BANCO[c.chiave]));
      const unita = c.unita && c.unita !== 'f/' ? ' ' + esc(c.unita) : '';
      let valore;
      if (c.chiave === 'red') {
        const r = pb && pb.ottica && pb.ottica.riduttore;
        valore = r ? '<b>' + esc(r.voce || r.fattore) + '</b>'
                   : '<span style="opacity:.7">' + esc(T('Pag_Banco_RedDaFocale')) + '</span>';
      } else if (c.provenienza === 'riconoscimento') {
        const id = c.chiave + '.id';
        const pezzo = pb && pb[c.pezzo];
        /*  LA CAMERA RICONOSCIUTA PRIMA DELLA DOMANDA: la voce nel campo, in grigio quando nessuno l'ha scritta; e dal 18
         *  settembre 2026, quando la voce scritta non e' la camera collegata, il disaccordo in giallo coi due nomi — vale
         *  la collegata. «Collegata (…): riconosciuta come (…)» diceva che la collegata era stata riconosciuta come una
         *  cosa che non e'. La voce scritta resta nel campo com'e': serve quando la camera sara' scollegata. */
        const rc = c.pezzo === 'camera' ? cameraDelBanco : null;
        const segnaposto = rc && rc.id && dichiarato[id] == null ? ' placeholder="' + esc(rc.voce || rc.id) + '"' : '';
        const notaCamera = !rc ? ''
          : rc.disaccordo
            ? '<div style="font-size:12px">' + disaccordoDellaCamera(rc.disaccordo) + '</div>'
          : rc.id
            ? ' <span style="font-size:12px;opacity:.75">' + MF('Pag_Banco_Riconosciuto', esc(rc.voce || rc.id)) +
              (rc.via === 'nome_e_geometria' ? ' ' + MF('Pag_Banco_CamDalDriver') : '') + '</span>'
          : rc.chiesto
            ? '<div style="font-size:12px;color:#e0a030">' + MF('Pag_Banco_CamVoceSconosciuta', esc(rc.chiesto)) + '</div>'
          : '';
        valore = '<input data-banco="' + esc(id) + '" value="' + escOVuoto(dichiarato[id]) + '"' + segnaposto +
          ' list="voci-' + esc(c.pezzo) + '" autocomplete="off" style="width:170px" spellcheck="false">' + notaCamera +
          (!rc && pezzo && pezzo.voce && (c.pezzo !== 'camera' || pezzo.id)
            ? ' <span style="font-size:12px;opacity:.7">' + MF('Pag_Banco_Riconosciuto', esc(pezzo.voce)) +
              (pezzo.riconoscimento === 'nome_e_geometria' ? ' ' + MF('Pag_Banco_CamDalDriver') : '') + '</span>' : '') +
          (c.pezzo === 'camera' && pezzo && !pezzo.id && !(rc && rc.chiesto)
            ? '<div style="font-size:12px;color:#e0a030">' + MF('Pag_Banco_CamNonRiconosciuta') + '</div>' : '') +
          (c.pezzo === 'camera' && pezzo && pezzo.buio ? '<div style="font-size:12px;opacity:.7">' + (pezzo.buio.fonte
            ? MF('Pag_Banco_Buio', cifra(pezzo.buio.e_pixel_s), cifra(pezzo.buio.temperatura_c),
                 esc(pezzo.buio.sensore || ''), pezzo.buio.modello ? cifra(pezzo.buio.modello.raddoppio_c) : '')
            : MF('Pag_Banco_BuioSenzaFonte', cifra(pezzo.buio.e_pixel_s))) + '</div>' : '');
      } else if (c.provenienza === 'nina') {
        const v = nina[c.chiave];
        valore = '<b style="font-size:15px">' + (v == null ? '—' : (c.unita === 'f/' ? 'f/' : '') + cifra(v, c.unita === 'f/' ? 2 : undefined)) + '</b>' + unita +
          ' <span style="font-size:12px;opacity:.7">' + esc(T('Pag_Banco_Fonte_nina')) + '</span>';
      } else if (c.provenienza === 'dichiarabile') {
        /*  LA SPIEGAZIONE DEL CAMPO la manda il motore (regia, 18 settembre 2026): nel tooltip del campo,
         *  della proposta di catalogo e della «i» accanto. Prima qui c'era un'unita' sola — «frazione lineare» — e per due
         *  campi una nota scritta dal Ponte. L'effetto dell'ostruzione — l'area persa e il diametro equivalente — si
         *  scrive come arriva: qui non si calcola. */
        const spiega = spiegazioneDi(c);
        const titolo = spiega ? ' title="' + esc(spiega) + '"' : '';
        const p = delProdotto(c);
        valore = '<input data-banco="' + esc(c.chiave) + '" data-numero="1" value="' + escOVuoto(dichiarato[c.chiave]) +
          '"' + titolo + ' style="width:70px" spellcheck="false">' + unita + '<span' + titolo + '>' + fonte(p) + '</span>' +
          (spiega ? ' <span class="ico spiega" tabindex="0"' + titolo + '>i</span>' : '') +
          (p && p.perdita_area_pct != null && p.diametro_equivalente_mm != null
            ? '<div style="font-size:12px;opacity:.7">' +
              MF('Pag_Banco_OstruzioneEffetto', cifra(p.perdita_area_pct), cifra(p.diametro_equivalente_mm)) + '</div>' : '');
      } else if (c.provenienza === 'descrizione') {
        /*  LA CAMERA FUORI CATALOGO (17 settembre 2026): i campi del modulo «su misura» di AIS, sotto la voce, col loro
         *  titolo davanti al primo. La matrice si sceglie; il resto si scrive, e i numeri li giudica il servizio. */
        const titolo = (c.chiave === primaDescritta
          ? '<tr><td colspan="2" style="padding-top:.8em"><b>' + esc(T('Pag_Banco_CamDescrittaTitolo')) + '</b>' +
            '<div style="font-size:12px;opacity:.7">' + MF(spenta ? 'Pag_Banco_CamDescrittaSpenta' : 'Pag_Banco_CamDescrittaNota') +
            '</div></td></tr>' : '') +
          /*  i campi facoltativi dicono che cosa costano, col numero misurato (regia, 17 settembre 2026) */
          (c.chiave === 'cam.rumore_lettura_e' && !spenta
            ? '<tr><td colspan="2"><div style="font-size:12px;color:#e0a030">' + MF('Pag_Banco_CamDescrittaCosto') + '</div></td></tr>' : '');
        /*  SPENTA, CON I DATI DELLA CAMERA RICONOSCIUTA (17 settembre 2026): i campi non si scrivono, e mostrano quello che
         *  Strategy usa; salvando tengono quello che era dichiarato. */
        const k = c.chiave.split('.')[1];
        const mostrato = spenta ? (datiCamera[k] == null ? '' : typeof datiCamera[k] === 'number' ? cifra(datiCamera[k]) : esc(datiCamera[k]))
          : escOVuoto(dichiarato[c.chiave]);
        const scelta = spenta ? datiCamera[k] : dichiarato[c.chiave];
        const spento = spenta ? ' disabled class="spento"' : '';
        if (c.chiave === 'cam.matrice')
          valore = '<select data-banco="cam.matrice"' + spento + '><option value=""></option>' + ['colore', 'mono'].map(m =>
            '<option value="' + m + '"' + (scelta === m ? ' selected' : '') + '>' + esc(T(PAROLA_MATRICE[m])) +
            '</option>').join('') + '</select>';
        else
          valore = '<input data-banco="' + esc(c.chiave) + '"' + (c.unita ? ' data-numero="1"' : '') + spento +
            ' value="' + mostrato + '" style="width:' + (c.unita ? '70' : '170') + 'px" spellcheck="false">' + unita;
        return titolo + '<tr><th>' + etichetta + '</th><td>' + valore + '</td></tr>';
      } else return '';
      return '<tr><th>' + etichetta + '</th><td>' + valore + '</td></tr>';
    };
    const campoDi = d => campiDelBanco.find(c => c.pezzo === d.pezzo && c.chiave.split('.')[1] === d.campo);
    const divergenza = d => {
      const parola = PAROLA_DIVERGENZA_BANCO[d.codice];
      if (!parola) return '<li>' + esc(d.codice) + '</li>';
      if (d.codice === 'dichiarato_diverso_dal_catalogo') {
        const c = campoDi(d);
        const nome = c && PAROLA_CAMPO_BANCO[c.chiave] ? T(PAROLA_CAMPO_BANCO[c.chiave]) : d.campo;
        const u = c && c.unita ? ' ' + c.unita : '';
        return '<li>' + MF(parola, esc(nome), cifra(d.dichiarato), cifra(d.catalogo), esc(d.voce), esc(u)) + '</li>';
      }
      if (d.codice === 'descrizione_non_usata') {
        const nomi = (d.campi || []).map(k => PAROLA_CAMPO_BANCO['cam.' + k] ? T(PAROLA_CAMPO_BANCO['cam.' + k]) : k);
        return '<li>' + MF(parola, esc(d.voce), esc(nomi.join(', '))) + '</li>';
      }
      if (d.codice === 'geometria_diversa_dal_catalogo') {
        const pc = PAROLA_CAMPO_CAMERA[d.campo];
        return '<li>' + MF(parola, esc(pc ? T(pc) : d.campo), cifra(d.driver), esc(d.voce), cifra(d.catalogo)) + '</li>';
      }
      if (d.codice === 'focale_diversa_dal_catalogo')
        return '<li>' + MF(parola, cifra(d.nina), cifra(d.catalogo), esc(d.voce), cifra(d.riduttore)) + '</li>';
      const a = d.apertura || {}, n = d.nina || {};
      const fa = PAROLA_FONTE_BANCO[a.fonte];
      return '<li>' + MF(parola, cifra(a.valore), esc(fa ? T(fa) : a.fonte), cifra(n.focale_mm), cifra(n.rapporto, 2),
        esc(n.apertura_mm)) + '</li>';
    };
    const divergenze = (pb && pb.divergenze) || [];
    box.innerHTML = '<div class="box"><b>' + esc(T('Pag_BancoTitolo')) + '</b>' +
      (bancoLetto && bancoLetto.nota ? '<div style="margin:.6em 0;opacity:.85">&#9888; ' + esc(bancoLetto.nota) + '</div>' : '') +
      '<div style="margin:.4em 0;opacity:.7;font-size:12.5px">' + MF('Pag_BancoNota') + '</div>' + avvisoOrfane +
      '<table style="width:100%">' + campi.map(riga).join('') + '</table>' +
      (divergenze.length ? '<ul style="margin:.6em 0 0 1.1em;padding:0;color:#e0a030">' + divergenze.map(divergenza).join('') + '</ul>' : '') +
      '<div style="margin-top:.7em"><button id="salvaBanco">' + esc(T('Pag_SalvaBanco')) + '</button>' +
      esito('esitoBanco') + '</div></div>';
    /*  un campo toccato dopo il salvataggio: quello che si vede non e' piu' quello salvato */
    box.oninput = box.onchange = ev => {
      if (ev.target && ev.target.hasAttribute && ev.target.hasAttribute('data-banco'))
        segnaEsito('esitoBanco', 'attesa', T('Pag_NonSalvato'));
    };
    const salva = $('salvaBanco');
    if (salva) salva.addEventListener('click', () => {
      const valori = {};
      Array.prototype.forEach.call(box.querySelectorAll('input[data-banco], select[data-banco]'), i => {
        const chiave = i.getAttribute('data-banco');
        /*  un campo spento mostra la camera riconosciuta: si salva quello che era dichiarato, non quello che si vede */
        valori[chiave] = i.disabled ? (dichiarato[chiave] != null ? dichiarato[chiave] : null)
          : valoreScritto(i.value, i.hasAttribute('data-numero'));
      });
      segnaEsito('esitoBanco', 'corso', T('Pag_Salvo'));
      chiedi('salvaBanco', { banco: valori }).then(r2 => {
        if (r2.ok) segnaEsito('esitoBanco', 'ok', T('Pag_SalvatoAlle').replace('{0}', oraDiAdesso()));
        else segnaEsito('esitoBanco', 'no', T('Pag_NonSalvatoPerche').replace('{0}', r2.messaggio || r2.codice || ''));
        ritiraDalloSchermo(r2.ritirata);
        if (r2.ok) chiedi('banco').then(r3 => { if (r3.ok) { bancoLetto = r3; disegnaBanco(); riconosciLaCamera(); } });
      });
    });
  }

  /*  «USA INVECE LA VOCE SCRITTA» (regia, 18 settembre 2026) vale per la richiesta che fa partire e basta: e' un
   *  argomento di questa chiamata, non uno stato della pagina — niente che resti acceso e invecchi. */
  async function vai(domanda) {
    const soloVoce = !!(domanda && domanda.voceScritta === true);
    /*  Senza oggetto non si chiede niente: si dice, e si torna al campo. */
    if (!$('oggetto').value.trim()) {
      stato(T('Pag_ScriviOggetto'), 'no');
      $('oggetto').focus();
      return;
    }
    $('vai').disabled = true;
    stato(T('Pag_StoChiedendo'));
    $('uscita').innerHTML = '';
    $('dettagli').innerHTML = '';
    parzialeUsato = null;
    aggiornaProvenienzeDelSito();

    const r = await chiedi('prescrizione', {
      /* IL SITO E' QUELLO DEL PROFILO, non piu' sette numeri scritti qui dentro.
         Se manca qualcosa manca davvero: nessun ripiego, nessun valore di serie. E solo i campi che il Ponte offre: un
         campo spento non parte mai (sitoDaMandare). */
      sito:      sitoDaMandare(),
      /*  IL BANCO IN TRE PEZZI (16 settembre 2026): quello che N.I.N.A. tiene — focale e rapporto, riletti ogni volta —,
       *  quello che il catalogo del motore riconosce dall'identificativo dichiarato, e quello che chi riprende dichiara
       *  perche' non ce l'ha nessuno dei due. Si compone dalla lista dei campi che il servizio pubblica, non da una lista
       *  di qui; la camera e' quella che il driver dichiara. Dedurre «askar71f» dal nome di un dispositivo sarebbe
       *  indovinare l'identita' fisica da un'etichetta: l'identificativo lo dichiara chi riprende, come per i filtri. */
      banco:     bancoDaMandare(soloVoce),
      bersaglio: { id: $('oggetto').value.trim() },
      /*  LE TRE DICHIARAZIONI, e adesso vengono dai controlli.
       *  `notti` era il letterale 3 e la data era cablata nel markup: due numeri
       *  che nessun utente aveva scelto, su cui pero' il piano si costruiva.
       *  Si manda quello che c'e' scritto, senza correggerlo: se e' fuori scala lo
       *  dice il servizio, che ha la lista chiusa dei rifiuti. */
      /*  SI MANDA QUELLO CHE C'E' SCRITTO, senza convertirlo. `Number('due')` da'
       *  NaN, e JSON lo scrive `null`: un valore malformato diventava un'ASSENZA,
       *  e il servizio applicava il proprio predefinito invece di rifiutare. Il
       *  Ponte trasporta; a giudicare se sono notti valide e' Strategy, che ha la
       *  lista chiusa dei rifiuti e sa dire perche'. */
      quando:    { data: $('data').value.trim(),
                   notti: $('notti').value.trim() },
      /*  L'IDENTIFICATIVO E NIENT'ALTRO. Non un numero di secondi, non una soglia:
       *  che cosa comporti questo modo lo decide Strategy. Quando l'elenco non e'
       *  arrivato la chiave non parte affatto, e il servizio applica il proprio
       *  predefinito — che e' sempre stato compito suo. */
      /*  LA COPERTURA SI DICHIARA, I RIQUADRI NO.
       *  Qui partiva `pannelli: 1` e la copertura non partiva affatto: il motore
       *  legge l'assenza come «soggetto completo», quindi la richiesta diceva
       *  insieme «copri tutto il soggetto» e «il progetto e' un campo solo».
       *  I riquadri sono GEOMETRIA — dipendono da come il sensore cade sul cielo e
       *  da quanto hai ruotato — e una pagina senza inquadratura non puo'
       *  calcolarli: non li manda, e Strategy dichiara quanti ne ha assunti. */
      /*  E la politica di sessione e la strada del menu, con la stessa regola: si mandano quando ci sono, e quando
       *  mancano sceglie il servizio. */
      opzioni:   Object.assign({ copertura: coperturaScelta() },
                               modoScelto ? { strategia: modoScelto } : {},
                               politicaScelta ? { politica: politicaScelta } : {},
                               stradaScelta ? { strada: stradaScelta } : {})
    });

    $('vai').disabled = false;

    if (!r.ok) {
      stato(T('Pag_NonRiuscita'), 'no');
      $('dettagli').innerHTML = '';
      $('uscita').innerHTML = disegnaRifiuto(r, soloVoce);
      return;
    }

    stato(T('Pag_RispostaRicevuta'), 'ok');

    /* `corpo` e' la risposta del servizio parola per parola: la si legge qui, e il
       fatto che si legga e' la prova che ha attraversato il ponte intatta. */
    const d = JSON.parse(r.corpo);
    const p = d.prodotto;
    /*  la Luna sotto la data e' quella della notte che il piano usa davvero */
    notteUsata = p.notte.usata;
    lunaDellaData();

    const riquadro = riquadroDelPiano(p, r, d);

    /*  IL CUORE IN ALTO (17 settembre 2026): sotto la domanda, subito, la tecnica di ripresa e le notti da mandare a
     *  N.I.N.A. (`uscita`); il resto della risposta — oggetto, notte, verdetto, filtri, contratto, dati assunti — sta
     *  sotto, in `dettagli`, e sopra sito, banco e filtri. */
    $('dettagli').innerHTML =
      '<div class="box"><table>' +
      '<tr><th>' + T('Pag_ColOggetto') + '</th><td>' + identita(p.bersaglio, p.bersaglio.scheda) + '</td></tr>' +
      '<tr><th>' + T('Pag_ColNotte') + '</th><td>' +
        MF('Pag_NotteChiestaUsata', esc(p.notte.chiesta), esc(p.notte.usata)) +
        (p.notte.spostataDi ? ' <span style="opacity:.55">' +
          MF('Pag_NotteSpostata', p.notte.spostataDi) + '</span>' : '') +
        '</td></tr>' +
      /*  Le ore utili sono la somma delle notti chieste, e il numero delle notti lo dice il servizio: senza, accanto
          alla notte chiesta, sembravano le ore di una notte sola. */
      '<tr><th>' + T('Pag_RigaOreUtili') + '</th><td>' + (p.notte.nottiDisponibili == null
        ? cifra(p.notte.oreDisponibili, 2) + ' h'
        : MF(p.notte.nottiDisponibili === 1 ? 'Pag_OreUtiliInUnaNotte' : 'Pag_OreUtiliInNotti',
             cifra(p.notte.oreDisponibili, 2), cifra(p.notte.nottiDisponibili))) + '</td></tr>' +
      /*  IL VERDETTO CON LE SUE NOTTI (regia, 16 settembre 2026): quanto del progetto coprono le notti chieste, e quante
          ne servono per il minimo. I numeri li fa Strategy; senza, la riga non c'e'. */
      (x => x ? '<tr><th>' + T('Pag_RigaVerdetto') + '</th><td>' + x + '</td></tr>' : '')(verdettoIntero(p.prescrizione)) +
      rigaDelCampionamento(p) +
      /*  SU QUALI VETRI E' STATA CALCOLATA, e non e' un dettaglio da nascondere.
         Senza questa riga «non e' cambiato niente perche' il motore avrebbe scelto
         gli stessi vetri» e «non e' cambiato niente perche' la ruota non e' partita»
         si somigliano troppo — e la prima volta ci ha fregati per mezz'ora. */
      '<tr><th>' + T('Pag_RigaCalcolataSu') + '</th><td>' +
        (r.ruotaAggiunta && r.ruotaAggiunta.length
          ? MF('Pag_CalcolataTuaRuota', r.ruotaAggiunta.map(esc).join(' · '))
          : '<span style="color:#e0a030">' + MF('Pag_CalcolataDiSerie') + '</span>') +
        /*  I FILTRI DEL PROGETTO: con un progetto aperto il motore calcola la strada intera, anche coi filtri che stanotte
         *  non sono in ruota; senza questa riga «la tua ruota» direbbe meno filtri di quelli del calcolo. */
        (x => x.length ? '<div style="margin-top:4px">' + MF('Pag_CalcolataColProgetto', x.map(esc).join(' · ')) + '</div>' : '')(
          [...new Set((p.saltati_stanotte || []).map(s => s.filtro))]) +
        /*  LE ORFANE SI DICONO QUI, alla richiesta, una per riga: un filtro dichiarato e poi rinominato o tolto in
         *  N.I.N.A. non e' partito, e senza questa riga lo si scoprirebbe solo al rifiuto della consegna. */
        (r.orfane || []).map(o => '<div style="color:#e0a030;margin-top:4px">' +
          MF('Pag_RuotaOrfana', '«' + esc(o.nina) + '»' + (o.id ? ' (' + esc(o.id) + ')' : '')) + '</div>').join('') +
        '</td></tr>' +
      '<tr><th>' + T('Pag_RigaContratto') + '</th><td>' +
        MF('Pag_Contratto',
           '<code>' + esc(d.contratto) + '</code>',
           d.misura ? cifra(d.misura.ms) + ' ms' : '—',
           (r.corpo.length / 1024).toFixed(0)) + '</td></tr>' +
      '</table></div>' +
      parzialeDelProdotto(p.parziale) +
      notaDeiRiferimenti(p.parziale);
    $('uscita').innerHTML =
      disegnaMenu(p.prescrizione) +
      (p.banco && p.banco.camera ? cameraDelCalcolo(p.banco.camera, soloVoce) : '') +
      nottiGiuste(p) +
      riquadro +
      bloccoDelProgetto(p, r) +
      (r.consegnabile ? '' :
        '<div class="box err"><b>' + T('Pag_NonSiPuoMandare') + '</b>' +
        '<div style="margin-top:6px;opacity:.85">' +
        esc(r.perche || T('Pag_MotivoNonDichiarato')) + '</div></div>') +
      '<div id="consegna"></div>';
    /*  Il banco che questa prescrizione ha usato, campo per campo con la sua provenienza e le divergenze. */
    bancoUsato = p.banco || null;
    disegnaBanco();
    parzialeUsato = p.parziale || null;
    aggiornaProvenienzeDelSito();

    for (const b of document.querySelectorAll('button.manda'))
      b.addEventListener('click', () => manda(b));
    /*  APRI UN PROGETTO NUOVO: il Ponte toglie il profilo congelato, e la domanda si rifa' — torna proposta, e la prossima
     *  consegna apre il progetto nuovo. */
    const nuovo = $('nuovoProgetto');
    if (nuovo) nuovo.addEventListener('click', async () => {
      nuovo.disabled = true;
      segnaEsito('esitoProgetto', 'corso', T('Pag_Salvo'));
      const r2 = await chiedi('nuovoProgetto', null, {});
      nuovo.disabled = false;
      if (!r2.ok) { segnaEsito('esitoProgetto', 'no', T('Pag_NonSalvatoPerche').replace('{0}', r2.messaggio || r2.codice || '')); return; }
      vai();
    });
    /*  LE NOTTI GIUSTE: il tasto mette la data proposta e rifa' la domanda. La strada scelta non vale piu', come quando
     *  la data la cambia chi scrive. */
    const sposta = $('spostaData');
    if (sposta) {
      sposta.addEventListener('click', () => {
        $('data').value = sposta.getAttribute('data-data');
        stradaScelta = null;
        vai();
      });
    }
    /*  IL CLIC SU UNA STRADA RIFA' LA DOMANDA con quella strada, e basta: ore, pose e sequenze le ricalcola
     *  Strategy. Qui non si sposta niente da una carta all'altra. */
    for (const c of document.querySelectorAll('#menu [data-strada]'))
      c.addEventListener('click', () => {
        stradaScelta = c.getAttribute('data-strada') === 'auto' ? null : c.getAttribute('data-strada');
        vai();
      });
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
      MF('Pag_BlocchiPose', cifra(r.blocchi), cifra(r.pose)) + ' ' +
      '<span style="opacity:.7">' + T('Pag_NienteAvviato') + '</span></div>' +
      elenco(T('Pag_Scartato'), r.scartati) + elenco(T('Pag_DaSapere'), r.note) +
      /*  la prima consegna ha aperto il progetto: da qui il profilo e' congelato */
      (r.progettoAperto ? '<div style="margin-top:6px">' + MF('Pag_Progetto_ApertoOra') + '</div>' : '') +
      (r.progettoNonSalvato ? '<div style="margin-top:6px;color:#e0a030">' + MF('Pag_Progetto_NonSalvato', esc(r.progettoNonSalvato)) + '</div>' : '') +
      '</div>';
  }

  /*  QUALE COPERTURA E' SPUNTATA. Due segmenti, e uno lo e' sempre: il markup
      preseleziona «soggetto completo» come fa AIS. Se un giorno nessuno lo fosse,
      si manda null e il servizio lo dichiara come non dichiarato. */
  const coperturaScelta = () => {
    const s = document.querySelector('input[name="cov"]:checked');
    return s ? s.value : null;
  };

  /*  LA DATA DI OGGI, messa dalla pagina e non dal markup. Una data scritta nel
      file e' una data che mente il giorno dopo, e la Luna si calcola su quella:
      il pannello mostrerebbe la fase di un'altra sera senza dirlo. */
  (function dataDiOggi() {
    const d = new Date();
    const p = n => String(n).padStart(2, '0');
    $('data').value = d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate());
  })();

  $('vai').addEventListener('click', vai);
  /*  Una strada e' di una scheda e di una notte: cambiando oggetto, data o notti la scelta non vale piu', e la
   *  prossima domanda torna a far scegliere il motore. */
  for (const campo of ['oggetto', 'data', 'notti'])
    $(campo).addEventListener('input', () => { stradaScelta = null; });
  /*  una data nuova e' una notte nuova: la Luna si richiede, e la notte usata della risposta di prima non vale piu' */
  $('data').addEventListener('input', () => { notteUsata = null; lunaDellaData(); });

  /*  IL CALENDARIO SI APRE DALL'ICONA (17 settembre 2026): nella riga della domanda il tasto nativo non stava piu' sotto
   *  l'icona, e il clic andava a vuoto. L'icona apre la scelta della data da se'; dove il browser non sa farlo, il campo
   *  prende il fuoco. */
  /*  «usa invece la voce scritta», ovunque compaia: nel banco, nella risposta, nel rifiuto */
  document.addEventListener('click', ev => {
    const t = ev.target && ev.target.closest ? ev.target.closest('[data-usa-voce-scritta]') : null;
    if (t) vai({ voceScritta: true });
  });

  const iconaData = document.querySelector('.data-cal svg');
  if (iconaData) {
    iconaData.addEventListener('click', () => {
      try { $('data').showPicker(); } catch (e) { $('data').focus(); }
    });
  }

  /*  LA LUNA SOTTO LA DATA, come in AIS (17 settembre 2026): la fase e l'altezza a meta' del buio astronomico, dal sito
   *  del profilo. Il conto e' di Strategy, e la pagina lo chiede all'ospite quando cambiano la data o il sito. Dopo una
   *  risposta, se la notte usata non e' quella chiesta, la Luna e' quella della notte usata, con la freccia. Vince
   *  l'ultima domanda. */
  let ultimaLuna = 0, notteUsata = null;
  function lunaDellaData() {
    const box = $('lunaNotte');
    const chiesta = $('data').value;
    const giorno = notteUsata && notteUsata !== chiesta ? notteUsata : chiesta;
    const mia = ++ultimaLuna;
    if (!box) return;
    if (!sito || sito.lat == null || sito.lon == null || !giorno) { box.textContent = ''; return; }
    chiedi('luna', null, { data: giorno, lat: sito.lat, lon: sito.lon }).then(r => {
      if (mia !== ultimaLuna) return;
      let l = null;
      try { l = r.ok ? JSON.parse(r.corpo).luna : null; } catch (e) { l = null; }
      if (!l) { box.textContent = ''; return; }
      const freccia = giorno !== chiesta
        ? '<span class="notte-usata">' + MF('Pag_NotteUsata', esc(dataScritta(giorno, { weekday: 'short', day: 'numeric', month: 'short' }))) + '</span> '
        : '';
      box.innerHTML = freccia + (l.sopra
        ? MF('Pag_LunaAlta', cifra(l.fasePercento), cifra(l.altezza_deg, 0))
        : MF('Pag_LunaSotto', cifra(l.fasePercento)));
    });
  }

  /*  L'OGGETTO SI TROVA MENTRE SI SCRIVE, come in AIS (17 settembre 2026: IC 435 dal Ponte non si trovava). A ogni
   *  tasto, dopo una breve pausa, la pagina chiede all'ospite `cerca`, e l'elenco sotto il campo si riempie con le
   *  corrispondenze del servizio, nello stesso ordine della pagina di AIS; con meno di due lettere, le schede complete.
   *  La pagina non cerca da sola: il catalogo e' del motore. Vince l'ultima domanda: una risposta arrivata tardi non
   *  copre quella del testo di adesso. Accanto al nome, l'alias che contiene quello che si e' scritto — «horse» e'
   *  IC 434 perche' si chiama Horsehead, e va detto — o i primi due, poi il tipo e la costellazione. */
  let ultimaRicerca = 0, pausaRicerca = null;
  function riempiElencoOggetti() {
    const mia = ++ultimaRicerca;
    const scritto = $('oggetto').value;
    const piatto = s => String(s).toLowerCase().replace(/[^\p{L}\p{N}]+/gu, '');
    chiedi('cerca', null, { q: $('oggetto').value }).then(r => {
      if (mia !== ultimaRicerca || !r.ok) return;
      let risultati;
      try { risultati = JSON.parse(r.corpo).risultati || []; } catch (e) { return; }
      $('elencoOggetti').innerHTML = risultati.map(x => {
        const alias = x.alias || [];
        const suo = piatto(scritto).length >= 2 && alias.find(a => piatto(a).indexOf(piatto(scritto)) >= 0);
        const parti = [suo || alias.slice(0, 2).join(', '), x.scheda ? T('Pag_CercaScheda') : x.tipo, x.costellazione];
        if (x.daCollaudare) parti.push(T('Pag_CercaDaCollaudare'));
        return '<option value="' + esc(x.nome) + '">' + esc(parti.filter(Boolean).join(' · ')) + '</option>';
      }).join('');
    });
  }
  $('oggetto').addEventListener('input', () => {
    clearTimeout(pausaRicerca);
    pausaRicerca = setTimeout(riempiElencoOggetti, 120);
  });
  riempiElencoOggetti();

  /*  «PULISCI», come in AIS (17 settembre 2026): svuota il campo e riporta l'elenco alle schede complete; compare solo
   *  quando c'e' qualcosa da pulire. Anche con Esc. */
  function aggiornaPulisci() { $('pulisci').hidden = !$('oggetto').value; }
  function pulisci() {
    $('oggetto').value = '';
    stradaScelta = null;
    aggiornaPulisci();
    riempiElencoOggetti();
    $('oggetto').focus();
  }
  $('pulisci').addEventListener('click', pulisci);
  $('oggetto').addEventListener('input', aggiornaPulisci);
  $('oggetto').addEventListener('keydown', e => { if (e.key === 'Escape' && $('oggetto').value) pulisci(); });
  aggiornaPulisci();

  /*  LA FRASE DELL'OSPITE VINCE SU QUELLA GENERICA: l'ospite compone gia'
      «Nessuno risponde a <indirizzo>» con la radice davvero in uso, e la pagina la
      buttava via per scrivere «servizio non raggiungibile», che non dice DOVE. */
  chiedi('salute').then(r => stato(
    r.ok ? T('Pag_ServizioSu') : (r.messaggio || T('Pag_ServizioGiu')),
    r.ok ? 'ok' : 'no'));

  /* LA RUOTA VIRTUALE: che cosa sono, fisicamente, i vetri che hai in ruota.
     N.I.N.A. da' i nomi e gli slot; il motore da' l'elenco dei vetri che conosce;
     in mezzo ci sei tu, che dichiari quale e' quale. Nessuna regola puo' indovinarlo:
     «HA» puo' stare davanti a un L-Ultimate, e solo chi l'ha comprato lo sa. */
  /* Quale vetro finira' davvero in sequenza per questo blocco: lo dice il motore in
     `posa.<canale>.ex.spec.filter.id`, e la dichiarazione lo traduce nel nome che hai
     scritto tu sulla ruota. Se non e' dichiarato lo si dice qui, invece di lasciare
     credere che andra' bene: e' lo stesso rifiuto che poi farebbe la consegna. */
  /*  IL GUADAGNO DEL BLOCCO, come la sequenza lo imposta (regia, 16 settembre 2026): il motore sceglie il modo, il
   *  pannello lo mostra, la sequenza lo imposta. Col guadagno, il modo e chi l'ha deciso; un blocco a -1 lascia il
   *  guadagno che la camera ha. I numeri sono quelli del modello, scritti come arrivano. */
  /*  IL RIQUADRO DEL PIANO — 21 settembre 2026.
   *  ═════════════════════════════════════════════════════════════════════════════════════════════════════
   *  Qui c'era una tabella: una riga per notte, una colonna per grandezza. Ed era SBAGLIATA, non brutta. Una
   *  grandezza che varia per canale — il filtro, il guadagno, la posa — dentro una cella per notte non ha dove
   *  andare, e si ripete tante volte quanti sono i canali: «100 (HCG, scelto dal motore)» sei volte di fila. Non era
   *  un difetto dentro la tabella: era la tabella, e si sarebbe ripresentato su ogni campo nuovo, perche' il
   *  contenitore non sapeva dire la forma del dato. Se un difetto si ripete non si corregge il caso: si toglie la
   *  possibilita' — e la possibilita' era la cella.
   *
   *  LA FORMA E' QUELLA DI AIS: notte → canali → riga della posa → totale e orologio.
   *
   *  LE REGOLE DI QUESTO RIQUADRO, ognuna col suo perche':
   *    · un campo che non arriva e' una riga che non si disegna: niente zeri, niente trattini ricavati. Un numero
   *      inventato dove il servizio tace e' la bugia piu' difficile da scoprire, perche' ha l'aria di un dato;
   *    · si compatta solo cio' che e' IDENTICO, e la parola che dice su che cosa e' il numero non la sceglie il
   *      pannello: «per banda» si scrive solo unendo blocchi che la risposta dichiara `perBanda`;
   *    · nessuna frase arriva fatta: si compone dai codici, nelle due lingue del dizionario;
   *    · una risposta con un contratto piu' nuovo si disegna per quello che si riconosce, e lo si dice.         */
  const CONTRATTO_CONOSCIUTO = 1;
  /*  LE ORE IN ORE E MINUTI, COME IN AIS — e il minuto lo manda il motore (21 settembre 2026). Il pannello non lo
   *  ricava: sarebbe un numero che nessuno gli ha mandato, e due arrotondamenti dello stesso tempo in due posti si
   *  dividono di un minuto sul mezzo minuto esatto — misurato, su 888 notti succedeva 21 volte. Arriva in pezzi,
   *  `{ore, minuti}`, arrotondati una volta sola; sotto l'ora si scrivono i soli minuti, come in AIS.
   *  Se i pezzi non arrivano — un servizio piu' vecchio — si scrive il decimale che c'e': lo stesso numero, in un'altra
   *  forma, non un numero inventato. */
  const oreScritte = (pezzi, h) => {
    if (pezzi && pezzi.ore != null && pezzi.minuti != null)
      return pezzi.ore > 0 ? MF('Pag_OreMinuti', cifra(pezzi.ore), String(pezzi.minuti).padStart(2, '0'))
                           : MF('Pag_SoloMinuti', cifra(pezzi.minuti));
    return (h == null || !isFinite(h)) ? '—' : cifra(h, 2) + ' h';
  };
  /*  LE CHIAVI SONO LETTERALI, come per l'affidabilita' e la classe: una chiave composta col codice la prova delle
   *  voci orfane non la vede. Un codice che la mappa non conosce non si disegna — si tace, non si indovina. */
  const PAROLA_DEL_LIMITE = { progetto: 'Pag_Limite_progetto', colore_stellare: 'Pag_Limite_colore_stellare',
    strategia: 'Pag_Limite_strategia', stella_protetta: 'Pag_Limite_stella_protetta',
    stella_scoperta: 'Pag_Limite_stella_scoperta', fondo_satura: 'Pag_Limite_fondo_satura',
    soggetto_satura: 'Pag_Limite_soggetto_satura', pose_minime: 'Pag_Limite_pose_minime',
    tetto_di_posa: 'Pag_Limite_tetto_di_posa', pavimento: 'Pag_Limite_pavimento',
    stelle_sature: 'Pag_Limite_stelle_sature', classe: 'Pag_Limite_classe', lettura: 'Pag_Limite_lettura',
    montatura: 'Pag_Limite_montatura' };
  /*  CHE COSA FA LA SERIE NEL SUO CANALE (21 settembre 2026). La serie corta dell'HDR si segna in testa alla riga, e si
   *  segna diversa: senza la parola, «20 s · 25 pose» si leggerebbe come una seconda posa del canale. */
  const PAROLA_DEL_RUOLO = { nucleo: 'Pag_Ruolo_nucleo' };
  const SPIEGAZIONE_DEL_RUOLO = { nucleo: 'Pag_RuoloSpiegazione_nucleo' };
  /*  LA VOCE DI UNA MAPPA, SOLO SE E' SUA. Un codice che coincide con un nome che ogni oggetto eredita — «constructor»,
   *  «toString» — troverebbe una funzione invece di niente, e il disegno di tutta la notte si fermerebbe. Un codice che
   *  la mappa non conosce deve tacere, come ogni altro. */
  const vocePropria = (mappa, codice) =>
    (typeof codice === 'string' && Object.prototype.hasOwnProperty.call(mappa, codice)) ? mappa[codice] : null;
  /*  LA SERIE CORTA SPIEGATA (22 settembre 2026). Qui il nucleo aveva solo la spiegazione generica, mentre la pagina
   *  del motore diceva il conto: le due facce non combaciavano piu'. Il servizio manda, per banda, i pezzi — fin dove
   *  chi brucia resta al sicuro, la posa principale, la serie o la posa unica, e fin dove arriva la parte debole con
   *  l'una e con l'altra forma — e qui si compone la frase, nelle due lingue del dizionario. Chi brucia e perche' una classe
   *  non vuole la serie arrivano come codici: un codice che la mappa non conosce non si dice. Un pezzo che manca e' una
   *  parte della frase che non si scrive; senza pezzi resta la sola spiegazione generica. */
  const PAROLA_DI_CHI_BRUCIA = { soggetto: 'Pag_Serie_Chi_soggetto', nucleo_misurato: 'Pag_Serie_Chi_nucleo_misurato',
    stelle: 'Pag_Serie_Chi_stelle' };
  const SPIEGAZIONE_SENZA_SERIE = { membri_brillanti_a_ogni_posa: 'Pag_Serie_Senza_membri_brillanti_a_ogni_posa' };
  /*  PERCHE' HA DECISO LA CLASSE: la banda non ha la brillanza del soggetto, o non ce l'ha per ogni riga che porta. Era
   *  una ragione di due; la seconda — il filtro a due righe sul sensore a colori — il servizio non la manda piu', perche'
   *  quella posa adesso la ricava (23 settembre 2026), e qui non resta nemmeno la parola. Un codice che la mappa non
   *  conosce non si dice, e resta la serie coi suoi numeri. */
  const RAGIONE_DELLA_CLASSE = { banda_senza_misura: 'Pag_Serie_Classe_banda_senza_misura' };
  /*  IL COLORE CHE SI RIEMPIE PER PRIMO, sul sensore a colori: e' quello a cui si riferisce il numero di «fino a». Si dice
   *  il colore e basta — quale riga cada su quale colore il pezzo non lo dice, e qui non si deduce. */
  const PAROLA_DEL_FOTOSITO = { rosso: 'Pag_Serie_Fotosito_rosso', verde: 'Pag_Serie_Fotosito_verde',
    blu: 'Pag_Serie_Fotosito_blu' };
  /*  IL NUMERO NON PROMETTE PIU' DEL SUO VERSO. Col verso `al_piu` il limite che arriva e' il piu' lungo possibile, e
   *  quello vero puo' stare prima: la frase lo dice. La mappa elenca solo chi dipende da quel verso — il soggetto —; il
   *  limite delle stelle non ne dipende, e per loro resta la parola di sempre. Col verso `almeno` la frase resta quella di
   *  sempre: il numero e' prudente per una ragione e non per tutte, e dire «almeno» prometterebbe di piu'. */
  const PAROLA_DI_CHI_BRUCIA_AL_PIU = { soggetto: 'Pag_Serie_Chi_soggetto_al_piu' };
  const fotositoDi = q => {
    const k = q.chiBrucia === 'soggetto' ? vocePropria(PAROLA_DEL_FOTOSITO, q.tettoFotosito) : null;
    return k ? MF(k) : '';
  };
  function perCheDellaSerie(etichetta, q) {
    if (!q) return '';
    const n = x => cifra(x);
    if (q.decisa === 'classe' && q.serieSec != null && q.seriePose != null) {
      /*  la parola del tetto di classe e' quella che la riga della posa usa gia', col suo segno: e' lo stesso ripiego
       *  dichiarato, detto al suo posto */
      const segno = '<span class="limite classe" title="' + esc(T('Pag_LimiteClasseSpiegazione')) + '">' +
                    esc(T('Pag_Limite_classe')) + '</span>';
      const ragione = vocePropria(RAGIONE_DELLA_CLASSE, q.motivoDiClasse);
      return MF('Pag_Serie_DellaClasse', esc(etichetta), segno, n(q.serieSec), n(q.seriePose)) +
             (ragione ? ' ' + MF(ragione) : '');
    }
    if (q.decisa === 'progetto' && q.serieSec != null && q.seriePose != null)
      return MF('Pag_Serie_DelProgetto', esc(etichetta), n(q.serieSec), n(q.seriePose));
    const chi = q.decisa === 'fisica'
      ? ((q.tettoDaRighe === 'al_piu' ? vocePropria(PAROLA_DI_CHI_BRUCIA_AL_PIU, q.chiBrucia) : null) ||
         vocePropria(PAROLA_DI_CHI_BRUCIA, q.chiBrucia)) : null;
    if (!chi || q.sicuroFinoA == null || q.posaPrincipale == null) return '';
    let f = MF(chi, esc(etichetta), n(q.sicuroFinoA), n(q.magProtetta)) + fotositoDi(q);
    const e = q.equivalenti || {}, eh = q.equivalentiHM || {};
    const pari = q.orologioPari != null ? oreScritte(q.orologioPariHM, q.orologioPari) : null;
    const eqD = e.doppia != null ? oreScritte(eh.doppia, e.doppia) : null;
    const eqU = e.unica != null ? oreScritte(eh.unica, e.unica) : null;
    const rif = q.posaRiferimento != null ? n(q.posaRiferimento) : null;
    if (q.forma === 'unica' && q.posaUnica != null) {
      f += MF('Pag_Serie_Unica', n(q.posaPrincipale), n(q.posaUnica));
      /*  SUL PAREGGIO NON SI CONFRONTA NIENTE: le due cifre sarebbero uguali, e il paragone citerebbe per giunta un
       *  piano che non si farebbe — a quella posa la serie non servirebbe. Il pezzo lo dice, e qui si scrive. */
      f += q.pareggio === true ? MF('Pag_Serie_Pareggio')
         : pari && eqU && rif ? (eqD ? MF('Pag_Serie_PariOreUnica', pari, eqU, rif, eqD) : MF('Pag_Serie_PariOreSoloUnica', pari, eqU, rif)) : '.';
    } else if (q.forma === 'doppia' && q.serieSec != null && q.seriePose != null) {
      f += MF('Pag_Serie_Doppia', n(q.posaPrincipale), n(q.serieSec), n(q.seriePose), n(q.serieDaConsegnare));
      if (q.nonBasta === true) f += MF('Pag_Serie_NonBasta', n(q.serieSec));
      if (pari && eqD && rif)
        f += eqU && q.posaUnica != null ? MF('Pag_Serie_PariOreDoppia', pari, eqD, rif, eqU, n(q.posaUnica))
                                        : MF('Pag_Serie_PariOreSoloDoppia', pari, eqD, rif);
    } else return '';
    return f;
  }
  /*  Le bande coi pezzi IDENTICI si dicono una volta, con le loro sigle insieme — come le voci della posa: R e G di un
   *  globulare hanno spesso lo stesso conto, e ripeterlo due volte seppellirebbe quello che dice. */
  function perCheDelleBande(serie, bande) {
    const pb = (serie && serie.perBanda) || {}, gruppi = [];
    for (const b of bande) {
      const q = Object.prototype.hasOwnProperty.call(pb, b) ? pb[b] : null;
      if (!q) continue;
      const k = JSON.stringify(q), g = gruppi.find(x => x.k === k);
      if (g) g.bande.push(b); else gruppi.push({ k, q, bande: [b] });
    }
    return gruppi.map(g => perCheDellaSerie(g.bande.join('·'), g.q)).filter(Boolean);
  }
  /*  Il testo semplice di un pezzo di HTML composto, per il suggerimento al passaggio del mouse. */
  const soloTesto = h => { const d = document.createElement('div'); d.innerHTML = h; return d.textContent; };
  const PAROLA_DEL_CIELO = { buio: 'Pag_Cielo_buio', luna_non_disturba: 'Pag_Cielo_luna_non_disturba',
    luna_tollerabile: 'Pag_Cielo_luna_tollerabile', luna_pesante: 'Pag_Cielo_luna_pesante', non_usata: 'Pag_Cielo_non_usata' };
  const PAROLA_DEL_RIFERIMENTO = { brillanza_pubblicata: 'Pag_Prof_Rif_brillanza_pubblicata',
    bordo_galassia: 'Pag_Prof_Rif_bordo_galassia' };
  const PAROLA_DEL_REGIME = { fondo: 'Pag_Prof_Regime_fondo', transizione: 'Pag_Prof_Regime_transizione',
    sorgente: 'Pag_Prof_Regime_sorgente' };
  /*  Le tinte dei canali sono le stesse delle pastiglie delle bande, piu' su: un filtro ha lo stesso colore in tutto
   *  il pannello. Il colore aiuta a riconoscere, non porta mai da solo l'informazione: il nome e' sempre scritto. */
  const TINTA_DEL_CANALE = { Ha: 'c-ha', OIII: 'c-oiii', 'Ha+OIII': 'c-oiii', SII: 'c-sii', L: 'c-l',
                             R: 'c-rgb', G: 'c-rgb', B: 'c-rgb', RGB: 'c-rgb' };

  /*  I canali della notte. I gruppi e le ore che il piano da' a ciascuno li dice `piano.nights[].blocks`; i blocchi
   *  della sequenza si appendono al loro gruppo per banda. Un blocco che nessun gruppo reclama diventa un gruppo suo:
   *  si mostra, non si perde. Un gruppo senza blocchi stanotte non ha niente da dire, e non si disegna. */
  function gruppiDellaNotte(nt, blocchi) {
    const gruppi = ((nt && nt.blocks) || []).map(g => ({ id: g.id, bande: g.bands || [g.id], h: g.h, hHM: g.hHM, blocchi: [] }));
    for (const b of blocchi) {
      const c = b.canali || [];
      /*  Il canale lo dice il blocco (`gruppo`): e' l'unico modo di rimettere R, G e B sotto RGB senza sapere da se'
       *  come il servizio li spartisce. Senza quel campo si prova per banda, e un blocco che nessuno reclama fa
       *  gruppo per conto suo. */
      let g = b.gruppo ? gruppi.find(x => x.id === b.gruppo) : gruppi.find(x => c.some(k => k === x.id || x.bande.indexOf(k) >= 0));
      if (!g) { g = { id: b.gruppo || c.join('+') || '—', bande: c, h: null, blocchi: [] }; gruppi.push(g); }
      g.blocchi.push(b);
    }
    return gruppi.filter(g => g.blocchi.length);
  }

  /*  LE VOCI DI UN CANALE, COMPATTATE. Due blocchi diventano una voce sola quando dicono esattamente le stesse cose —
   *  posa, pose di stanotte, pose e ore del canale, chi ha deciso i secondi, guadagno — E quando la risposta dichiara
   *  che quei numeri sono di ciascuna banda. Senza quella dichiarazione il pannello non unisce: unire e scrivere
   *  «per banda» vorrebbe dire decidere di che cosa sia il numero, e non tocca a chi disegna. */
  function vociDelCanale(blocchi) {
    const uguali = (a, b) => a.perBanda === true && b.perBanda === true && a.sec === b.sec && a.n === b.n &&
      a.poseCanale === b.poseCanale && a.oreCanale === b.oreCanale &&
      a.limite === b.limite && a.ruolo === b.ruolo &&
      a.gain === b.gain && a.offset === b.offset && a.gainFonte === b.gainFonte && a.modo === b.modo;
    const voci = [];
    for (const b of blocchi) {
      const v = voci.find(x => uguali(x.b, b));
      if (v) { v.bande.push(...(b.canali || [])); v.blocchi.push(b); }
      else voci.push({ b, bande: (b.canali || []).slice(), blocchi: [b] });
    }
    return voci;
  }

  /*  LA RIGA DELLA POSA — una grammatica sola: posa · pose del canale (ore) · stanotte. Quando il canale sta tutto in
   *  una notte «stanotte» ripete il numero, e la ripetizione dice una cosa vera: che la notte e' il canale intero.
   *  In coda, chi ha deciso i secondi: tenue per tutti tranne il tetto di classe, che e' un ripiego dichiarato e si
   *  segna diverso. Non un allarme e non un divieto: il prodotto consiglia. */
  function rigaDellaPosa(v, conBanda, guadagnoPerRiga, serie) {
    const b = v.b, unite = v.blocchi.length > 1;
    let s = '';
    const parolaDelRuolo = vocePropria(PAROLA_DEL_RUOLO, b.ruolo), spiegazioneDelRuolo = vocePropria(SPIEGAZIONE_DEL_RUOLO, b.ruolo);
    /*  sul nucleo, accanto alla spiegazione, il perche' della sua serie, banda per banda, quando i pezzi arrivano */
    const perChe = b.ruolo === 'nucleo' ? perCheDelleBande(serie, v.bande).map(soloTesto) : [];
    if (parolaDelRuolo && spiegazioneDelRuolo)
      s += '<span class="ruolo ' + (b.ruolo === 'nucleo' ? 'nucleo' : '') + '" title="' +
           esc([T(spiegazioneDelRuolo)].concat(perChe).join('\n\n')) + '">' +
           esc(T(parolaDelRuolo)) + '</span> ';
    s += '<span class="posa-sec">' + cifra(b.sec) + ' s</span>';
    if (b.poseCanale != null)
      s += ' <span class="tenue">·</span> <b>' + cifra(b.poseCanale) + '</b> ' +
           esc(T(unite ? 'Pag_PosePerBanda' : 'Pag_PoseSulCanale')) +
           (b.oreCanale != null ? ' <span class="tenue">(' + oreScritte(b.oreCanaleHM, b.oreCanale) + ')</span>' : '');
    if (b.n != null) s += ' <span class="tenue">· ' + esc(T('Pag_Stanotte')) + '</span> <b>' + cifra(b.n) + '</b>';
    if (conBanda) s += ' <span class="tenue">' + esc(v.bande.join('·')) + '</span>';
    if (guadagnoPerRiga) s += ' <span class="tenue">· ' + guadagnoDelBlocco(b) + '</span>';
    if (vocePropria(PAROLA_DEL_LIMITE, b.limite)) {
      const parola = T(vocePropria(PAROLA_DEL_LIMITE, b.limite));
      s += b.limite === 'classe'
        ? ' <span class="limite classe" title="' + esc(T('Pag_LimiteClasseSpiegazione')) + '">' + esc(parola) + '</span>'
        : ' <span class="limite" title="' + esc(T('Pag_LimiteSpiegazione')) + '">' + esc(parola) + '</span>';
    }
    return s;
  }

  /*  SOTTO LE ORE DEL CANALE CHE DECIDE L'IMMAGINE, una volta sola — la profondita' e' del canale, non della notte:
   *  fin dove arrivano quelle ore, contro che cosa, quanto costa una magnitudine in piu' QUI; e da dove vengono le
   *  ore, quando non sono una previsione. Le ore che vengono dalla classe si segnano diverse, come la posa ferma
   *  alla classe: e' lo stesso genere di debito, detto al suo posto. */
  function profonditaDelCanale(p) {
    const f = p.profondita, bri = p.brillanza, righe = [];
    if (f && f.arrivi != null) {
      const dec = f.unita === 'R' ? 0 : 2;
      /*  L'ELEMENTO E μ_lim (25 settembre 2026): «arriva a» e' a SNR 12 nell'elemento della posa, e il servizio manda la
       *  sua larghezza (`elemento.fwhm`) e μ_lim a 3σ in 10″×10″ (`muLim`). Si scrivono come arrivano; senza, si tace. */
      let r1 = MF('Pag_Prof_Arrivi', cifra(f.arrivi, dec), esc(f.unita || '')) +
        (f.elemento && f.elemento.fwhm != null ? ' ' + MF('Pag_Prof_Elemento', cifra(f.elemento.fwhm, 2)) : '');
      if (f.riferimento == null)
        r1 += ' <span class="tenue">· ' + esc(T('Pag_Prof_SenzaRiferimento')) + '</span>';
      else if (PAROLA_DEL_RIFERIMENTO[f.riferimentoTipo])
        r1 += ' <span class="tenue">·</span> ' + MF(PAROLA_DEL_RIFERIMENTO[f.riferimentoTipo], cifra(f.riferimento, dec)) +
              (f.coloreNonPubblicato && f.riferimentoBanda
                ? ' <span class="tenue">(' + MF('Pag_Prof_ColoreNonPubblicato', esc(f.riferimentoBanda)) + ')</span>' : '');
      righe.push('<div>' + r1 + '</div>');
      const ml = f.muLim;
      if (ml && ml.valore != null && ml.sigma != null && ml.lato != null)
        righe.push('<div>' + MF('Pag_Prof_MuLim', cifra(ml.sigma), cifra(ml.lato), cifra(ml.valore, ml.unita === 'R' ? 1 : 2), esc(ml.unita || '')) + '</div>');
      if (f.scala != null && f.ore_per_una_mag != null)
        righe.push('<div>' + MF('Pag_Prof_Scala', cifra(f.scala, 1), oreScritte(f.ore_per_una_magHM, f.ore_per_una_mag)) +
          (PAROLA_DEL_REGIME[f.regimeCodice] ? ' <span class="tenue">(' + esc(T(PAROLA_DEL_REGIME[f.regimeCodice])) + ')</span>' : '') +
          '</div>');
      if (f.pavimentoLega)
        righe.push('<div class="avvisa">' + MF('Pag_Prof_Pavimento', cifra(f.snr)) +
          (f.ore_al_pavimento != null ? MF('Pag_Prof_PavimentoOre', oreScritte(f.ore_al_pavimentoHM, f.ore_al_pavimento)) : '') + '</div>');
    }
    if (bri && bri.ore_sono === 'classe')
      righe.push('<div><span class="ore-classe">' + esc(T('Pag_Bril_Classe')) + '</span></div>');
    else if (bri && bri.ore_sono === 'tetto' && bri.valore != null)
      righe.push('<div class="avvisa">' + MF('Pag_Bril_Almeno', cifra(bri.valore, 1)) +
        (bri.ereditato_da ? ' <span class="tenue">· ' + MF('Pag_Bril_Ereditato', esc(bri.ereditato_da)) + '</span>' : '') +
        '<br>' + esc(T('Pag_Bril_OreTetto')) + '</div>');
    return righe.length ? '<div class="profondita">' + righe.join('') + '</div>' : '';
  }

  /*  LE FRASI SOTTO IL PIANO, sulle ore oltre i tetti fisici (25 settembre 2026). Arrivano come pezzi sui gruppi della
   *  prescrizione — `puntiDArresto`, `pratica`, `aggiunta`, `praticaARiga`, `tettoUnico` — e come due decisioni della
   *  prescrizione intera, `oltreITettiARiga` e `avanzanoDaDire`. Il pannello mette insieme la frase con le sue voci, in
   *  italiano o in inglese, e senza il suo pezzo la frase non c'e'. Le cifre arrivano gia' sul progetto e gia' in
   *  percentuale: qui non si fa nessun conto. Un decimale per le ore, e «in tutto» se i riquadri sono piu' d'uno. */
  function frasiOltreITetti(p) {
    const pr = p.prescrizione || {}, alloc = (pr.alloc || []).filter(g => g && !g.dropped);
    const ore = (h, dec) => cifra(h, dec == null ? 1 : dec) + ' h' + (pr.panels > 1 ? ' ' + esc(T('Pag_InTutto')) : '');
    const pct = x => cifra(x, 0) + ' %';
    const nome = id => esc(id === 'Ha' ? 'Hα' : id);
    /*  l'RGB delle sole stelle non ha punti d'arresto da dire: lo dichiara la strada (`banda_larga`), non il pannello */
    const soloStelle = g => g.id === 'RGB' && !g.critical && pr.road && pr.road.banda_larga === 'stelle';
    const capo = alloc.find(x => x.pratica && x.pratica.percentuali);
    const frase = g => {
      const pa = g.puntiDArresto;
      if (pa && pa.coloreDiProgetto != null) {
        if (pa.oltreDallaPratica && capo)
          return MF('Pag_Arr_OltreDallaPratica', ore(pa.coloreDiProgetto), cifra(capo.pratica.campione, 0), cifra(capo.pratica.coloreSuL, 2),
            pct(capo.pratica.percentuali.colore), pct(capo.pratica.percentuali.luminanza));
        if (pa.luminanzaSenzaArresto) return MF('Pag_Arr_LuminanzaSenzaArresto', ore(pa.coloreDiProgetto));
        if (pa.luminanzaDiProgetto == null) return '';
        return pa.luminanza >= pa.colore ? MF('Pag_Arr_ColoreEPoiLuminanza', ore(pa.coloreDiProgetto), ore(pa.luminanzaDiProgetto))
                                         : MF('Pag_Arr_LuminanzaEPoiColore', ore(pa.luminanzaDiProgetto), ore(pa.coloreDiProgetto));
      }
      if (g.tettoUnico && g.satDiProgetto != null) return MF('Pag_Arr_TettoUnico', ore(g.satDiProgetto));
      const a = g.aggiunta, q = a && a.percentuali;
      if (g.additivo && q && q.quota != null && a.campione != null && a.rapportoDellaPratica != null) {
        if (q.L != null && a.coloreSuL != null && a.pavimentoDiProgetto != null)
          return MF('Pag_Arr_AggiuntaPratica', nome(g.id), pct(q.quota), ore(a.pavimentoDiProgetto, 2), pct(q.L), pct(q.colore),
            pct(q.aggiunta), cifra(a.campione, 0), cifra(a.rapportoDellaPratica, 2), cifra(a.coloreSuL, 2));
        return MF('Pag_Arr_Aggiunta', nome(g.id), pct(q.quota), cifra(a.campione, 0), cifra(a.rapportoDellaPratica, 2));
      }
      return '';
    };
    /*  i canali della base vengono prima, le aggiunte dopo: la frase di un'aggiunta parla di cio' che la base lascia */
    const vivi = alloc.filter(g => !soloStelle(g));
    const frasi = vivi.filter(g => !g.additivo).map(frase).concat(vivi.filter(g => g.additivo).map(frase)).filter(Boolean);
    const riga = alloc.filter(g => g.praticaARiga);
    if (riga.length >= 2) {
      const c = riga[0].praticaARiga || {};
      if (riga.length >= 3 && c.campioneSHO != null && c.campioneHOO != null)
        frasi.push(MF('Pag_Arr_ShoTavolozza', cifra(c.campioneSHO, 0), cifra(c.campioneHOO, 0)));
      if (pr.oltreITettiARiga === true) frasi.push(MF('Pag_Arr_OltreARiga'));
    }
    if (pr.avanzanoDaDire != null) frasi.push('<span class="avanzano">' + MF('Pag_Arr_Avanzano', cifra(pr.avanzanoDaDire, 1)) + '</span>');
    return frasi.length ? '<div class="arresti">' + frasi.join(' ') + '</div>' : '';
  }

  /*  DA DOVE VENGONO LE ORE, una riga per canale (25 settembre 2026). In `valutazione.budget.<canale>` il servizio manda
   *  le ore e, sotto `ore`, la loro origine fatta di codici; il pannello sceglie per quei codici le voci del suo
   *  dizionario e ci mette i numeri che arrivano. Un codice che le mappe qui sotto non conoscono si mostra cosi' com'e':
   *  indovinarne il senso vorrebbe dire scrivere una frase al posto del servizio. */
  const PAROLA_DELLA_CONFIDENZA = { bassa: 'Pag_Prov_Conf_bassa', 'media-bassa': 'Pag_Prov_Conf_media_bassa',
    media: 'Pag_Prov_Conf_media', 'media-alta': 'Pag_Prov_Conf_media_alta', alta: 'Pag_Prov_Conf_alta' };
  const MOTIVO_DI_FAMIGLIA = { senza_misura: 'Pag_Prov_Motivo_senza_misura', matrice: 'Pag_Prov_Motivo_matrice',
    fondo_sopra: 'Pag_Prov_Motivo_fondo_sopra', planetaria_dopo_il_cielo: 'Pag_Prov_Motivo_planetaria_dopo_il_cielo',
    stelle_risolte: 'Pag_Prov_Motivo_stelle_risolte', banda_non_singola: 'Pag_Prov_Motivo_banda_non_singola',
    tasso_non_calcolabile: 'Pag_Prov_Motivo_tasso_non_calcolabile' };
  const MOTIVO_DEL_BORDO = { non_risolto: 'Pag_Prov_Bordo_non_risolto', sotto_il_fondo: 'Pag_Prov_Bordo_sotto_il_fondo',
    dentro_un_altro: 'Pag_Prov_Bordo_dentro_un_altro', sopra_la_media: 'Pag_Prov_Bordo_sopra_la_media',
    stima_sotto_il_fondo: 'Pag_Prov_Bordo_stima_sotto_il_fondo' };
  const MOTIVO_DELL_UTILE = { colore_mancante: 'Pag_Prov_UtileMotivo_colore_mancante', fondo_non_misurato: 'Pag_Prov_UtileMotivo_fondo_non_misurato' };
  function daDoveVengono(origine) {
    if (!origine) return null;
    const inR = x => cifra(x, x < 10 ? 1 : 0) + ' R', inMag = x => cifra(x, 2) + ' mag/arcsec²';
    const voceDi = (mappa, codice) => { const chiave = vocePropria(mappa, codice); return chiave ? MF(chiave) : esc(codice); };
    if (origine.da === 'famiglia') return MF('Pag_Prov_Famiglia', voceDi(MOTIVO_DI_FAMIGLIA, origine.motivo));
    const lim = origine.soglia;
    if (origine.da === 'limite' && lim && lim.valore != null && lim.rapporto != null)
      return MF('Pag_Prov_Limite', inR(lim.valore), cifra(lim.rapporto, 1)) + MF(origine.alzato ? 'Pag_Prov_LimiteAlza' : 'Pag_Prov_LimiteSopra');
    if (origine.da !== 'fotometria') return origine.da ? esc(origine.da) : null;
    const elem = origine.elemento || {};
    const pezzi = [MF(elem.scala === '2×2' ? 'Pag_Prov_Base2x2' : 'Pag_Prov_Base', cifra(origine.snr), cifra(elem.fwhm, 2))];
    const sg = origine.soglia || {};
    if (sg.codice === 'media_d25') pezzi.push(MF('Pag_Prov_Soglia_media_d25', inMag(sg.valore), esc(sg.banda)));
    else if (sg.codice === 'media') pezzi.push(MF('Pag_Prov_Soglia_media', inR(sg.valore)) + (sg.alPiu ? MF('Pag_Prov_Almeno') : ''));
    else if (sg.codice) pezzi.push(esc(sg.codice));
    const ut = origine.utile;
    if (!ut) {
      const motivo = origine.motivoUtileCodice, chiave = motivo ? vocePropria(MOTIVO_DELL_UTILE, motivo) : null;
      pezzi.push(MF('Pag_Prov_UtileFamiglia') + (!motivo ? '' : chiave ? MF(chiave) : ': ' + esc(motivo)));
    } else if (ut.codice === 'bordo_d25') pezzi.push(MF('Pag_Prov_Utile_bordo_d25', inMag(ut.valore), esc(ut.banda)));
    else if (ut.codice === 'bordo_catalogato') pezzi.push(MF('Pag_Prov_Utile_bordo_catalogato', inR(ut.valore)));
    else if (ut.codice === 'bordo_stimato') pezzi.push(MF('Pag_Prov_Utile_bordo_stimato', inR(ut.valore), cifra(ut.rapporto, 3),
      cifra(ut.regioni, 0), cifra(ut.rapportoMinimo, 3), cifra(ut.rapportoMassimo, 3)));
    else if (ut.codice === 'fondo_locale') pezzi.push(MF('Pag_Prov_Utile_fondo_locale', inR(ut.valore)) +
      (ut.motivoCodice ? ': ' + voceDi(MOTIVO_DEL_BORDO, ut.motivoCodice) + (ut.dentro ? ' (' + esc(ut.dentro) + ')' : '') : ''));
    else if (ut.codice) pezzi.push(esc(ut.codice));
    return pezzi.join('; ');
  }
  function righeDaDoveVengono(p) {
    const budget = p.valutazione && p.valutazione.budget, pr = p.prescrizione || {};
    if (!budget) return '';
    const inOre = x => cifra(x, 2) + ' h';
    const righe = [];
    /*  La riga scrive le ore del canale nel modo in cui la prescrizione le usa. Un gruppo con una banda sola e' quel
     *  canale, e i suoi numeri sono quelli del gruppo: anche quando la tecnica prende l'RGB del soggetto, che il budget
     *  tiene accanto all'RGB delle stelle. La L che comanda porta con se' le proprie (`oreProprie`), ed e' di quelle che
     *  l'origine racconta. Per un gruppo che unisce piu' bande si scrive il budget di ciascuna. */
    for (const gr of (pr.alloc || [])) {
      if (!gr || gr.dropped) continue;
      const elenco = gr.bands || [gr.id];
      for (const canale of elenco) {
        const voce = Object.prototype.hasOwnProperty.call(budget, canale) ? budget[canale] : null;
        if (!voce) continue;
        const numeri = elenco.length > 1 ? voce
          : (gr.oreProprie || { floor: gr.floor, useful: gr.useful, saturates: gr.sat });
        if (!(numeri.useful > 0)) continue;
        const origine = daDoveVengono(voce.ore);
        const fiducia = vocePropria(PAROLA_DELLA_CONFIDENZA, voce.confidence);
        righe.push('<div class="provenienza-riga" data-canale="' + esc(canale) + '"><b>' + esc(canale) + '</b>: ' +
          MF('Pag_Prov_Ore', inOre(numeri.floor), inOre(numeri.useful)) +
          (numeri.saturates > 0 ? MF('Pag_Prov_Tetto', inOre(numeri.saturates)) : '') +
          (voce.tettoDellaScheda && voce.tettoDellaScheda.scritto != null ? MF('Pag_Prov_Scheda', inOre(voce.tettoDellaScheda.scritto)) : '') +
          (fiducia ? MF('Pag_Prov_Confidenza', MF(fiducia)) : '') +
          (origine ? ' — ' + origine + '.' : '') + '</div>');
      }
    }
    return righe.length ? '<div class="provenienza"><b>' + esc(T('Pag_Prov_Titolo')) + '</b>' + righe.join('') + '</div>' : '';
  }

  function riquadroDelPiano(p, r, d) {
    const notti = (p.piano && p.piano.nights) || [];
    const critico = p.prescrizione && p.prescrizione.critGroup;
    const piuNuovo = Number(d.contratto) > CONTRATTO_CONOSCIUTO
      ? '<div class="nota-rif">' + MF('Pag_ContrattoPiuNuovo', esc(d.contratto), cifra(CONTRATTO_CONOSCIUTO)) + '</div>' : '';
    /*  il perche' della serie corta di ogni banda si scrive una volta sola, sotto il suo canale, la prima notte che lo
     *  riprende: i pezzi sono del canale su tutto il progetto, e ripeterli ogni notte direbbe tre volte la stessa cosa */
    const serieDette = new Set();
    let senzaSerieDetta = false;
    const schede = (p.sequenze || []).map((s, iNotte) => {
      const m = s.modello || {}, blocchi = m.blocchi || [];
      const nt = notti.find(x => x.n === s.notte) || null;
      /*  LA TESTA DELLA NOTTE: il numero, la data, le ore che il cielo concede, e il giudizio sulla Luna come codice,
       *  con i tre numeri che lo spiegano al passaggio del mouse. */
      const TONO = { buio: 'ok', luna_non_disturba: 'ok', luna_tollerabile: 'attenzione', luna_pesante: 'pesante', non_usata: 'spento' };
      /*  Il perche' della pastiglia dice di quanto la Luna alza il fondo stanotte — il numero come arriva. */
      const cielo = nt && PAROLA_DEL_CIELO[nt.cielo]
        ? ' <span class="cielo ' + (TONO[nt.cielo] || 'spento') + '"' +
          (nt.dMagV != null ? ' title="' + esc(T('Pag_CieloSpiegazione').replace('{0}', cifra(nt.dMagV, 2))) + '"' : '') +
          '>' + esc(T(PAROLA_DEL_CIELO[nt.cielo])) + '</span>' : '';
      const testa = '<div class="notte-testa"><span class="titolo">' + MF('Pag_NotteN', cifra(s.notte)) + '</span>' +
        '<span class="data">' + esc(dataScritta((m.quando && m.quando.data) || '', { weekday: 'short', day: 'numeric', month: 'short' })) + '</span>' +
        (nt && nt.availH != null ? '<span class="disponibili">' + MF('Pag_OreDisponibili', cifra(nt.availH, 1) + ' h') + '</span>' : '') +
        cielo + '</div>';
      /*  Il guadagno si dice una volta per notte quando e' lo stesso per tutti i blocchi — com'e' quasi sempre —, e
       *  su ogni riga solo quando cambia: dirlo sei volte uguale era proprio il difetto della tabella. */
      const guadagni = [...new Set(blocchi.map(b => guadagnoDelBlocco(b)))];
      const perRiga = guadagni.length > 1;
      const canali = gruppiDellaNotte(nt, blocchi).map(g => {
        const voci = vociDelCanale(g.blocchi);
        const conBanda = voci.length > 1 || voci.some(v => v.bande.length > 1) ||
          (voci.length === 1 && voci[0].bande.length === 1 && voci[0].bande[0] !== g.id);
        const vetri = [...new Set(g.blocchi.map(b => vetroDelBlocco(p, b)))].join(' · ');
        /*  Le ore di questo canale stanotte le manda il motore (`totale.perGruppo`): sommare i blocchi qui sarebbe
         *  ricavare un numero. Se non arrivano, la colonna resta vuota. */
        const fatte = m.totale && m.totale.perGruppo ? m.totale.perGruppo[g.id] : null;
        const fatteHM = m.totale && m.totale.perGruppoHM ? m.totale.perGruppoHM[g.id] : null;
        const riga = '<div class="canale"><div class="nome ' + (TINTA_DEL_CANALE[g.id] || '') + '">' + esc(g.id) +
          '<small>' + esc(vetri) + '</small></div>' +
          '<div class="ore">' + (g.h != null ? oreScritte(g.hHM, g.h) : '') + '</div>' +
          '<div class="posa">' + voci.map(v => rigaDellaPosa(v, conBanda, perRiga, m.serieCorta)).join(' <span class="tenue">&nbsp;·&nbsp;</span> ') + '</div>' +
          '<div class="fatte">' + (fatte != null ? '= ' + oreScritte(fatteHM, fatte) : '') + '</div></div>';
        const bandeNuove = [...new Set(g.blocchi.flatMap(b => b.canali || []))].filter(c => !serieDette.has(c));
        const perChe = perCheDelleBande(m.serieCorta, bandeNuove);
        bandeNuove.filter(c => m.serieCorta && m.serieCorta.perBanda && Object.prototype.hasOwnProperty.call(m.serieCorta.perBanda, c))
          .forEach(c => serieDette.add(c));
        return riga + (iNotte === 0 && critico && g.id === critico ? profonditaDelCanale(p) : '') +
          perChe.map(x => '<div class="serie-corta">' + x + '</div>').join('');
      }).join('');
      /*  se la classe rinuncia alla serie corta, una riga sola, dopo i canali della prima notte che lo dice */
      const senza = !senzaSerieDetta && m.serieCorta ? vocePropria(SPIEGAZIONE_SENZA_SERIE, m.serieCorta.senzaSerie) : null;
      if (senza) senzaSerieDetta = true;
      const senzaSerie = senza ? '<div class="serie-corta senza">' + MF(senza) + '</div>' : '';
      const t = m.totale || {};
      const totale = t.ore == null ? '' : (t.orologio != null
        ? MF('Pag_TotaleNotte', oreScritte(t.oreHM, t.ore), oreScritte(t.orologioHM, t.orologio))
        : MF('Pag_TotaleNotteSoloIntegrazione', oreScritte(t.oreHM, t.ore)));
      /*  «restano X se il cielo tiene» c'e' quando il servizio lo manda: la soglia e' sua, e sotto la soglia il campo
       *  non arriva. Il pannello non la conosce e non ne ha bisogno. */
      const restano = nt && nt.restano != null
        ? ' <span class="tenue">· ' + MF('Pag_Restano', oreScritte(nt.restanoHM, nt.restano)) + '</span>' : '';
      /* L'IDENTIFICATIVO VIAGGIA CON LA RIGA, non in una variabile a parte.
         Sembra un dettaglio ed e' la differenza fra una guardia che funziona e una che
         non puo' scattare: se il tasto leggesse l'ultimo identificativo ricevuto, un
         tasto rimasto in giro da una richiesta precedente manderebbe l'oggetto NUOVO
         con l'aria di mandare il vecchio — e il ponte non avrebbe modo di accorgersene,
         perche' l'identificativo che riceve sarebbe quello giusto. Provato: succedeva. */
      const tasto = r.consegnabile
        ? '<button class="manda" data-notte="' + s.notte + '" data-prescrizione="' + esc(r.prescrizione || '') + '">' +
          T('Pag_MandaANina') + '</button>' : '';
      const piede = '<div class="notte-piede"><span>' + totale + restano + '</span>' +
        (!perRiga && guadagni.length === 1 ? '<span>· ' + guadagni[0] + '</span>' : '') + tasto + '</div>';
      return '<div class="notte">' + testa + righeDellaLuna(p.luna, s.notte) + canali + senzaSerie + piede + '</div>';
    }).join('');
    return piuNuovo + frasiOltreITetti(p) + righeDaDoveVengono(p) + schede;
  }

  function guadagnoDelBlocco(b) {
    if (b.gain == null || b.gain < 0) return T('Pag_GuadagnoDellaCamera');
    return b.gainFonte === 'dichiarato'
      ? MF('Pag_GuadagnoDichiarato', cifra(b.gain))
      : MF('Pag_GuadagnoMotore', cifra(b.gain), esc(b.modo || '—'));
  }

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
    return vetroNellaRuota(id);
  }
  /*  Il nome che la ruota di N.I.N.A. da' a un filtro del catalogo: e' quello che si legge nella sequenza. */
  function vetroNellaRuota(id) {
    if (!id) return '—';
    if (id === '__none') return T('Pag_NessunFiltroBayer');
    const v = righeRuota.find(x => x.id === id);
    return v ? v.nina : T('Pag_FiltroNonDichiarato').replace('{0}', id);
  }

  const num = v => cifra(v);

  /*  LA PROVENIENZA DI UN CAMPO DEL SITO. E' un codice, e la parola la mette il dizionario: qui si confrontava la
   *  frase italiana («non disponibile», «dichiarato»), che tradotta avrebbe sbagliato il colore in silenzio e non
   *  tradotta in inglese si leggeva in italiano.
   *  E QUANDO NESSUNO LO DICHIARA, l'assunzione del motore sta accanto al campo (16 settembre 2026): il blocco diceva
   *  «non disponibile» mentre il riquadro dei dati assunti, sotto, diceva il valore. Il campo lo nomina il motore
   *  (`campo` in `parziale`); la pagina non sa che cosa si assume finche' una prescrizione non lo dice. */
  const unitaDelSito = {};
  function provenienzaDelSito(campo) {
    const p = sitoProv[campo] || 'non_disponibile';
    const colore = p === 'non_disponibile' ? 'color:#e0a030' : p === 'dichiarato' ? 'opacity:.75' : 'opacity:.6';
    const parola = { profilo: 'Pag_Prov_profilo', misurato: 'Pag_Prov_misurato',
                     dichiarato: 'Pag_Prov_dichiarato', non_disponibile: 'Pag_Prov_non_disponibile' }[p];
    const assunto = (p === 'non_disponibile' && parzialeUsato)
      ? parzialeUsato.find(x => x.pezzo === 'sito' && x.campo === campo && x.dati && x.dati.assunto != null) : null;
    return '<span data-prov-sito="' + esc(campo) + '" style="font-size:12px;' + colore + '">' +
      (assunto ? MF('Pag_Prov_assunto', cifra(assunto.dati.assunto), unitaDelSito[campo] || '')
               : esc(parola ? T(parola) : p)) + '</span>';
  }
  function aggiornaProvenienzeDelSito() {
    for (const s of document.querySelectorAll('[data-prov-sito]'))
      s.outerHTML = provenienzaDelSito(s.getAttribute('data-prov-sito'));
  }

  /*  L'ORIZZONTE DEL PROFILO (prova in N.I.N.A. sul MiniX, 16 settembre 2026): da quale file viene e quanti punti porta,
   *  oppure che il profilo lo nomina e non si e' letto. Senza file non c'e' riga: vale l'altezza minima qui sotto. Il
   *  profilo viaggia dentro `sito`, e la richiesta lo rimanda com'e'. */
  function rigaOrizzonte(r) {
    const punti = sito && Array.isArray(sito.orizzonte) ? sito.orizzonte.length : 0;
    const file = (r && r.orizzonteFile) || '';
    if (!punti && !file) return '';
    const nome = esc(file.split(/[\\/]/).pop());
    return '<tr><th>' + esc(T('Pag_Orizzonte')) + '</th><td><span style="font-size:12px;' +
      (punti ? 'opacity:.6">' + MF('Pag_OrizzonteDalProfilo', nome, cifra(punti))
             : 'color:#e0a030">' + MF('Pag_OrizzonteNonLetto', nome)) + '</span></td></tr>';
  }

  /*  IL SITO CHE PARTE (regia, 18 settembre 2026): la richiesta porta solo i campi che il Ponte offre — la geometria e
   *  l'orizzonte del profilo, il cielo, il seeing tipico e l'altezza minima. Guida e notti serene non si offrono piu': i
   *  loro valori restano nei profili, muti, e non partono ne' col numero rimasto ne' vuoti; il motore applica il suo
   *  riferimento e lo dichiara. IL SEEING TIPICO TORNA (regia, 19 settembre 2026): e' una proprieta' del posto, della
   *  stessa specie dell'SQM, e il motore lo usa nel giudizio di campionamento e nel consiglio di binning; la posa resta sul
   *  riferimento, e il motore dice perche'. E' l'elenco di cio' che entra, non di cio' che resta fuori: una chiave nuova
   *  del sito non parte finche' qualcuno non la scrive qui. L'ospite gia' non manda i muti (CampiMutiTests); questa e' la
   *  seconda porta, per un sito che li porta ancora. */
  const SITO_CHE_PARTE = ['lat', 'lon', 'sqm', 'seeing', 'horizonMin', 'orizzonte'];
  function sitoDaMandare() {
    const s = {};
    for (const k of SITO_CHE_PARTE) if (sito && sito[k] !== undefined) s[k] = sito[k];
    return s;
  }

  function campoDelSito(chiave) {
    return campiDelSito.find(c => c && c.chiave === chiave) || null;
  }
  /*  LE SPIEGAZIONI DEL SITO, dal motore (18 settembre 2026): il tooltip e la «i» delle righe. I campi del
   *  servizio possono arrivare prima o dopo il sito: si applicano a quello che e' a schermo, senza ridisegnarlo, cosi' un
   *  numero scritto e non ancora salvato resta dov'e'. */
  function spiegaIlSito() {
    for (const tr of document.querySelectorAll('#sito [data-riga-sito]')) {
      const s = spiegazioneDi(campoDelSito(tr.getAttribute('data-riga-sito')));
      for (const el of tr.querySelectorAll('th, input, .ico.spiega')) {
        if (s) el.setAttribute('title', s); else el.removeAttribute('title');
      }
      const i = tr.querySelector('.ico.spiega');
      if (i) i.hidden = !s;
    }
  }

  function disegnaSito(r) {
    sito = r.sito || null;
    lunaDellaData();
    sitoScritto = r.dichiarato || {};
    sitoProv = r.provenienza || {};
    sitoManca = r.manca || null;

    /* La provenienza si vede accanto al numero: un SQM misurato e uno scritto a mano
       valgono lo stesso per il motore, ma non per chi guarda. La spiegazione del campo la manda il motore, e la mette
       `spiegaIlSito` nel tooltip della riga e nella «i»: qui la riga dice solo quale campo e'. */
    const riga = (etichetta, campo, unita, scrivibile) => {
      const v = sito ? sito[campo] : null;
      unitaDelSito[campo] = unita;
      return '<tr data-riga-sito="' + campo + '"><th>' + etichetta + '</th><td>' +
        (scrivibile
          ? '<input data-sito="' + campo + '" value="' + (sitoScritto[campo] === null ||
              sitoScritto[campo] === undefined ? '' : sitoScritto[campo]) +
            '" style="width:70px" spellcheck="false"> ' +
            (v === null || v === undefined ? '' : '<b>' + num(v) + '</b> ' + unita)
          : '<b>' + num(v) + '</b> ' + unita) +
        ' ' + provenienzaDelSito(campo) + ' <span class="ico spiega" tabindex="0" hidden>i</span></td></tr>';
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
      /*  IL SEEING TIPICO DEL SITO (regia, 19 settembre 2026): si dichiara, e solo si dichiara. La FWHM che una stazione
       *  misura stanotte non e' il seeing del posto — e' la notte, con la guida e l'ottica dentro — e non lo sostituisce. */
      riga(T('Pag_Seeing'), 'seeing', '&Prime;', true) +
      rigaOrizzonte(r) +
      riga(T('Pag_AltezzaMinima'), 'horizonMin', '&deg;', true) +
      /*  Guida e notti serene non ci sono piu' (regia, 18 settembre 2026): nel Ponte non muovevano niente di visibile, e
       *  un campo cosi' e' un'assunzione muta travestita da controllo. Il valore che il motore assume per le notti serene si
       *  legge nella nota sotto la prescrizione (`notaDeiRiferimenti`). Quello che cielo, seeing e altezza minima vogliono
       *  dire lo spiega il motore nei tooltip. */
      '</table>' +
      '<div style="margin-top:.7em">' +
        '<button id="salvaSito">' + T('Pag_SalvaSito') + '</button> ' +
        esito('esitoSito') +
      '</div>' +
      '<div style="margin-top:.7em;opacity:.7;font-size:12.5px">' +
        MF('Pag_LatLonNota') +
      '</div></div>';
    spiegaIlSito();

    Array.prototype.forEach.call(document.querySelectorAll('#sito input'), i => {
      i.addEventListener('input', () => { segnaEsito('esitoSito', 'attesa', T('Pag_NonSalvato')); });
    });
    const b = $('salvaSito');
    if (b) b.addEventListener('click', () => {
      const s = {};
      Array.prototype.forEach.call(document.querySelectorAll('#sito input'), i => {
        const v = i.value.trim().replace(',', '.');
        s[i.getAttribute('data-sito')] = v === '' ? null : Number(v);
      });
      segnaEsito('esitoSito', 'corso', T('Pag_Salvo'));
      chiedi('salvaSito', { sito: s }).then(r2 => {
        if (r2.ok) segnaEsito('esitoSito', 'ok', T('Pag_SalvatoAlle').replace('{0}', oraDiAdesso()));
        else segnaEsito('esitoSito', 'no', T('Pag_NonSalvatoPerche').replace('{0}', r2.messaggio || r2.codice || ''));
        ritiraDalloSchermo(r2.ritirata);
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
          ritiraDalloSchermo(r2.ritirata);
          if (r2.ok) chiedi('filtri').then(disegnaRuota);
        });
    });
  }

  /* ── IL MENU DELLE STRADE ─────────────────────────────────────────────────────
   *  Le carte sono quelle che Strategy manda nella prescrizione, e questa pagina non ne decide nessuna: quali strade
   *  si scelgono, quale e' raccomandata e perche', quali non si fanno e con che motivo, il prezzo di ognuna alla scala
   *  del progetto. Qui si leggono e si scrivono con parole del Ponte, per codice. Il nome di una strada e' quello del
   *  motore e non si traduce: e' il nome che arriva in N.I.N.A.
   *
   *  Il colore dice la natura della frase, come nella pagina del motore: neutro una spiegazione, giallo un limite
   *  dichiarato, rosso un guasto.
   *
   *  Le chiavi sono letterali, non composte col codice: una chiave composta la prova delle voci orfane non la vede.
   *  Un codice che qui non ha una voce non diventa una frase inventata: si scrive il codice. */
  const PAROLA_VIA = {
    variante_pura: 'Pag_Men_Via_variante_pura',
    default_della_scheda: 'Pag_Men_Via_default_della_scheda',
    prima_voce_della_tecnica: 'Pag_Men_Via_prima_voce_della_tecnica',
  };
  const PAROLA_NON_PREZZABILE = {
    scala_del_soggetto_non_dichiarata: 'Pag_Men_NonPrezzabile_scala_del_soggetto_non_dichiarata',
    scala_delle_stelle_non_dichiarata: 'Pag_Men_NonPrezzabile_scala_delle_stelle_non_dichiarata',
  };
  const PAROLA_SOSTITUITA = {
    strada_sconosciuta: 'Pag_Men_Sostituita_strada_sconosciuta',
    spezzata_dalla_coppia: 'Pag_Men_Sostituita_spezzata_dalla_coppia',
    scala_del_soggetto_non_dichiarata: 'Pag_Men_Sostituita_scala_del_soggetto_non_dichiarata',
    stessi_canali: 'Pag_Men_Sostituita_stessi_canali',
  };

  /*  I conteggi della tabella con la loro data: la data arriva nella forma dei dati, e si scrive in quella della
   *  lingua. Se i dati non portano la data, qui non se ne inventa una. */
  function margineDellaClasse(m) {
    if (!m || m.conteggio === null || m.conteggio === undefined) return '';
    const base = m.seconda
      ? MF('Pag_Men_Margine', esc(m.tecnica), cifra(m.conteggio), esc(m.seconda.tecnica), cifra(m.seconda.conteggio))
      : MF('Pag_Men_MargineSolo', esc(m.tecnica), cifra(m.conteggio));
    const d = /^(\d{4})-(\d{2})-(\d{2})$/.exec(m.conteggiDel || '');
    if (!d) return base;
    let quando = m.conteggiDel;
    try {
      quando = new Date(Date.UTC(+d[1], +d[2] - 1, +d[3])).toLocaleDateString(T('Pag_Men_FormatoData'),
        { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' });
    } catch (e) { /* una lingua che il browser non conosce: resta la data dei dati */ }
    return base + '; ' + MF('Pag_Men_ConteggiDel', esc(quando));
  }
  function primaScelta(m) {
    const margine = margineDellaClasse(m);
    return MF('Pag_Men_PrimaScelta', esc(m.tecnica)) + (margine ? ' (' + margine + ')' : '');
  }
  function testoDellaRaccomandata(v) {
    const m = v && v.raccomandataPerche;
    if (!m || !PAROLA_VIA[m.motivo]) return '';
    return primaScelta(m) +
      (m.tecnicaDellaVoce ? '; ' + MF('Pag_Men_SuMatrice', esc(m.tecnica), esc(m.tecnicaDellaVoce)) : '') +
      '; ' + MF(PAROLA_VIA[m.motivo], esc(m.tecnicaDellaVoce || m.tecnica));
  }
  function testoDellAssenza(a, pr) {
    if (!a) return '';
    if (a.motivo === 'classe_non_misurata') return MF('Pag_Men_Assente_classe_non_misurata');
    if (a.motivo === 'nessuna_voce_della_tecnica')
      return MF('Pag_Men_Assente_nessuna_voce_della_tecnica', primaScelta(a), esc(a.tecnica));
    if (a.motivo === 'prima_scelta_non_percorribile') {
      const b = (pr.blocked || []).find(x => x.road === a.road);
      return MF('Pag_Men_Assente_prima_scelta_non_percorribile', primaScelta(a), esc(b ? (b.name || b.road) : a.road));
    }
    return esc(a.motivo);
  }

  /*  IL MOTIVO DI UNA BANDA CHE MANCA lo scrive Strategy coi dati che servono a dirlo — la banda, se la camera ha la
   *  matrice, quali filtri mancano, quale filtro suggerisce, quali filtri in ruota non vanno —, e qui si mettono le
   *  parole. Nessun catalogo dei filtri qui dentro: i nomi arrivano nel motivo. */
  function testoDelMotivo(x) {
    if (!x) return '';
    /*  Il nome della banda come arriva nel motivo, protetto e nient'altro: qui non si ricava una banda. E le larghezze
     *  si scrivono come le manda Strategy: arrotondarle qui sarebbe gia' decidere quale cifra conta. */
    const nomeDellaBanda = escOVuoto(x.banda);
    const dove = esc(T(x.matrice ? 'Pag_Men_Dove_matrice' : 'Pag_Men_Dove_mono'));
    const scelto = esc(x.nome || x.filtro || '');
    const suggerito = esc((x.suggerito || {}).nome || '');
    switch (x.tipo) {
      case 'canale_spento': return MF('Pag_Men_Motivo_canale_spento', nomeDellaBanda);
      case 'canale_assente':
        if (Array.isArray(x.mancano)) return MF('Pag_Men_Motivo_canale_assente_rgb', dove, x.mancano.map(esc).join(', '));
        if (x.nelCatalogo === false) return MF('Pag_Men_Motivo_canale_assente_non_esiste', nomeDellaBanda, dove);
        return MF('Pag_Men_Motivo_canale_assente', nomeDellaBanda, dove, suggerito);
      case 'sensore_incompatibile':
        if (Array.isArray(x.nomi) && x.nomi.length)
          return MF('Pag_Men_Motivo_sensore_incompatibile_ruota', nomeDellaBanda, x.nomi.map(esc).join(', '), dove, suggerito);
        /*  il ruolo rifiutato dal sensore porta il filtro compatibile (decisione del 16 settembre 2026) */
        if (suggerito) return MF('Pag_Men_Motivo_sensore_incompatibile_suggerito', nomeDellaBanda, scelto, dove, suggerito);
        return MF('Pag_Men_Motivo_sensore_incompatibile', nomeDellaBanda, scelto, dove);
      case 'non_in_ruota': return MF('Pag_Men_Motivo_non_in_ruota', nomeDellaBanda, scelto);
      case 'vetro_sconosciuto': return MF('Pag_Men_Motivo_vetro_sconosciuto', nomeDellaBanda, scelto);
      case 'classe_incompatibile': return MF('Pag_Men_Motivo_classe_incompatibile', nomeDellaBanda, scelto);
      case 'sotto_soglia_continuo':
        return MF('Pag_Men_Motivo_sotto_soglia_continuo', nomeDellaBanda, scelto, cifra(x.larghezza_nm), cifra(x.soglia_nm));
      case 'escluso_per_decisione': return MF('Pag_Men_Motivo_escluso_per_decisione', nomeDellaBanda, scelto);
      /*  un tipo che la pagina non conosce ancora: la banda e il codice del motore, non una colpa data alla ruota */
      default: return MF('Pag_Men_Motivo_generico', nomeDellaBanda, escOVuoto(x.tipo));
    }
  }
  /*  LA SPIEGAZIONE DI UN CAMPO DEL BANCO, nella lingua in uso (18 settembre 2026): la manda il motore, accanto al campo,
   *  in piu' lingue. Se manca quella in uso vale un'altra delle sue; se non ne manda nessuna non c'e' tooltip — mai una
   *  frase nostra al suo posto. */
  function spiegazioneDi(c) {
    const s = c && c.spiegazione;
    if (!s || typeof s !== 'object') return '';
    return s[T('Pag_CodiceLingua')] || s.it || s.en || '';
  }

  /*  LA STRADA BLOCCATA, una carta sola per il menu e per il rifiuto (18 settembre 2026): il nome, «non disponibile»,
   *  e il motivo come lo manda Strategy. Il rifiuto la usa perche' il pannello non scriva mai una frase sua al posto
   *  dei motivi del motore. */
  function cartaBloccata(b) {
    return '<div class="roadcard lack" aria-disabled="true">' +
      '<div class="rc-h"><b>' + esc(b.name || b.road) + '</b></div>' +
      '<div class="rc-p"><span class="pill p-dim">' + esc(T('Pag_Men_NonDisponibile')) + '</span></div>' +
      '<div class="rc-n"><span class="p-warn">' + (b.perche || []).map(testoDelMotivo).join(' · ') + '</span></div></div>';
  }

  /*  LA CAMERA DEL CALCOLO (18 settembre 2026): quale camera c'era dentro il conto, da dove viene e che matrice ha. E'
   *  l'informazione che mancava per capire un rifiuto: con una mono scritta nel banco i dual-band non separano l'OIII,
   *  e il rimedio e' la camera. Tutto come lo manda Strategy nel prodotto o nel rifiuto. */
  function cameraDelCalcolo(cam, soloVoce) {
    if (!cam) return '';
    const collegataFuori = MF('Pag_Camera_Via_collegata_fuori_catalogo', esc(cam.nome_driver || cam.voce || ''));
    const via = cam.riconoscimento === 'nome_e_geometria' ? MF('Pag_Camera_Via_nome_e_geometria')
      : cam.riconoscimento === 'dichiarata' || cam.riconoscimento === 'catalogo'
        ? MF(cam.osservata ? 'Pag_Camera_Via_dichiarata_collegata' : 'Pag_Camera_Via_dichiarata')
      : cam.osservata ? collegataFuori : MF('Pag_Camera_Via_descritta');
    const matrice = cam.matrice && PAROLA_MATRICE[cam.matrice] ? esc(T(PAROLA_MATRICE[cam.matrice])) : '';
    return '<div class="camera-calcolo">' + MF('Pag_CameraDelCalcolo', esc(cam.voce || cam.id || ''), via, matrice) +
      (soloVoce ? '<div class="avvisa">' + MF('Pag_Camera_SoloVoce') + '</div>' : '') +
      (cam.disaccordo ? '<div>' + disaccordoDellaCamera(cam.disaccordo) + '</div>' : '') + '</div>';
  }

  /*  IL DISACCORDO FRA LA VOCE SCRITTA E LA CAMERA COLLEGATA, in giallo coi due nomi: vale la collegata. Accanto, «usa
   *  invece la voce scritta», che chiede la prescrizione con la sola voce — solo quando la voce esiste nel catalogo. */
  function disaccordoDellaCamera(dis) {
    if (!dis) return '';
    const s = dis.scritta || {}, c = dis.collegata;
    const scritta = s.voce ? esc(s.voce) : '«' + esc(s.chiesto || '') + '»';
    const collegata = esc(c ? (c.voce || c.id) : (dis.nome_driver || ''));
    return '<span class="avvisa">' + MF('Pag_Camera_Disaccordo', scritta, collegata) + '</span>' +
      (s.id ? ' <button type="button" class="collegamento" data-usa-voce-scritta="1">' +
        esc(T('Pag_Camera_UsaVoceScritta')) + '</button>' : '');
  }

  /*  IL RIFIUTO DEL MOTORE, coi suoi motivi (regia, 18 settembre 2026): la frase che l'ospite ha composto dai dati, la
   *  camera del calcolo, e le strade bloccate con la carta del menu. Prima c'era la sola frase, e per nessuna_prescrizione
   *  era una frase nostra che dava la colpa ai filtri. */
  function disegnaRifiuto(r, soloVoce) {
    let e = null;
    try { e = r.corpo ? (JSON.parse(r.corpo).errore || null) : null; } catch (x) { e = null; }
    const bloccate = e && Array.isArray(e.bloccate) ? e.bloccate : [];
    return '<div class="box err"><b>' + esc(r.codice || T('Pag_Errore')) + '</b>' +
      '<div style="margin-top:6px;opacity:.8">' + M(r.messaggio || '') + '</div>' +
      (e && e.camera ? cameraDelCalcolo(e.camera, soloVoce) : '') +
      (bloccate.length ? '<div class="roadgrid" style="margin-top:10px">' + bloccate.map(cartaBloccata).join('') + '</div>' : '') +
      '</div>';
  }

  function testoStelle(perche) {
    if (perche && perche.tipo === 'canale_spento')
      return MF('Pag_Men_StelleNonRiprendibili_spento', escOVuoto(perche.banda));
    const m = (perche && perche.mancano) || [];
    if (m.length) return MF('Pag_Men_StelleNonRiprendibili_mancano', m.map(esc).join(', '));
    /*  un motivo senza le bande che mancano passa dal testo del motivo (18 settembre 2026); la frase senza motivo vale
     *  solo quando il motore non ne ha mandato uno */
    return perche && perche.tipo ? MF('Pag_Men_StelleNonRiprendibili_perche', testoDelMotivo(perche))
                                 : MF('Pag_Men_StelleNonRiprendibili');
  }

  function disegnaMenu(pr) {
    if (!pr || !pr.roadChoices || !pr.roadChoices.length) return '';
    const voci = pr.roadChoices;
    const chiuse = (pr.blocked || []).filter(b => (b.needs || []).length && !voci.some(c => c.id === b.road));
    const nonPrezzabili = ((pr.generazione || {}).nonPrezzabili) || [];
    const stesse = pr.stessaRipresa || [];
    const escluse = pr.stradeEscluse || [];
    const assenza = testoDellAssenza(pr.raccomandataAssente, pr);
    const titolo = '<div class="sc-t">' + esc(T('Pag_Men_Titolo')) +
      ' <span class="muted">' + esc(T('Pag_Men_Sottotitolo')) + '</span></div>';
    /*  Un menu da una carta non ha niente da scegliere, ma il perche' di un'assenza resta: e' un'informazione sulla
     *  classe o sull'attrezzatura. Anche le carte bloccate si contano: per questo una prima scelta che la
     *  ruota non fa non riduce mai il menu a una carta sola. */
    if (voci.length + chiuse.length + nonPrezzabili.length + stesse.length + escluse.length < 2)
      return assenza ? '<div class="box stratbox" id="menu">' + titolo +
        '<div class="sc-s"><span class="p-warn">' + assenza + '.</span></div></div>' : '';

    const pastiglia = (chiave, classe) => '<span class="pill ' + (classe || 'p-dim') + '">' + esc(T(chiave)) + '</span>';
    const nomeDi = id => { const v = voci.find(c => c.id === id); return v ? v.name : (id || ''); };
    /*  Il prezzo di progetto, come Strategy lo manda: qui non si moltiplica niente per i riquadri. */
    const prezzo = c => {
      const ore = c.progetto && c.progetto.ideal;
      if (ore === null || ore === undefined) return '<span class="p-bad">' + esc(T('Pag_Men_CostoNonCalcolabile')) + '</span>';
      return MF(pr.panels > 1 ? 'Pag_Men_PrezzoProgetto' : 'Pag_Men_Prezzo', cifra(ore, 1));
    };
    const carta = c => {
      const scelta = !!(pr.roadPicked && pr.road && pr.road.id === c.id);
      const presa = !!(!pr.roadPicked && pr.road && pr.road.id === c.id);
      const pastiglie = (scelta ? pastiglia('Pag_Men_SceltaTua', 'p-ok') : presa ? pastiglia('Pag_Men_LaPrendeIlMotore') : '') +
        (c.raccomandata ? pastiglia('Pag_Men_Raccomandata') : '');
      return '<button class="roadcard' + (scelta ? ' on' : '') + (presa ? ' now' : '') + '" data-strada="' + esc(c.id) +
          '" aria-pressed="' + (scelta ? 'true' : 'false') + '">' +
        '<div class="rc-h"><b>' + esc(c.name) + '</b></div>' +
        (pastiglie ? '<div class="rc-p">' + pastiglie + '</div>' : '') +
        (c.when ? '<div class="rc-w">' + esc(c.when) + '</div>' : '') +
        (c.limiti || []).filter(l => l.tipo === 'passata_stelle_non_riprendibile')
          .map(l => '<div class="rc-n"><span class="p-warn">' + testoStelle(l.perche) + '</span></div>').join('') +
        '<div class="rc-n">' + prezzo(c) + '</div></button>';
    };
    const ferma = (nome, chiavePastiglia, testo) => '<div class="roadcard lack" aria-disabled="true">' +
      '<div class="rc-h"><b>' + esc(nome) + '</b></div><div class="rc-p">' + pastiglia(chiavePastiglia) + '</div>' +
      '<div class="rc-n">' + testo + '</div></div>';
    const auto = voci.find(c => c.id === pr.roadAuto);
    const cartaAuto = '<button class="roadcard auto' + (pr.roadPicked ? '' : ' on') + '" data-strada="auto" aria-pressed="' +
        (pr.roadPicked ? 'false' : 'true') + '">' +
      '<div class="rc-h"><b>' + esc(T('Pag_Men_SceglieMotore')) + '</b></div>' +
      (pr.roadPicked ? '' : '<div class="rc-p">' + pastiglia('Pag_Men_InUso', 'p-ok') + '</div>') +
      '<div class="rc-w">' + esc(T('Pag_Men_SceglieMotoreNota')) + '</div>' +
      '<div class="rc-n">' + MF('Pag_Men_OraSarebbe', esc(auto ? auto.name : (pr.roadAuto || ''))) + '</div></button>';

    const righe = [];
    const racc = voci.find(c => c.raccomandata);
    const perche = racc ? testoDellaRaccomandata(racc) : '';
    if (perche) righe.push('<div class="sc-s">' + MF('Pag_Men_RaccomandataPerche', esc(racc.name), perche) + '.</div>');
    if (assenza) righe.push('<div class="sc-s"><span class="p-warn">' + assenza + '.</span></div>');
    /*  UN'IMMAGINE DIVERSA SI DICE DIVERSA (decisione del 16 settembre 2026): la HOO consegnata perche' manca il SII non e'
     *  una SHO piu' economica. Lo dichiara Strategy sulla bloccata; qui si scrive. */
    const diverse = [...new Set(chiuse.filter(b => b.immagineDiversa && b.tecnica).map(b => b.tecnica))];
    if (diverse.length && pr.road && pr.road.tecnica)
      righe.push('<div class="sc-s">' + MF('Pag_Men_ImmagineDiversa', esc(pr.road.tecnica), esc(diverse.join(' / '))) + '</div>');
    const chiesta = pr.roadRequestedBlocked, rs = pr.roadRequestedStessaRipresa, sost = pr.roadRequestedSostituita;
    const presaOra = pr.road ? pr.road.name : '';
    if (chiesta)
      righe.push('<div class="sc-s">' + MF('Pag_Men_ChiestaBloccata', esc(chiesta.name || chiesta.road),
        (chiesta.perche || []).map(testoDelMotivo).join(' · ')) + '</div>');
    else if (rs && rs.motivo === 'passata_stelle_non_riprendibile')
      righe.push('<div class="sc-s">' + MF('Pag_Men_ChiestaStelle', esc(rs.nomeChiesta || rs.chiesta),
        testoStelle(rs.perche), esc(presaOra)) + '</div>');
    else if (rs)
      righe.push('<div class="sc-s">' + MF('Pag_Men_ChiestaStessaRipresa', esc(rs.nomeChiesta || rs.chiesta), esc(presaOra)) + '</div>');
    else if (sost)
      righe.push('<div class="sc-s">' + MF(PAROLA_SOSTITUITA[sost.motivo] || 'Pag_Men_Sostituita_generica',
        esc(sost.nomeChiesta || sost.chiesta), esc(presaOra), esc(sost.nomeCon || sost.con || ''), escOVuoto(sost.motivo)) + '</div>');
    else if (pr.roadPicked && !pr.roadAutoSame)
      righe.push('<div class="sc-s">' + MF('Pag_Men_StaiScegliendo', esc(auto ? auto.name : (pr.roadAuto || ''))) + '</div>');
    if (!pr.roadPicked && pr.roadAutoRisolta)
      righe.push('<div class="sc-s">' + MF('Pag_Men_AutoRisolta', esc(pr.roadAutoRisolta.nomeDecisa),
        testoStelle(pr.roadAutoRisolta.perche), esc(pr.roadAutoRisolta.nomePresa)) + '</div>');
    for (const l of (pr.limitiDellaStrada || []).filter(l => l.tipo === 'passata_stelle_non_riprendibile'))
      righe.push('<div class="sc-s">' + MF('Pag_Men_LimiteStelle', esc(presaOra), testoStelle(l.perche)) + '</div>');

    return '<div class="box stratbox" id="menu">' + titolo + '<div class="roadgrid">' +
      cartaAuto + voci.map(carta).join('') +
      chiuse.map(cartaBloccata).join('') +
      nonPrezzabili.map(x => ferma(x.name, 'Pag_Men_NonDisponibile', '<span class="p-warn">' +
        (PAROLA_NON_PREZZABILE[x.motivo] ? esc(T(PAROLA_NON_PREZZABILE[x.motivo])) : esc(x.motivo)) + '</span>')).join('') +
      stesse.map(x => ferma(x.name, 'Pag_Men_StessaRipresa', x.motivo === 'passata_stelle_non_riprendibile'
        ? '<span class="p-warn">' + MF('Pag_Men_Stessa_passata_stelle_non_riprendibile', testoStelle(x.perche), esc(nomeDi(x.con))) + '</span>'
        : MF('Pag_Men_Stessa_stessa_ripresa_su_matrice', esc(nomeDi(x.con))))).join('') +
      escluse.map(x => ferma(x.name || x.road, x.motivo === 'stessi_canali' ? 'Pag_Men_StessiCanali' : 'Pag_Men_Esclusa',
        x.motivo === 'spezzata_dalla_coppia'
          ? '<span class="p-warn">' + MF('Pag_Men_Esclusa_spezzata_dalla_coppia') + '</span>'
          : MF('Pag_Men_Esclusa_stessi_canali', esc(x.nomeCon || x.con || '')))).join('') +
      '</div>' + righe.join('') + '</div>';
  }

  applicaVoci();
  ridisegna();
