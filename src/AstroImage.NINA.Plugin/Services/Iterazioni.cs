using System;
using System.Globalization;
using System.Reflection;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  QUANTE POSE, e perche' scriverlo richiede un pezzo tutto suo.
     *
     *  IL DIFETTO. Sul banco vero, N.I.N.A. 3.3 mostrava «# 20 · Progresso 0/151»: il
     *  numero chiesto era 151, quello scritto nella casella era 20 — il valore del
     *  modello clonato. Non un dettaglio grafico: da quale dei due comanda dipende se
     *  la notte sono cinque ore o quaranta minuti.
     *
     *  PERCHE' SUCCEDE. Le due versioni hanno forme diverse, verificate leggendo i
     *  metadati delle assembly e non a memoria:
     *
     *      3.2   SmartExposure : Attempts, ErrorBehavior
     *            `Iterations` esiste solo sulla base SequenceContainer, ed e' il
     *            contatore del CONTENITORE. Il numero di pose vive nel LoopCondition.
     *
     *      3.3   SmartExposure : Attempts, ErrorBehavior,
     *                            Iterations:Int32,
     *                            IterationsDefinition:String,      ← la verita'
     *                            IterationsExpression:Expression
     *            piu' il metodo BackfillIterationsExpressionFromLoopCondition.
     *
     *  Quel «Backfill» spiega tutto: la 3.3, quando clona un elemento vecchio, copia il
     *  numero DAL LoopCondition dentro la propria definizione. Il nostro ordine era
     *
     *      1. clono dal modello            LoopCondition = 20
     *      2. la 3.3 fa il backfill        IterationsDefinition = "20"   ← si fissa qui
     *      3. scrivo LoopCondition = 151   il backfill e' gia' passato
     *
     *  e la casella continuava a dire 20 perche' diceva la verita': quella era la
     *  definizione. Il plugin e' compilato sulla 3.2, dove `SmartExposure.Iterations`
     *  non esiste, quindi `se.Iterations` si legava alla proprieta' della BASE — un
     *  oggetto diverso da quello che la 3.3 legge.
     *
     *  LA RIFLESSIONE QUI E' LA STRADA GIUSTA, e altrove in questo progetto e' stata
     *  esclusa. La differenza e' che li' si cercava un servizio che N.I.N.A. non
     *  fornisce — una scommessa su un'architettura — mentre qui si scrive una
     *  proprieta' che ESISTE, di tipo noto, su una versione nota, e la si RILEGGE per
     *  verificare. Compilare sulla 3.3 non e' un'alternativa: il plugin deve girare
     *  anche sulla 3.2, ed e' la ragione per cui e' costruito sulla piu' bassa.
     *
     *  E SOPRATTUTTO: NON BASTA SCRIVERE, BISOGNA VERIFICARE. Questo modulo rilegge
     *  quello che ha scritto e dice se ha attecchito. Chi lo chiama, se non ha
     *  attecchito, NON consegna — perche' una sequenza che dice 20 dove la prescrizione
     *  diceva 151 e' esattamente il difetto che questo progetto rifiuta: qualcosa che
     *  somiglia alla prescrizione e non lo e'.
     */
    public static class Iterazioni {

        /// <summary>Le proprieta' della 3.3, per nome. Assenti sulla 3.2, e la loro
        /// assenza non e' un errore.</summary>
        public const string Definizione = "IterationsDefinition";
        public const string Numero = "Iterations";

        /// <summary>
        /// Scrive il numero di pose ovunque quella versione di N.I.N.A. lo tenga, poi
        /// rilegge. Torna false con un motivo se qualcosa non ha attecchito.
        /// </summary>
        /// <param name="posa">Lo SmartExposure. Su 3.3 ha una propria definizione.</param>
        /// <param name="condizione">Il LoopCondition sotto di lui. C'e' in entrambe.</param>
        public static bool Imposta(object? posa, object? condizione, int pose, out string? perCheNo) {
            perCheNo = null;
            if (pose <= 0) { perCheNo = "un numero di pose non positivo non si scrive."; return false; }
            if (posa is null && condizione is null) { perCheNo = "non c'e' niente su cui scrivere."; return false; }

            var testo = pose.ToString(CultureInfo.InvariantCulture);

            /*  Si scrive su TUTTI i posti che esistono, e l'ordine conta: prima
             *  l'intero, poi la definizione — perche' sulla 3.3 e' la definizione a
             *  ricalcolare l'intero, e scrivendola per ultima vince lei. */
            foreach (var o in new[] { condizione, posa }) {
                if (o is null) continue;
                Scrivi(o, Numero, pose);
                Scrivi(o, Definizione, testo);
            }

            /*  LA RILETTURA. Un valore scritto e non ricontrollato e' una speranza. */
            var effettivo = Effettivo(posa, condizione);
            if (effettivo is null) {
                perCheNo = "non si e' potuto rileggere il numero di pose dopo averlo scritto.";
                return false;
            }
            if (effettivo != pose) {
                perCheNo = $"il numero di pose non ha attecchito: chieste {pose}, l'oggetto " +
                           $"dice {effettivo}. Su N.I.N.A. 3.3 il valore che comanda e' " +
                           $"`{Definizione}` dello SmartExposure, e questa versione del ponte " +
                           "non e' riuscita a scriverlo.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Quante pose l'oggetto dice ADESSO. Si guarda prima la definizione dello
        /// SmartExposure — che sulla 3.3 e' quella che comanda e che l'utente legge nella
        /// casella «#» — e solo se non c'e' si ripiega sulla condizione, che e' l'unico
        /// posto dove il numero vive sulla 3.2.
        /// </summary>
        public static int? Effettivo(object? posa, object? condizione) {
            if (posa is not null) {
                var d = Leggi(posa, Definizione);
                if (d is string s && int.TryParse(s, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var n)) return n;
            }
            if (condizione is not null && Leggi(condizione, Numero) is int c) return c;
            if (posa is not null && Leggi(posa, Numero) is int p) return p;
            return null;
        }

        /*  Scrivere una proprieta' che potrebbe non esserci non deve mai sollevare: su
         *  3.2 meta' di queste non esistono, ed e' normale. */
        private static void Scrivi(object o, string nome, object valore) {
            try {
                var p = o.GetType().GetProperty(nome, BindingFlags.Public | BindingFlags.Instance);
                if (p is null || !p.CanWrite) return;
                if (!p.PropertyType.IsInstanceOfType(valore)) return;
                p.SetValue(o, valore);
            } catch (Exception) { /* una proprieta' che rifiuta non e' un guasto nostro */ }
        }

        private static object? Leggi(object o, string nome) {
            try {
                return o.GetType().GetProperty(nome, BindingFlags.Public | BindingFlags.Instance)
                        ?.GetValue(o);
            } catch (Exception) { return null; }
        }
    }
}
