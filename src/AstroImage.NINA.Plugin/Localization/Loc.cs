using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Resources;

#nullable enable

namespace AstroImage.NINA.Plugin.Localization {

    /*  LE PAROLE CHE L'UTENTE LEGGE, IN DUE LINGUE.
     *
     *  Il registro dei plugin di N.I.N.A. non impone nessuna lingua — l'ho verificato
     *  sul README dei manifest, non c'e' una riga in proposito. Ma il catalogo lo apre
     *  gente in mezzo mondo, e un plugin che parla solo italiano non e' irregolare:
     *  e' inservibile per quasi tutti quelli che lo scaricano. Quindi l'inglese e' la
     *  lingua di serie, e l'italiano c'e' per chi tiene N.I.N.A. in italiano.
     *
     *  DUE FILE DI RISORSE DENTRO LA STESSA DLL, e non e' pigrizia.
     *
     *  La via normale di .NET sarebbe `Strings.it.resx`, che il compilatore trasforma
     *  in un assembly satellite dentro una cartella `it\`. Il ponte pero' si distribuisce
     *  copiando dei file in una cartella di N.I.N.A.: una sottocartella dimenticata
     *  nella copia non da' nessun errore, da' un plugin che parla inglese a chi ha
     *  scelto l'italiano e nessuno capisce perche'. Con l'underscore — `Strings_it` —
     *  il meccanismo dei satelliti non si accende e le due lingue viaggiano dentro la
     *  DLL, che o c'e' o non c'e'.
     *
     *  QUI DENTRO NON SI NOMINA N.I.N.A., e non e' un vezzo: i test di questo progetto
     *  girano SENZA una sola DLL di N.I.N.A. accanto, e un tipo che eredita da una sua
     *  classe base non si carica affatto (vedi TipiDelPonte). L'altro plugin di casa,
     *  AdaptiveAgentForPHD2, eredita da `BaseINPC` di N.I.N.A. per avvisare la vista:
     *  qui si usa `INotifyPropertyChanged`, che sta nella libreria di base, fa la stessa
     *  identica cosa per WPF e lascia questa classe collaudabile. E' l'unico punto in
     *  cui mi sono scostato dal modello gia' collaudato, e il motivo e' questo.
     *
     *  IL RIPIEGO E' OBBLIGATORIO E VA IN UN VERSO SOLO: se una chiave manca
     *  nell'italiano si prende l'inglese; se manca anche li' si restituisce la chiave.
     *  Mai una stringa vuota: un'etichetta sparita e' un difetto che si nota sotto il
     *  cielo, il nome di una chiave in mezzo alla pagina si nota subito.
     */
    public sealed class Loc : INotifyPropertyChanged {

        public static Loc Instance { get; } = new Loc();

        private static readonly ResourceManager En =
            new ResourceManager("AstroImage.NINA.Plugin.Localization.Strings_en", typeof(Loc).Assembly);
        private static readonly ResourceManager It =
            new ResourceManager("AstroImage.NINA.Plugin.Localization.Strings_it", typeof(Loc).Assembly);

        /*  "" segue N.I.N.A., "en" e "it" la impongono. Segue di serie: N.I.N.A. porta
         *  la propria lingua nella cultura del thread, quindi non serve un'altra
         *  impostazione da salvare, da mostrare e da tenere allineata a quella. Il
         *  giorno in cui servisse un selettore nelle Opzioni, e' una chiamata a
         *  ForzaLingua e niente altro. */
        private string _lingua = "";

        private Loc() { }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Impone la lingua: "" segue N.I.N.A., "it" o "en" la fissano. La vista si
        /// aggiorna subito, senza riavviare: il legame di WPF ascolta "Item[]".
        /// </summary>
        public void ForzaLingua(string? lingua) {
            _lingua = (lingua ?? "").Trim().ToLowerInvariant();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }

        /// <summary>Quale lingua e' in uso adesso: "it" oppure "en".</summary>
        public string LinguaInUso => Italiano ? "it" : "en";

        private bool Italiano =>
            _lingua == "it"
            || (_lingua.Length == 0
                && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it");

        public string this[string chiave] {
            get {
                if (string.IsNullOrEmpty(chiave)) { return ""; }
                var prima = Italiano ? It : En;
                return prima.GetString(chiave) ?? En.GetString(chiave) ?? chiave;
            }
        }

        /// <summary>Una frase intera: <c>Loc.T("Sito_SenzaPosizione")</c>.</summary>
        public static string T(string chiave) => Instance[chiave];

        /*  TUTTE LE VOCI DI UNA FAMIGLIA, PER CHI NON PUO' CHIEDERLE UNA PER UNA.
         *
         *  La pagina del pannello vive dentro WebView2 e non ha modo di chiamare questo
         *  codice: parla per messaggi. Chiederle una alla volta vorrebbe dire sessanta
         *  andate e ritorni prima di poter disegnare qualcosa, e nel frattempo il
         *  pannello resterebbe con le etichette vuote.
         *
         *  Quindi le riceve tutte insieme. Il prefisso non e' un dettaglio di comodo:
         *  alla pagina vanno le SUE parole e nient'altro — i messaggi del montaggio, le
         *  ragioni dei rifiuti, i testi delle Opzioni restano nel C#, dove nascono. Chi
         *  apre gli strumenti di sviluppo sulla pagina non deve trovarsi in mano tutto
         *  il vocabolario del plugin.
         */
        public static IReadOnlyDictionary<string, string> Famiglia(string prefisso) {
            var fuori = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(prefisso)) { return fuori; }

            /*  Si scorre l'INGLESE, non la lingua in uso: e' quello che ha sempre tutte
             *  le chiavi, ed e' il motivo per cui e' anche il ripiego. Il valore invece
             *  passa dall'indicizzatore, che sceglie la lingua e ripiega da solo. */
            var set = En.GetResourceSet(CultureInfo.InvariantCulture, true, true);
            if (set is null) { return fuori; }
            foreach (System.Collections.DictionaryEntry v in set) {
                var chiave = v.Key as string;
                if (chiave != null && chiave.StartsWith(prefisso, StringComparison.Ordinal))
                    fuori[chiave] = Instance[chiave];
            }
            return fuori;
        }

        /// <summary>
        /// Una frase con dei valori dentro: <c>Loc.F("Blocco_PoseDaSecondi", pose, sec)</c>.
        /// I segnaposto sono quelli di <c>string.Format</c>, e la cultura resta quella
        /// corrente di proposito: «4,83 h» con la virgola per chi ha N.I.N.A. in
        /// italiano, «4.83 h» per gli altri. I numeri seguono chi legge, non il file.
        /// </summary>
        public static string F(string chiave, params object?[] valori) {
            var modello = Instance[chiave];
            if (valori is null || valori.Length == 0) { return modello; }
            try { return string.Format(CultureInfo.CurrentCulture, modello, valori); }
            catch (FormatException) {
                /*  Un segnaposto sbagliato in un file di risorse non deve far cadere una
                 *  consegna: si restituisce il modello cosi' com'e'. C'e' un test che
                 *  verifica che i segnaposto delle due lingue coincidano, quindi qui si
                 *  arriva solo se qualcuno ha modificato un resx a mano. */
                return modello;
            }
        }
    }
}
