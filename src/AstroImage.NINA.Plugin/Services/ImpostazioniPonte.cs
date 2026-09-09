using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AstroImage.NINA.Plugin.Localization;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  LE POCHE COSE CHE IL PONTE RICORDA DI TE, E NON DELLA POSTAZIONE.
     *
     *  Tutto il resto di quello che il ponte salva — la ruota dichiarata, il sito — sta
     *  nel profilo di N.I.N.A., e ci sta apposta: cambiano davvero fra Borno e Milano.
     *  La lingua no. Sei sempre tu che leggi. Un valore per profilo vorrebbe dire che
     *  cambi postazione e il pannello cambia lingua, e quel difetto non si presenta come
     *  un difetto: si presenta come un mistero.
     *
     *  Quindi un file, fuori dai profili. N.I.N.A. non offre ai plugin nessun archivio
     *  globale — `PluginOptionsAccessor` e' sempre e solo del profilo attivo — e questa
     *  e' la stessa strada gia' collaudata in AdaptiveAgentForPHD2.
     *
     *  IL FILE STA FUORI DALLA CARTELLA DEL PLUGIN, e non e' un caso: i plugin si
     *  installano sotto `Plugins\3.0.0\<nome>`, che un aggiornamento puo' riscrivere.
     *  Questo sta un piano sopra, in `Plugins\<nome>`, e sopravvive.
     *
     *  QUI DENTRO NON SI NOMINA N.I.N.A., come in Loc e per la stessa ragione: i test
     *  girano senza le sue DLL. Un guasto non si scrive nel registro da qui — si mette
     *  in <see cref="Nota"/> e lo scrive chi chiama, che e' la stessa regola di
     *  MemoriaRuota e delle altre dichiarazioni.
     */
    public sealed class ImpostazioniPonte : INotifyPropertyChanged {

        /// <summary>Segui la lingua di N.I.N.A.</summary>
        public const string SegueNina = "";

        /*  INGLESE PER CHI INSTALLA LA PRIMA VOLTA.
         *
         *  Non «segui N.I.N.A.», che pure sarebbe stato difendibile: il catalogo lo apre
         *  gente in mezzo mondo, e la lingua che tutti capiscono almeno un po' e' una
         *  sola. Chi vuole l'italiano lo sceglie, ed e' a un clic. */
        public const string LinguaDiSerie = "en";

        private const string It = "it";
        private const string En = "en";

        /// <summary>
        /// Dove finisce il file. Un piano SOPRA la cartella dove N.I.N.A. installa i
        /// plugin, cosi' un aggiornamento non se lo porta via.
        /// </summary>
        public static string PercorsoDiSerie => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NINA", "Plugins", "AstroImage.NINA.Plugin", "impostazioni.json");

        private readonly string _percorso;
        private string _lingua = LinguaDiSerie;
        private bool _zitto;

        /// <summary>
        /// Che cosa e' andato storto leggendo o scrivendo, in chiaro. Null se tutto bene.
        /// Non si scrive nel registro da qui: lo fa chi chiama, che conosce N.I.N.A.
        /// </summary>
        public string? Nota { get; private set; }

        private ImpostazioniPonte(string percorso) { _percorso = percorso; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Percorso => _percorso;

        /// <summary>
        /// La lingua del pannello: "" segue N.I.N.A., "it" e "en" la impongono.
        /// Assegnarla la applica subito e la scrive: e' l'unica cosa che l'utente
        /// tocca, e un'impostazione che si perde chiudendo N.I.N.A. non e'
        /// un'impostazione.
        /// </summary>
        public string Lingua {
            get => _lingua;
            set {
                var v = Normalizza(value);
                if (_lingua == v && !_zitto) { return; }
                _lingua = v;
                Loc.Instance.ForzaLingua(v);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Lingua)));
                if (!_zitto) { Salva(); }
            }
        }

        /*  UN VALORE CHE NON CONOSCO VALE COME NON AVERLO DETTO, e «non detto» qui e'
         *  l'inglese. E' l'unico modo di avere una regola sola: file assente, file
         *  rotto, valore sconosciuto — tre strade che finiscono nello stesso posto.
         *  Farle finire in due posti diversi vorrebbe dire spiegare, un domani, perche'
         *  un file corrotto segue N.I.N.A. e un'installazione nuova no. */
        private static string Normalizza(string? v) {
            var s = (v ?? "").Trim().ToLowerInvariant();
            return s == SegueNina || s == It || s == En ? s : LinguaDiSerie;
        }

        /// <summary>
        /// Legge il file e APPLICA la lingua. Un file che non c'e' non e' un guasto: e'
        /// la prima volta. Un file rotto invece si dice, ma non si cancella e non
        /// impedisce al plugin di partire.
        /// </summary>
        public static ImpostazioniPonte Carica(string? percorso = null) {
            var i = new ImpostazioniPonte(percorso ?? PercorsoDiSerie);
            i._zitto = true;
            try {
                if (File.Exists(i._percorso)) {
                    var d = JsonSerializer.Deserialize<Documento>(File.ReadAllText(i._percorso));
                    if (d is null) {
                        i.Nota = "il file delle impostazioni non dice niente: valgono quelle di serie.";
                    } else {
                        var letta = Normalizza(d.Lingua);
                        if (d.Lingua is not null && letta != d.Lingua.Trim().ToLowerInvariant()) {
                            i.Nota = $"lingua «{d.Lingua}» sconosciuta: vale «{letta}».";
                        }
                        i.Lingua = letta;
                    }
                }
            } catch (Exception e) {
                /*  Un file illeggibile NON deve impedire al plugin di partire, e non si
                 *  riscrive da solo: chi l'ha modificato a mano potrebbe volerlo
                 *  aggiustare. Si riparte da quelle di serie e si dice perche'. */
                i.Nota = "le impostazioni non si sono potute leggere (" + e.Message +
                         "): valgono quelle di serie, e il file non e' stato toccato.";
            } finally {
                i._zitto = false;
            }
            /*  Si applica SEMPRE, anche senza file: altrimenti alla prima installazione
             *  Loc resterebbe su «segui N.I.N.A.» invece che sull'inglese di serie. */
            Loc.Instance.ForzaLingua(i._lingua);
            return i;
        }

        /// <summary>Scrive il file. Torna false e riempie <see cref="Nota"/> se non ci riesce.</summary>
        public bool Salva() {
            Nota = null;
            try {
                var cartella = Path.GetDirectoryName(_percorso);
                if (!string.IsNullOrEmpty(cartella)) { Directory.CreateDirectory(cartella!); }
                File.WriteAllText(_percorso, JsonSerializer.Serialize(
                    new Documento { Lingua = _lingua },
                    new JsonSerializerOptions { WriteIndented = true }));
                return true;
            } catch (Exception e) {
                Nota = "le impostazioni non si sono potute salvare: " + e.Message;
                return false;
            }
        }

        /*  Il documento su disco. Una chiave sola, e il nome per esteso: fra due anni
         *  chi lo apre deve capirlo senza avere il codice davanti. */
        private sealed class Documento {
            [JsonPropertyName("lingua")]
            public string? Lingua { get; set; }
        }
    }
}
