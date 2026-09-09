using System;
using System.IO;
using System.Text;
using System.Text.Json;
using AstroImage.NINA.Plugin.Localization;

#nullable enable

namespace AstroImage.NINA.Plugin.Views {

    /*  LA PAGINA DI PROVA, e si chiama cosi' perche' lo e'.
     *
     *  Serve a dimostrare che il giro si chiude: la pagina chiede, il ponte va a
     *  prendere, il servizio calcola col motore vero, e la risposta torna qui. Niente
     *  di piu'. Non e' l'interfaccia di Strategy e non ci somiglia: quella esiste gia',
     *  e' quella che l'utente conosce dal browser, e il pannello la ospitera' intera.
     *
     *  FINO A IERI STAVA DENTRO UNA STRINGA C#, e i suoi ventiduemila caratteri erano
     *  una costante di questo file. Funzionava, ma nessuno strumento la vedeva: niente
     *  controllo di sintassi sull'HTML, niente aiuto dell'editor sul JavaScript, niente
     *  modo di aprirla in un browser per guardarla mentre la si scrive. Adesso sono tre
     *  file veri — Pagina/prova.html, prova.css, prova.js — incorporati nella DLL come
     *  i due file delle lingue, e per la stessa ragione: il ponte si distribuisce
     *  copiando file, e una cartella dimenticata nella copia non darebbe un errore,
     *  darebbe un pannello bianco.
     *
     *  SI RICOMPONE IN UN DOCUMENTO SOLO perche' WebView2 riceve la pagina con
     *  NavigateToString e non ha nessuna cartella da cui risolvere gli indirizzi: un
     *  <link href="prova.css"> resterebbe non caricato, in silenzio. Quei due tag
     *  stanno lo stesso dentro prova.html, e sono il punto esatto in cui il foglio e lo
     *  script entrano — cosi' prova.html resta un documento vero, che si apre in un
     *  browser, invece di un file pieno di segnaposto.
     *
     *  QUI DENTRO NON C'E' UN INDIRIZZO, e non e' una dimenticanza. La pagina non sa
     *  dove sia il motore e non deve saperlo: manda un messaggio all'ospite. Il giorno
     *  in cui ci sara' un'identita' da spendere, il segreto restera' nel C#, dove chi
     *  apre gli strumenti di sviluppo non lo trova.
     */
    internal static class Pagina {

        private const string Radice = "AstroImage.NINA.Plugin.Views.Pagina.";
        private const string SegnaFoglio = "<link rel=\"stylesheet\" href=\"prova.css\">";
        private const string SegnaScript = "<script src=\"prova.js\"></script>";
        private const string SegnaVoci = "<!--voci-->";

        /// <summary>Il prefisso delle chiavi che appartengono alla pagina, e solo a lei.</summary>
        internal const string Prefisso = "Pag_";

        /*  Lo scheletro si compone una volta e si tiene: non dipende dalla lingua,
         *  perche' dentro non c'e' una parola — ci sono le chiavi. Lazy perche' e' il
         *  modo di farlo una volta sola anche se due pannelli nascono insieme. */
        private static readonly Lazy<string> _scheletro = new Lazy<string>(Componi);

        /*  LE PAROLE ENTRANO ALL'ULTIMO, e non insieme allo scheletro.
         *
         *  Se fossero incorporate nella parte tenuta in memoria, un pannello aperto dopo
         *  un cambio di lingua nascerebbe con le parole di prima. Costa una sostituzione
         *  su ventiduemila caratteri, cioe' niente, e toglie una categoria intera di
         *  guasti che si presentano solo nel secondo pannello. */
        internal static string Prova => _scheletro.Value.Replace(SegnaVoci, Voci());

        /*  SI SERIALIZZA CON LE REGOLE DI SERIE, e non con quelle «rilassate».
         *
         *  Le rilassate lasciano passare < e > e renderebbero la pagina piu' leggibile
         *  a chi la ispeziona. Ma queste voci finiscono DENTRO un tag <script>: basterebbe
         *  che una di esse contenesse la chiusura di quel tag per uscirne, e da li' in
         *  poi il resto sarebbe markup. Oggi nessuna la contiene — domani le scrive
         *  qualcun altro. La porta si chiude per costruzione, non per fiducia: il
         *  codificatore di serie scrive < come \\u003C e la questione non si pone.
         */
        private static string Voci() =>
            "<script>window.__LOC__=" +
            JsonSerializer.Serialize(Loc.Famiglia(Prefisso)) + ";</script>";

        private static string Componi() {
            var html = Risorsa("prova.html");
            var css = Risorsa("prova.css");
            var js = Risorsa("prova.js");

            /*  Se un segnaposto non c'e' piu', il foglio o lo script resterebbero fuori
             *  e il pannello si aprirebbe storto SENZA dire niente: e' il guasto
             *  peggiore, quello che si scopre sotto il cielo. Meglio fermarsi qui, dove
             *  il messaggio arriva a chi ha appena modificato il file. */
            if (html.IndexOf(SegnaFoglio, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("prova.html non ha piu' il collegamento a prova.css");
            if (html.IndexOf(SegnaScript, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("prova.html non ha piu' il collegamento a prova.js");
            if (html.IndexOf(SegnaVoci, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("prova.html non ha piu' il segnaposto " + SegnaVoci +
                    ": senza, la pagina si aprirebbe con i nomi delle chiavi al posto delle parole");

            /*  L'a capo finale dei file si toglie: c'e' perche' un file di testo finisce
             *  con un a capo, ma aggiungendone un altro qui verrebbe una riga vuota
             *  prima di ogni chiusura. Non cambierebbe niente a nessuno — e' proprio per
             *  questo che si sistema adesso, invece di lasciar divergere il documento da
             *  com'era prima senza una ragione. */
            return html
                .Replace(SegnaFoglio, "<style>\n" + css.TrimEnd('\r', '\n') + "\n</style>")
                .Replace(SegnaScript, "<script>\n" + js.TrimEnd('\r', '\n') + "\n</script>");
        }

        private static string Risorsa(string nome) {
            var asm = typeof(Pagina).Assembly;
            using (var flusso = asm.GetManifestResourceStream(Radice + nome)) {
                if (flusso is null) {
                    /*  Succede se qualcuno rinomina un file e dimentica il .csproj, o se
                     *  sposta la cartella. I nomi delle risorse presenti stanno nel
                     *  messaggio: chi lo legge capisce in tre secondi che cosa e'
                     *  successo, invece di cercare per mezz'ora. */
                    throw new InvalidOperationException(
                        "La risorsa «" + Radice + nome + "» non e' dentro la DLL. Ci sono: " +
                        string.Join(", ", asm.GetManifestResourceNames()));
                }
                using (var lettore = new StreamReader(flusso, Encoding.UTF8)) {
                    return lettore.ReadToEnd();
                }
            }
        }
    }
}
