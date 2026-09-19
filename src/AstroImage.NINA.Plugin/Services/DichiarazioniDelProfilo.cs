using System;
using System.Collections.Generic;
using AstroImage.NINA.Plugin.Models;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  QUELLO CHE HAI DICHIARATO PER IL PROFILO ATTIVO, E SOLO PER LUI.
     *
     *  La ruota e il sito dichiarati stanno dentro il profilo di N.I.N.A. — misurato: un
     *  valore scritto nel profilo A non si legge dal profilo B, e i file dei profili lo
     *  confermano — e il testo che l'utente legge dice «una volta per profilo». Fino al
     *  16 settembre 2026 pero' il pannello li leggeva UNA volta, quando nasceva, e li
     *  teneva: cambiando profilo a N.I.N.A. aperta restavano quelli di prima, partivano
     *  nelle richieste, e un salvataggio li avrebbe scritti nel profilo nuovo. Un banco
     *  ereditato in silenzio e' la peggiore delle tre possibilita': numeri plausibili di
     *  un altro telescopio.
     *
     *  Qui si rileggono tutti insieme, e non si eredita niente: o c'e' quello del profilo
     *  attivo, o non c'e'. Questa classe non nomina N.I.N.A., perche' si prova senza: la
     *  memoria per profilo sta dietro `IMemoriaRuota`, e chi si accorge del cambio — il
     *  pannello, sull'evento `ProfileChanged` — chiama `CambioDiProfilo.Applica`.
     */
    public sealed class DichiarazioniDelProfilo {

        /// <summary>La chiave della ruota dichiarata, dentro lo spazio del plugin nel profilo.</summary>
        public const string ChiaveRuota = "ruotaVirtuale";

        /// <summary>La chiave dei parametri del sito che N.I.N.A. non sa.</summary>
        public const string ChiaveSito = "sitoDichiarato";

        private readonly IMemoriaRuota memoria;

        public DichiarazioniDelProfilo(IMemoriaRuota memoria) {
            this.memoria = memoria ?? throw new ArgumentNullException(nameof(memoria));
            Ricarica();
        }

        /// <summary>Che cosa sono, fisicamente, i filtri in ruota nel profilo attivo.</summary>
        public RuotaVirtuale Ruota { get; private set; } = new RuotaVirtuale();

        /// <summary>Perche' la ruota non e' quella che ti aspettavi, se e' il caso.</summary>
        public string? NotaRuota { get; private set; }

        /// <summary>I parametri del sito dichiarati nel profilo attivo.</summary>
        public SitoDichiarato Sito { get; private set; } = new SitoDichiarato();

        /// <summary>Che cosa non andava nei parametri salvati, se qualcosa non andava.</summary>
        public string? NotaSito { get; private set; }

        /// <summary>La chiave del banco dichiarato: quello che del banco non sanno ne' N.I.N.A. ne' il catalogo.</summary>
        public const string ChiaveBanco = "bancoDichiarato";

        /// <summary>Il banco dichiarato nel profilo attivo, per chiave del contratto del banco.</summary>
        public BancoDichiarato Banco { get; private set; } = new BancoDichiarato();

        /// <summary>Che cosa non andava nel banco salvato, se qualcosa non andava.</summary>
        public string? NotaBanco { get; private set; }

        /// <summary>Rilegge tutto dal profilo attivo. Quello che il profilo non ha, qui non c'e'.</summary>
        public void Ricarica() {
            Ruota = DichiarazioneRuota.Leggi(memoria.Leggi(ChiaveRuota), out var notaR);
            NotaRuota = notaR;
            Sito = DichiarazioneSito.Leggi(memoria.Leggi(ChiaveSito), out var notaS);
            NotaSito = notaS;
            /*  Il banco si rilegge con gli altri, e per la stessa ragione: un banco ereditato da un altro profilo sono
             *  numeri plausibili di un altro telescopio. */
            Banco = DichiarazioneBanco.Leggi(memoria.Leggi(ChiaveBanco), out var notaB);
            NotaBanco = notaB;
            /*  E i progetti aperti (19 settembre 2026): un progetto di un altro profilo sono pose e guadagni di un altro
             *  treno. */
            Progetti = ProgettiDelProfilo.Leggi(memoria.Leggi(ProgettiDelProfilo.Chiave), out var notaP);
            NotaProgetti = notaP;
        }

        /// <summary>I progetti aperti nel profilo attivo: il profilo congelato di ogni bersaglio col suo banco.</summary>
        public List<ProgettiDelProfilo.Progetto> Progetti { get; private set; } = new List<ProgettiDelProfilo.Progetto>();

        /// <summary>Che cosa non andava nei progetti salvati, se qualcosa non andava.</summary>
        public string? NotaProgetti { get; private set; }

        /// <summary>Scrive i progetti aperti nel profilo attivo.</summary>
        public bool SalvaProgetti(out string? perCheNo) {
            NotaProgetti = null;
            return memoria.Scrivi(ProgettiDelProfilo.Chiave, ProgettiDelProfilo.Scrivi(Progetti), out perCheNo);
        }

        /// <summary>Sostituisce il banco dichiarato e lo scrive nel profilo attivo.</summary>
        public bool SalvaBanco(BancoDichiarato? nuovo, out string? perCheNo) {
            Banco = nuovo ?? new BancoDichiarato();
            NotaBanco = null;
            return memoria.Scrivi(ChiaveBanco, DichiarazioneBanco.Scrivi(Banco), out perCheNo);
        }

        /// <summary>Sostituisce la ruota dichiarata e la scrive nel profilo attivo.</summary>
        public bool SalvaRuota(RuotaVirtuale? nuova, out string? perCheNo) {
            Ruota = nuova ?? new RuotaVirtuale();
            NotaRuota = null;
            return memoria.Scrivi(ChiaveRuota, DichiarazioneRuota.Scrivi(Ruota), out perCheNo);
        }

        /// <summary>Sostituisce i parametri dichiarati del sito e li scrive nel profilo attivo.</summary>
        public bool SalvaSito(SitoDichiarato? nuovo, out string? perCheNo) {
            Sito = nuovo ?? new SitoDichiarato();
            NotaSito = null;
            return memoria.Scrivi(ChiaveSito, DichiarazioneSito.Scrivi(Sito), out perCheNo);
        }
    }

    /*  IL CAMBIO DI PROFILO, IN UN GESTO SOLO.
     *
     *  Le dichiarazioni si rileggono, e la prescrizione in mano SI RITIRA: non si
     *  rietichetta e non si ricalcola da sola. Era calcolata sul banco e sul sito del
     *  profilo di prima, e mostrata mentre e' attivo l'altro sarebbe un numero che mente
     *  senza essere rosso. Chi preme «manda» su una riga rimasta a schermo riceve
     *  `prescrizione_ritirata` e il perche'. */
    public static class CambioDiProfilo {
        public static void Applica(DichiarazioniDelProfilo dichiarazioni, PrescrizioneCorrente inMano) {
            if (dichiarazioni is null) throw new ArgumentNullException(nameof(dichiarazioni));
            if (inMano is null) throw new ArgumentNullException(nameof(inMano));
            dichiarazioni.Ricarica();
            inMano.Ritira();
        }
    }

    /*  IL RITIRO GENERALIZZATO (regia, 16 settembre 2026).
     *
     *  Il profilo non e' la sola cosa su cui la prescrizione e' calcolata: anche la ruota, il banco e il sito dichiarati
     *  entrano nella richiesta. Salvarne uno diverso lasciava in mano la prescrizione di prima, e «manda» la consegnava
     *  calcolata sulla dichiarazione vecchia — lo stesso numero che mente senza essere rosso del cambio di profilo. Adesso
     *  lo stesso gesto, col suo perche'.
     *
     *  Si ritira solo se la dichiarazione e' davvero cambiata, confrontando il documento che si scrive: riscrivere la
     *  stessa non tocca niente. E si ritira anche quando la scrittura nel profilo non riesce, perche' la dichiarazione in
     *  memoria e' gia' la nuova e la prossima richiesta la usa. Senza una prescrizione in mano non c'e' niente da ritirare,
     *  e chi preme «manda» deve sentire «chiedine una», non un ritiro che non e' avvenuto. */
    public static class SalvataggioDichiarato {

        public static bool Ruota(DichiarazioniDelProfilo d, PrescrizioneCorrente inMano, RuotaVirtuale? nuova,
                                 out string? perCheNo, out bool ritirata) {
            var prima = DichiarazioneRuota.Scrivi(d.Ruota);
            var ok = d.SalvaRuota(nuova, out perCheNo);
            ritirata = Ritira(inMano, prima, DichiarazioneRuota.Scrivi(d.Ruota), PercheRitirata.Ruota);
            return ok;
        }

        public static bool Banco(DichiarazioniDelProfilo d, PrescrizioneCorrente inMano, BancoDichiarato? nuovo,
                                 out string? perCheNo, out bool ritirata) {
            var prima = DichiarazioneBanco.Scrivi(d.Banco);
            var ok = d.SalvaBanco(nuovo, out perCheNo);
            ritirata = Ritira(inMano, prima, DichiarazioneBanco.Scrivi(d.Banco), PercheRitirata.Banco);
            return ok;
        }

        public static bool Sito(DichiarazioniDelProfilo d, PrescrizioneCorrente inMano, SitoDichiarato? nuovo,
                                out string? perCheNo, out bool ritirata) {
            var prima = DichiarazioneSito.Scrivi(d.Sito);
            var ok = d.SalvaSito(nuovo, out perCheNo);
            ritirata = Ritira(inMano, prima, DichiarazioneSito.Scrivi(d.Sito), PercheRitirata.Sito);
            return ok;
        }

        private static bool Ritira(PrescrizioneCorrente inMano, string prima, string dopo, PercheRitirata perche) {
            if (inMano is null) throw new ArgumentNullException(nameof(inMano));
            if (string.Equals(prima, dopo, StringComparison.Ordinal) || inMano.Id is null) return false;
            inMano.Ritira(perche);
            return true;
        }
    }
}
