using System;
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
}
