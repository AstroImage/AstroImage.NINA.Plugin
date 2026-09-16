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
    const dove = (r && r.messaggio) ? '<div class="mg-dove">' + esc(r.messaggio) + '</div>' : '';
    $('modi').innerHTML =
      '<div class="box err motore-giu">' +
      '<b>' + esc(T('Pag_StrategyNonRisponde')) + '</b>' + dove +
      '<div class="mg-perche">' + esc(T('Pag_PercioNienteModi')) + '</div></div>';
    stato((r && r.messaggio) ? r.messaggio : T('Pag_ServizioGiu'), 'no');
  }

  function ridisegna() {
    chiedi('modalita').then(r => {
      /*  Qui c'era `return`, e basta: senza modi la pagina non disegnava i riquadri
          e non diceva niente. Adesso lo dice, con l'indirizzo. */
      if (!r.ok || !r.modalita || !r.modalita.length) { motoreGiu(r); return; }
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
    });
    chiedi('camera').then(r => { if (r.ok) camera = r.camera || null; });
    chiedi('banco').then(r => { if (r.ok) { bancoLetto = r; disegnaBanco(); } });
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
    'tel.ostruzione': 'Pag_Banco_tel_ostruzione', 'tel.trasmissione': 'Pag_Banco_tel_trasmissione',
    'tel.focale_mm': 'Pag_Banco_tel_focale_mm', 'tel.rapporto': 'Pag_Banco_tel_rapporto',
    'red': 'Pag_Banco_red', 'mnt': 'Pag_Banco_mnt', 'cam': 'Pag_Banco_cam',
    'mnt.rms_caratteristico_arcsec': 'Pag_Banco_mnt_rms_caratteristico_arcsec',
    'luna.riferimento_deg': 'Pag_Banco_luna_riferimento_deg' };
  const NOTA_CAMPO_BANCO = { 'mnt.rms_caratteristico_arcsec': 'Pag_Banco_RmsNota',
    'luna.riferimento_deg': 'Pag_Banco_LunaNota' };
  const PAROLA_DIVERGENZA_BANCO = {
    'dichiarato_diverso_dal_catalogo': 'Pag_Banco_Div_dichiarato_diverso_dal_catalogo',
    'focale_diversa_dal_catalogo': 'Pag_Banco_Div_focale_diversa_dal_catalogo',
    'apertura_diversa_da_nina': 'Pag_Banco_Div_apertura_diversa_da_nina',
    'geometria_diversa_dal_catalogo': 'Pag_Banco_Div_geometria_diversa_dal_catalogo' };
  /*  I campi della geometria della camera, per la divergenza fra il driver e la voce riconosciuta. */
  const PAROLA_CAMPO_CAMERA = { pixel_um: 'Pag_Banco_Cam_pixel_um', width_px: 'Pag_Banco_Cam_width_px',
    height_px: 'Pag_Banco_Cam_height_px', matrice: 'Pag_Banco_Cam_matrice' };
  const PAROLA_FONTE_BANCO = { nina: 'Pag_Banco_Fonte_nina', catalogo: 'Pag_Banco_Fonte_catalogo',
    dichiarato: 'Pag_Banco_Fonte_dichiarato', riferimento: 'Pag_Banco_Fonte_riferimento' };
  /*  La camera c'e' dal 16 settembre 2026: la geometria la dice il driver, la voce di catalogo porta la fisica. */
  /*  La Luna c'e' dal 16 settembre 2026: la distanza di riferimento da cui Strategy ricava la soglia di ogni filtro. */
  const PEZZI_DEL_BLOCCO = ['ottica', 'riduttore', 'camera', 'montatura', 'luna'];

  /*  QUELLO CHE IL BANCO NON DICEVA (regia, 16 settembre 2026): `parziale` del prodotto, in giallo, una parola per tipo
   *  coi numeri che il motore manda. Un tipo senza parola si scrive col suo nome, e la guardia del motore lo dice in
   *  rosso (tools/gate-parziale.js). Le chiavi sono letterali; i campi, nell'ordine dei segnaposto. */
  const PAROLA_PARZIALE = {
    'rumore_di_lettura_non_noto': ['Pag_Parziale_rumore_di_lettura_non_noto', ['assunto']],
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
    'guida_non_dichiarata': ['Pag_Parziale_guida_non_dichiarata', ['assunto']],
    'notti_serene_non_dichiarate': ['Pag_Parziale_notti_serene_non_dichiarate', ['assunto']],
    'ruota_non_dichiarata': ['Pag_Parziale_ruota_non_dichiarata', ['usata']],
    'convenzioni_di_posa': ['Pag_Parziale_convenzioni_di_posa', ['assunte.download', 'assunte.settle', 'assunte.ditherEvery']],
  };
  const campoDelDato = (d, via) => { let o = d; for (const k of via.split('.')) o = (o == null ? o : o[k]); return o; };
  function parzialeDelProdotto(lista) {
    if (!lista || !lista.length) return '';
    const righe = lista.map(p => {
      const w = PAROLA_PARZIALE[p.tipo];
      if (!w) return '<li>' + esc(p.tipo + ' — ' + p.effetto) + '</li>';
      const valori = w[1].map(via => { const v = campoDelDato(p.dati || {}, via);
        return v == null ? '—' : Array.isArray(v) ? esc(v.join(', ')) : cifra(v); });
      return '<li>' + MF(w[0], ...valori) + '</li>';
    });
    return '<div class="box" style="color:#e0a030"><b>' + esc(T('Pag_ParzialeTitolo')) + '</b>' +
      '<ul style="margin:.4em 0 0 1.1em;padding:0">' + righe.join('') + '</ul></div>';
  }

  /*  LA LUNA TIENE FUORI (regia, 16 settembre 2026): notte per notte, i canali sotto la loro soglia di Luna, coi numeri che
   *  Strategy manda — la fase, la distanza, la soglia. Giallo: il canale esce dalla notte, non dal progetto. Il filtro si
   *  scrive col nome della ruota, come nella sequenza; una notte in cui la Luna tiene fuori tutto non ha una riga sopra,
   *  e resta qui. */
  function lunaFuori(l) {
    const fuori = (l && l.fuori) || [];
    if (!fuori.length) return '';
    const righe = fuori.map(f => '<li>' + MF(f.congiunto ? 'Pag_LunaFuoriCongiunto' : 'Pag_LunaFuori',
      cifra(f.notte), esc(f.id), esc(vetroNellaRuota(f.filtro)), cifra(f.fasePercento),
      cifra(f.distanza, 0), cifra(f.soglia, 0)) + '</li>');
    return '<div class="box" style="color:#e0a030"><b>' + esc(T('Pag_LunaFuoriTitolo')) + '</b>' +
      '<ul style="margin:.4em 0 0 1.1em;padding:0">' + righe.join('') + '</ul></div>';
  }

  /*  LA RICHIESTA: ogni campo della lista col valore che gli spetta — dal profilo di N.I.N.A. se e' suo, dalla
   *  dichiarazione se e' dichiarabile o e' un riconoscimento. Nessun valore di serie: quello che manca manca, e il
   *  servizio dice che cosa. */
  function bancoDaMandare() {
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
    const idCamera = (b.cam && typeof b.cam === 'object') ? b.cam.id : null;
    if (camera && typeof camera === 'object') b.cam = Object.assign({}, camera, idCamera ? { id: idCamera } : {});
    else if (camera && !idCamera) b.cam = camera;
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

  function disegnaBanco() {
    const box = $('banco');
    if (!box) return;
    if (!campiDelBanco.length) { box.innerHTML = ''; return; }
    const dichiarato = (bancoLetto && bancoLetto.dichiarato) || {};
    const nina = (bancoLetto && bancoLetto.nina) || {};
    const pb = bancoUsato;
    const campi = campiDelBanco.filter(c => PEZZI_DEL_BLOCCO.indexOf(c.pezzo) >= 0 &&
      !/\.id$/.test(c.chiave) && PAROLA_CAMPO_BANCO[c.chiave]);
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
        valore = '<input data-banco="' + esc(id) + '" value="' + escOVuoto(dichiarato[id]) +
          '" style="width:130px" spellcheck="false">' +
          (pezzo && pezzo.voce && (c.pezzo !== 'camera' || pezzo.id)
            ? ' <span style="font-size:12px;opacity:.7">' + MF('Pag_Banco_Riconosciuto', esc(pezzo.voce)) +
              (pezzo.riconoscimento === 'nome_e_geometria' ? ' ' + MF('Pag_Banco_CamDalDriver') : '') + '</span>' : '') +
          (c.pezzo === 'camera' && pezzo && !pezzo.id
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
        valore = '<input data-banco="' + esc(c.chiave) + '" data-numero="1" value="' + escOVuoto(dichiarato[c.chiave]) +
          '" style="width:70px" spellcheck="false">' + unita + fonte(delProdotto(c));
      } else return '';
      const nota = NOTA_CAMPO_BANCO[c.chiave]
        ? '<div style="font-size:12px;opacity:.7">' + esc(T(NOTA_CAMPO_BANCO[c.chiave])) + '</div>' : '';
      return '<tr><th>' + etichetta + '</th><td>' + valore + nota + '</td></tr>';
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
      '<div style="margin:.4em 0;opacity:.7;font-size:12.5px">' + MF('Pag_BancoNota') + '</div>' +
      '<table style="width:100%">' + campi.map(riga).join('') + '</table>' +
      (divergenze.length ? '<ul style="margin:.6em 0 0 1.1em;padding:0;color:#e0a030">' + divergenze.map(divergenza).join('') + '</ul>' : '') +
      '<div style="margin-top:.7em"><button id="salvaBanco">' + esc(T('Pag_SalvaBanco')) + '</button>' +
      '<span id="esitoBanco" style="margin-left:.6em;opacity:.8"></span></div></div>';
    const salva = $('salvaBanco');
    if (salva) salva.addEventListener('click', () => {
      const valori = {};
      Array.prototype.forEach.call(box.querySelectorAll('input[data-banco]'), i => {
        valori[i.getAttribute('data-banco')] = valoreScritto(i.value, i.hasAttribute('data-numero'));
      });
      $('esitoBanco').textContent = T('Pag_Salvo');
      chiedi('salvaBanco', { banco: valori }).then(r2 => {
        $('esitoBanco').textContent = r2.ok ? T('Pag_Salvato')
          : T('Pag_NonSalvatoPerche').replace('{0}', r2.messaggio || r2.codice || '');
        ritiraDalloSchermo(r2.ritirata);
        if (r2.ok) chiedi('banco').then(r3 => { if (r3.ok) { bancoLetto = r3; disegnaBanco(); } });
      });
    });
  }

  async function vai() {
    $('vai').disabled = true;
    stato(T('Pag_StoChiedendo'));
    $('uscita').innerHTML = '';
    parzialeUsato = null;
    aggiornaProvenienzeDelSito();

    const r = await chiedi('prescrizione', {
      /* IL SITO E' QUELLO DEL PROFILO, non piu' sette numeri scritti qui dentro.
         Se manca qualcosa manca davvero: nessun ripiego, nessun valore di serie. */
      sito:      sito || {},
      /*  IL BANCO IN TRE PEZZI (16 settembre 2026): quello che N.I.N.A. tiene — focale e rapporto, riletti ogni volta —,
       *  quello che il catalogo del motore riconosce dall'identificativo dichiarato, e quello che chi riprende dichiara
       *  perche' non ce l'ha nessuno dei due. Si compone dalla lista dei campi che il servizio pubblica, non da una lista
       *  di qui; la camera e' quella che il driver dichiara. Dedurre «askar71f» dal nome di un dispositivo sarebbe
       *  indovinare l'identita' fisica da un'etichetta: l'identificativo lo dichiara chi riprende, come per i filtri. */
      banco:     bancoDaMandare(),
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
      /*  Pose e ore della notte le fa il motore (`totale`), e qui si stampano: il numero che la pagina mostra lo manda
       *  il motore (contratto delle schede §6 ter). Un motore che non le manda lascia il trattino. */
      const tot = m.totale || {};
      const pose = cifra(tot.pose);
      const ore = tot.ore == null ? '—' : cifra(tot.ore, 2) + ' h';
      const tasto = r.consegnabile
        ? '<button class="manda" data-notte="' + s.notte +
          '" data-prescrizione="' + esc(r.prescrizione || '') + '">' + T('Pag_MandaANina') + '</button>'
        : '<span style="opacity:.45">—</span>';
      righe += '<tr><td class="n">' + s.notte + '</td>' +
               '<td>' + esc((m.quando && m.quando.data) || '—') + '</td>' +
               '<td class="n">' + m.blocchi.length + '</td>' +
               '<td class="n">' + pose + '</td>' +
               '<td class="n">' + ore + '</td>' +
               /*  IL NOME CHE VEDRAI IN SEQUENZA, non l'etichetta di banda del motore.
                  «HO · L» erano i nomi dei CANALI, e leggerli accanto al tasto che
                  consegna faceva credere che quelli sarebbero finiti nel Sequenziatore.
                  Il vetro vero e' in `posa.<canale>.ex.spec.filter.id`, e la
                  dichiarazione dice come si chiama sulla tua ruota. */
               '<td>' + esc(m.blocchi.map(b => vetroDelBlocco(p, b)).join(' · ')) + '</td>' +
               '<td>' + m.blocchi.map(b => guadagnoDelBlocco(b)).join(' · ') + '</td>' +
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
      /*  Le ore utili sono la somma delle notti chieste, e il numero delle notti lo dice il servizio: senza, accanto
          alla notte chiesta, sembravano le ore di una notte sola. */
      '<tr><th>' + T('Pag_RigaOreUtili') + '</th><td>' + (p.notte.nottiDisponibili == null
        ? cifra(p.notte.oreDisponibili, 2) + ' h'
        : MF(p.notte.nottiDisponibili === 1 ? 'Pag_OreUtiliInUnaNotte' : 'Pag_OreUtiliInNotti',
             cifra(p.notte.oreDisponibili, 2), cifra(p.notte.nottiDisponibili))) + '</td></tr>' +
      /*  IL VERDETTO CON LE SUE NOTTI (regia, 16 settembre 2026): quanto del progetto coprono le notti chieste, e quante
          ne servono per il minimo. I numeri li fa Strategy; senza, la riga non c'e'. */
      ((v => (v && v.coperturaPercento != null)
        ? '<tr><th>' + T('Pag_RigaVerdetto') + '</th><td>' +
          /*  la Luna tiene fuori un canale da tutte le notti chieste: nessuna copertura da dire, il perche' */
          (v.lunaFuori
            ? (v.perIlMinimo == null
              ? MF('Pag_Verdetto_LunaFuoriOltre', cifra(v.notti), cifra(v.massimo))
              : MF('Pag_Verdetto_LunaFuori', cifra(v.notti), cifra(v.perIlMinimo)))
          : v.perIlMinimo == null
            ? MF('Pag_Verdetto_MinimoOltre', cifra(v.notti), cifra(v.coperturaPercento), cifra(v.massimo))
            : v.perIlMinimo > v.notti
              ? MF('Pag_Verdetto_Minimo', cifra(v.notti), cifra(v.coperturaPercento), cifra(v.perIlMinimo))
              : MF('Pag_Verdetto_Coperto', cifra(v.notti), cifra(v.coperturaPercento))) + '</td></tr>'
        : '')(p.prescrizione && p.prescrizione.verdetto)) +
      /*  SU QUALI VETRI E' STATA CALCOLATA, e non e' un dettaglio da nascondere.
         Senza questa riga «non e' cambiato niente perche' il motore avrebbe scelto
         gli stessi vetri» e «non e' cambiato niente perche' la ruota non e' partita»
         si somigliano troppo — e la prima volta ci ha fregati per mezz'ora. */
      '<tr><th>' + T('Pag_RigaCalcolataSu') + '</th><td>' +
        (r.ruotaAggiunta && r.ruotaAggiunta.length
          ? MF('Pag_CalcolataTuaRuota', r.ruotaAggiunta.map(esc).join(' · '))
          : '<span style="color:#e0a030">' + MF('Pag_CalcolataDiSerie') + '</span>') +
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
      disegnaMenu(p.prescrizione) +
      '<div class="box"><table>' +
      '<tr><th>' + T('Pag_ColNotte') + '</th><th>' + T('Pag_ColData') + '</th><th>' +
        T('Pag_ColBlocchi') + '</th><th>' + T('Pag_ColPose') + '</th><th>' +
        T('Pag_ColDurata') + '</th><th>' + T('Pag_ColFiltri') + '</th><th>' + T('Pag_ColGuadagno') + '</th><th></th></tr>' +
      righe + '</table></div>' +
      lunaFuori(p.luna) +
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
      elenco(T('Pag_Scartato'), r.scartati) + elenco(T('Pag_DaSapere'), r.note) + '</div>';
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
      (assunto ? MF('Pag_Prov_assunto', esc(assunto.dati.assunto), unitaDelSito[campo] || '')
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

  function disegnaSito(r) {
    sito = r.sito || null;
    sitoScritto = r.dichiarato || {};
    sitoProv = r.provenienza || {};
    sitoManca = r.manca || null;

    /* La provenienza si vede accanto al numero: un SQM misurato e uno scritto a mano
       valgono lo stesso per il motore, ma non per chi guarda. */
    const riga = (etichetta, campo, unita, scrivibile) => {
      const v = sito ? sito[campo] : null;
      unitaDelSito[campo] = unita;
      return '<tr><th>' + etichetta + '</th><td>' +
        (scrivibile
          ? '<input data-sito="' + campo + '" value="' + (sitoScritto[campo] === null ||
              sitoScritto[campo] === undefined ? '' : sitoScritto[campo]) +
            '" style="width:70px" spellcheck="false"> ' +
            (v === null || v === undefined ? '' : '<b>' + num(v) + '</b> ' + unita)
          : '<b>' + num(v) + '</b> ' + unita) +
        ' ' + provenienzaDelSito(campo) + '</td></tr>';
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
      rigaOrizzonte(r) +
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
      /*  Seeing e guida sono dichiarazioni, e lo dicono: servono al campionamento e al confronto, e non entrano nella
       *  prescrizione. Nelle decisioni entra l'RMS caratteristico della montatura, che si dichiara nel banco. */
      /*  E l'SQM dice che cosa vuole: il carattere del sito, e nel dubbio il valore piu' chiaro (regia, 16 settembre 2026). */
      '<div style="margin-top:.5em;opacity:.7;font-size:12.5px">' + MF('Pag_SqmNota') + '</div>' +
      '<div style="margin-top:.3em;opacity:.7;font-size:12.5px">' + MF('Pag_SeeingNota') + '</div>' +
      '<div style="margin-top:.3em;opacity:.7;font-size:12.5px">' + MF('Pag_RmsNota') + '</div></div>';

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
   *  lingua. Senza la data nei dati non se ne scrive una. */
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
      default: return MF('Pag_Men_Motivo_generico', nomeDellaBanda);
    }
  }
  function testoStelle(perche) {
    if (perche && perche.tipo === 'canale_spento')
      return MF('Pag_Men_StelleNonRiprendibili_spento', escOVuoto(perche.banda));
    const m = (perche && perche.mancano) || [];
    return m.length ? MF('Pag_Men_StelleNonRiprendibili_mancano', m.map(esc).join(', '))
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
     *  classe o sull'attrezzatura. E le bloccate contano fra le carte: e' per questo che una prima scelta che la
     *  ruota non fa non fa mai collassare il menu a una carta. */
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
        esc(sost.nomeChiesta || sost.chiesta), esc(presaOra), esc(sost.nomeCon || sost.con || '')) + '</div>');
    else if (pr.roadPicked && !pr.roadAutoSame)
      righe.push('<div class="sc-s">' + MF('Pag_Men_StaiScegliendo', esc(auto ? auto.name : (pr.roadAuto || ''))) + '</div>');
    if (!pr.roadPicked && pr.roadAutoRisolta)
      righe.push('<div class="sc-s">' + MF('Pag_Men_AutoRisolta', esc(pr.roadAutoRisolta.nomeDecisa),
        testoStelle(pr.roadAutoRisolta.perche), esc(pr.roadAutoRisolta.nomePresa)) + '</div>');
    for (const l of (pr.limitiDellaStrada || []).filter(l => l.tipo === 'passata_stelle_non_riprendibile'))
      righe.push('<div class="sc-s">' + MF('Pag_Men_LimiteStelle', esc(presaOra), testoStelle(l.perche)) + '</div>');

    return '<div class="box stratbox" id="menu">' + titolo + '<div class="roadgrid">' +
      cartaAuto + voci.map(carta).join('') +
      chiuse.map(b => ferma(b.name || b.road, 'Pag_Men_NonDisponibile',
        '<span class="p-warn">' + (b.perche || []).map(testoDelMotivo).join(' · ') + '</span>')).join('') +
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
