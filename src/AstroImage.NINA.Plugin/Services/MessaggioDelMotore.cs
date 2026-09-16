using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using AstroImage.NINA.Plugin.Localization;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  LA FRASE LA SCRIVE CHI HA UNO SCHERMO.
     *  ────────────────────────────────────────────────────────────────────────────
     *  Strategy e' un calcolatore dietro una porta e parla una lingua sola,
     *  l'italiano. Non e' una svista: non ha un utente. Il ponte un utente ce l'ha,
     *  ed e' bilingue — inglese di serie, italiano a scelta.
     *
     *  Se la frase arrivasse dal motore gia' scritta, un utente inglese leggerebbe
     *  italiano; e per correggere una traduzione sbagliata bisognerebbe entrare nel
     *  repository del motore, che e' privato e proprietario. Questo plugin invece va
     *  nel registro pubblico di N.I.N.A. con licenza MPL-2.0: il testo che la gente
     *  legge deve stare dove la gente puo' leggerlo e correggerlo. Sta qui.
     *
     *  IL CONTRATTO. Il servizio manda tre cose insieme:
     *      codice      quale errore, da un elenco chiuso
     *      dati        i valori nudi — numeri e nomi, non pezzi di frase
     *      messaggio   la frase italiana, per i log e per chi interroga con curl
     *  Qui si compone dalla resx usando codice e dati. Se il codice non e' noto, o
     *  se mancano i valori che la frase richiede, si mostra il messaggio italiano
     *  del motore: si degrada in italiano, non nel vuoto. Una mezza frase con un
     *  buco dentro sarebbe peggio di una frase intera nella lingua sbagliata.
     *
     *  PERCHE' NON SI TRADUCE QUI DENTRO. `dettaglio` porta il messaggio grezzo di
     *  un'eccezione JavaScript: non lo scriviamo noi, quindi nessuno lo puo'
     *  tradurre. Si incastona dentro una frase nostra invece di lasciarlo solo
     *  davanti a qualcuno.
     */

    /// <summary>
    /// Scrive nella lingua scelta l'errore che arriva da Strategy, partendo dal suo
    /// codice e dai suoi dati. Ripiega sulla frase italiana del motore per i codici
    /// che non conosce.
    /// </summary>
    public static class MessaggioDelMotore {

        /*  UN CODICE, UNA CHIAVE, I CAMPI NELL'ORDINE IN CUI LA FRASE LI USA.
         *
         *  L'ordine e' il contratto fra questa tabella e il testo della resx: il
         *  primo campo diventa {0}, il secondo {1}, il terzo {2}. Cambiare l'ordine
         *  qui senza cambiare la frase la' non rompe niente a compilazione e produce
         *  una frase che dice il falso — per questo c'e' una prova che, per ogni
         *  chiave, conta i segnaposto e li confronta con i campi dichiarati qui.
         *
         *  L'elenco rispecchia i CODICI che il servizio dichiara. Un codice nuovo di
         *  la' e non di qua esce in italiano: e' il ripiego, ed e' voluto.
         *  Chi manda un codice nuovo lo dichiara, e chi lo riceve lo aggiunge
         *  qui: finche' non lo fa, la frase resta quella italiana.
         */
        private static readonly Dictionary<string, (string Chiave, string[] Campi)> Frasi =
            new Dictionary<string, (string, string[])> {
                //  ROUTINE — l'utente ci sbatte facendo cose normali
                ["cielo_assente"]         = ("Motore_CieloAssente",        new[] { "min", "max" }),
                ["cielo_non_valido"]      = ("Motore_CieloNonValido",      new[] { "ricevuto" }),
                ["cielo_implausibile"]    = ("Motore_CieloImplausibile",   new[] { "ricevuto", "min", "max" }),
                ["bersaglio_sconosciuto"] = ("Motore_BersaglioSconosciuto", new[] { "chiesto" }),
                /*  Il catalogo intero sulla porta (17 settembre 2026): un oggetto che si trova e non si prescrive, e le
                 *  situazioni sono due — una stella, e un oggetto a cui manca l'evidenza della classe. */
                ["bersaglio_non_esteso"]   = ("Motore_BersaglioNonEsteso",   new[] { "nome" }),
                ["bersaglio_senza_classe"] = ("Motore_BersaglioSenzaClasse", new[] { "nome" }),
                /*  DUE CODICI DOVE PRIMA CE N'ERA UNO, e non e' pignoleria: sono due
                 *  situazioni che si correggono in modo diverso. Un identificativo che
                 *  non risulta si corregge scegliendone un altro o descrivendo il pezzo;
                 *  un record a cui mancano dei campi si corregge riempiendoli. Il
                 *  vecchio `banco_sconosciuto` li faceva sembrare la stessa cosa e ci
                 *  incastonava dentro il messaggio grezzo del motore, che nessuno puo'
                 *  tradurre.
                 *
                 *  `campi` arriva come lista e diventa una riga sola: ci pensa
                 *  `Appiattisci` col separatore localizzato.                          */
                ["setup_sconosciuto"]     = ("Motore_SetupSconosciuto",    new[] { "pezzo", "chiesto" }),
                ["setup_incompleto"]      = ("Motore_SetupIncompleto",     new[] { "pezzo", "chiesto", "campi" }),
                ["nessuna_prescrizione"]  = ("Motore_NessunaPrescrizione", new string[0]),
                /*  Il contratto del banco (16 settembre 2026): una chiave che il motore non conosce, nominata col suo
                 *  percorso, e un valore che non e' un numero del suo campo. */
                ["banco_chiave_sconosciuta"] = ("Motore_BancoChiaveSconosciuta", new[] { "chiave" }),
                ["banco_valore_non_valido"]  = ("Motore_BancoValoreNonValido",   new[] { "pezzo", "chiave", "ricevuto" }),
                /*  La modalita' di ripresa e la politica di sessione (16 settembre 2026): il pannello le manda dai suoi
                 *  bottoni, quindi un rifiuto vuol dire due versioni diverse. La frase usa solo `valide`: `ricevuto` il
                 *  servizio lo manda nullo quando il valore non e' un testo, e un nullo qui diventa un vuoto. */
                ["modalita_sconosciuta"]  = ("Motore_ModalitaSconosciuta", new[] { "valide" }),
                ["politica_sconosciuta"]  = ("Motore_PoliticaSconosciuta", new[] { "valide" }),
                //  DIAGNOSTICA — se un utente la vede, il problema non e' la lingua
                ["via_sconosciuta"]       = ("Motore_ViaSconosciuta",      new string[0]),
                ["richiesta_incompleta"]  = ("Motore_RichiestaIncompleta", new[] { "mancano" }),
                ["richiesta_troppo_grande"] = ("Motore_RichiestaTroppoGrande", new[] { "limite_byte" }),
                ["json_illeggibile"]      = ("Motore_JsonIlleggibile",     new[] { "dettaglio" }),
                ["motore_in_errore"]      = ("Motore_InErrore",            new[] { "dettaglio" }),
            };

        /*  LE FRASI DI RISERVA PER UN CAMPO CHE MANCA (16 settembre 2026). Un pezzo che il banco non ha — la camera
         *  spenta — non ha un nome: la frase che lo cita per nome non si compone, e al suo posto va questa. */
        private static readonly Dictionary<string, (string Manca, string Chiave, string[] Campi)> FrasiSenza =
            new Dictionary<string, (string, string, string[])> {
                ["setup_incompleto"] = ("chiesto", "Motore_SetupIncompletoSenzaNome", new[] { "pezzo", "campi" }),
            };

        /*  I PEZZI E I CAMPI DEL MOTORE, NELLA LINGUA DI CHI GUARDA. Prima la frase diceva «al ottica» e metteva in fila
         *  `aperture_mm, throughput`. Un nome che qui non c'e' passa com'e': meglio un nome del motore che un buco. */
        private static readonly Dictionary<string, string> ParolaDelPezzo = new Dictionary<string, string> {
            ["ottica"] = "Motore_Pezzo_ottica", ["camera"] = "Motore_Pezzo_camera",
            ["montatura"] = "Motore_Pezzo_montatura", ["riduttore"] = "Motore_Pezzo_riduttore",
        };
        private static readonly Dictionary<string, string> ParolaDelCampo = new Dictionary<string, string> {
            ["voce"] = "Motore_Campo_voce", ["matrice"] = "Motore_Campo_matrice", ["fattore"] = "Motore_Campo_fattore",
            ["pixel_um"] = "Motore_Campo_pixel_um", ["width_px"] = "Motore_Campo_width_px", ["height_px"] = "Motore_Campo_height_px",
            ["aperture_mm"] = "Motore_Campo_aperture_mm", ["obstruction_linear"] = "Motore_Campo_obstruction_linear",
            ["throughput"] = "Motore_Campo_throughput", ["focal_mm"] = "Motore_Campo_focal_mm",
        };

        /// <summary>I codici che questa classe sa scrivere. Serve alle prove.</summary>
        public static IReadOnlyCollection<string> CodiciNoti => Frasi.Keys.ToList();

        /// <summary>Le frasi di riserva per un campo che manca, codice per codice. Serve alle prove.</summary>
        public static IReadOnlyDictionary<string, (string Manca, string Chiave, string[] Campi)> FrasiDiRiserva => FrasiSenza;

        /// <summary>Le chiavi delle parole di pezzi e campi. Serve alle prove.</summary>
        public static IEnumerable<string> ChiaviDelleParole => ParolaDelPezzo.Values.Concat(ParolaDelCampo.Values);

        /// <summary>La chiave di risorsa e i campi attesi per un codice, o null.</summary>
        public static (string Chiave, string[] Campi)? FraseDi(string? codice) =>
            codice != null && Frasi.TryGetValue(codice, out var f) ? f : ((string, string[])?)null;

        /// <summary>
        /// La frase da mostrare. <paramref name="dellMotore"/> e' la frase italiana
        /// che il servizio ha mandato: e' il ripiego, e torna tale e quale quando il
        /// codice non e' noto o quando i dati non bastano a comporre.
        /// </summary>
        public static string? Rendi(string? codice, IReadOnlyDictionary<string, string>? dati,
                                    string? dellMotore) {
            if (codice is null || !Frasi.TryGetValue(codice, out var f)) return dellMotore;
            if (FrasiSenza.TryGetValue(codice, out var r) && (dati is null || !dati.ContainsKey(r.Manca)))
                f = (r.Chiave, r.Campi);

            var valori = new object?[f.Campi.Length];
            for (var i = 0; i < f.Campi.Length; i++) {
                if (dati is null || !dati.TryGetValue(f.Campi[i], out var v) || v is null)
                    /*  Un campo che manca non si rimpiazza con il vuoto: la frase
                     *  uscirebbe monca proprio nel punto che serve a correggere
                     *  l'errore. Meglio quella italiana, che almeno e' intera. */
                    return dellMotore;
                valori[i] = f.Campi[i] == "pezzo" && ParolaDelPezzo.TryGetValue(v, out var kp) ? Loc.T(kp) : v;
            }
            return Loc.F(f.Chiave, valori);
        }

        /*  I DATI ARRIVANO COME JSON e vanno resi testo una volta sola, qui.
         *
         *  I numeri si formattano con la cultura INVARIANTE, non con quella
         *  dell'utente: un SQM e' 20.8 in tutta l'app, nella pagina, nei log del
         *  motore e nel file che va al Sequenziatore. Farlo diventare 20,8 solo
         *  dentro un messaggio d'errore vorrebbe dire che lo stesso numero si
         *  scrive in due modi a seconda di dove lo leggi, e chi confronta un
         *  messaggio con un campo si ferma a chiedersi se sono lo stesso valore.
         */
        public static IReadOnlyDictionary<string, string> Appiattisci(
                IDictionary<string, JsonElement>? dati) {
            var fuori = new Dictionary<string, string>();
            if (dati is null) return fuori;
            foreach (var kv in dati) {
                var v = kv.Value;
                /*  UN NULLO O UN VUOTO NON E' UN VALORE (16 settembre 2026): se entrasse, `Rendi` comporrebbe la frase
                 *  con un buco — «camera «»» — invece di ripiegare. */
                if (v.ValueKind == JsonValueKind.Null || v.ValueKind == JsonValueKind.Undefined) continue;
                if (v.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(v.GetString())) continue;
                fuori[kv.Key] = v.ValueKind switch {
                    JsonValueKind.String => v.GetString() ?? string.Empty,
                    JsonValueKind.Number => v.GetDouble().ToString("0.####", CultureInfo.InvariantCulture),
                    JsonValueKind.True   => "true",
                    JsonValueKind.False  => "false",
                    JsonValueKind.Null   => string.Empty,
                    //  Una lista diventa una riga sola: «sito, banco». Il separatore
                    //  e' localizzato perche' cambia da lingua a lingua.
                    JsonValueKind.Array  => string.Join(Loc.T("Motore_Separatore"),
                                                v.EnumerateArray().Select(x =>
                                                    x.ValueKind == JsonValueKind.String
                                                        ? ParolaDi(x.GetString() ?? string.Empty)
                                                        : x.ToString())),
                    _ => v.ToString(),
                };
            }
            return fuori;
        }

        /*  Un nome di campo del motore dentro una lista diventa la sua parola; un testo che non e' un nome noto resta com'e'. */
        private static string ParolaDi(string s) => ParolaDelCampo.TryGetValue(s, out var k) ? Loc.T(k) : s;
    }
}
