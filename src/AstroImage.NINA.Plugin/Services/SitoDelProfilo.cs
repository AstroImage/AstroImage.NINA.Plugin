using System;
using System.Collections.Generic;
using AstroImage.NINA.Plugin.Models;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using AstroImage.NINA.Plugin.Localization;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  DOVE SEI, LETTO DA N.I.N.A.
     *
     *  Il profilo ha SEMPRE latitudine e longitudine: nessuno arriva a riprendere senza
     *  averle inserite, e N.I.N.A. le chiede alla prima configurazione. Sono l'unica
     *  parte del sito che non va dichiarata.
     *
     *  Il resto — qualita' del cielo, seeing, errore di guida — arriva solo se un
     *  dispositivo lo misura: una stazione meteo con misuratore SQM, o il guider
     *  collegato. Quando non c'e', qui torna nullo e lo dichiara chi riprende: non si
     *  inventa e non si stima.
     */
    public sealed class SitoDelProfilo {

        private readonly IProfileService? profilo;
        private readonly IWeatherDataMediator? meteo;
        private readonly IGuiderMediator? guida;

        public SitoDelProfilo(IProfileService? profilo,
                              IWeatherDataMediator? meteo,
                              IGuiderMediator? guida) {
            this.profilo = profilo;
            this.meteo = meteo;
            this.guida = guida;
        }

        /// <summary>
        /// Quello che N.I.N.A. sa adesso. I campi che nessun dispositivo fornisce
        /// restano nulli: <see cref="DichiarazioneSito.Unisci"/> li completa con la
        /// dichiarazione, e se manca anche quella restano nulli fino in fondo.
        /// </summary>
        public SitoDiRipresa Leggi(out string? perCheNo) {
            perCheNo = null;
            var s = new SitoDiRipresa();

            var a = Protetto(() => profilo?.ActiveProfile?.AstrometrySettings);
            if (a is null) {
                perCheNo = Loc.T("Sito_ProfiloSenzaPosizione");
                return s;
            }

            var lat = Protetto(() => (double?)a.Latitude);
            var lon = Protetto(() => (double?)a.Longitude);
            s.Nome = Testo(Protetto(() => a.Site));

            /*  ZERO E' UNA COORDINATA VALIDA, ED E' IL GOLFO DI GUINEA.
             *
             *  In N.I.N.A. latitudine e longitudine sono `double`, non annullabili: un
             *  profilo appena creato vale 0, 0. Non e' un valore mancante che si nota —
             *  e' una posizione perfettamente plausibile in mezzo all'Atlantico, e il
             *  motore ci calcolerebbe sopra una notte intera senza un errore.
             *
             *  Si rifiuta solo lo zero esatto su ENTRAMBE, che e' l'unico caso in cui
             *  «non impostato» e' certo: chi riprende davvero sull'equatore ha almeno
             *  un decimale, e chi non l'ha ha comunque una longitudine. */
            if (lat is null || lon is null ||
                (Math.Abs(lat.Value) < 1e-9 && Math.Abs(lon.Value) < 1e-9)) {
                perCheNo = Loc.T("Sito_ZeroZero");
                return s;
            }
            s.Lat = lat;
            s.Lon = lon;

            /*  L'ORIZZONTE PER AZIMUT, e finalmente per intero.
             *
             *  Questo commento, in DichiarazioneSito, diceva la cosa giusta e non la
             *  poteva fare: «N.I.N.A. ha un orizzonte per AZIMUT, molto piu' ricco del
             *  numero solo che il motore accetta [...] il massimo nasconde meta' cielo,
             *  il minimo fa riprendere dentro la casa». Adesso il motore accetta un
             *  profilo, e ridurlo sarebbe una scelta invece che un obbligo.
             *
             *  SI CAMPIONA A UN GRADO. `CustomHorizon.GetAltitude` interpola fra i punti
             *  del file e normalizza da se' gli azimut fuori scala, quindi trecentosessanta
             *  domande restituiscono la curva com'e': i file .hrz sono scritti a gradi
             *  interi — quello di Borno ha esattamente 360 righe — e sotto il grado non
             *  c'e' montagna che cambi. Il motore interpola allo stesso modo, cosi' la
             *  curva che disegna N.I.N.A. e quella che usa Strategy sono la stessa.
             *
             *  Sono circa quattro kilobyte di JSON: la richiesta ne ammette mille.
             *
             *  NULL E' UN CASO NORMALE, non un errore. L'utente puo' non averlo mai
             *  configurato; oppure il file era illeggibile al caricamento del profilo, e
             *  allora N.I.N.A. azzera anche il percorso — i due casi collassano e nessuno
             *  dei due autorizza a inventare un orizzonte piatto. Si lascia nullo, come
             *  si fa con l'SQM, e chi non ce l'ha dichiara un numero. */
            var oriz = Protetto(() => a.Horizon);
            s.OrizzonteFile = Testo(Protetto(() => a.HorizonFilePath));
            if (oriz is not null) {
                var punti = new List<double[]>(360);
                var buoni = 0;
                for (var az = 0; az < 360; az++) {
                    var h = Protetto(() => (double?)oriz.GetAltitude(az));
                    if (h is null || !double.IsFinite(h.Value)) continue;
                    punti.Add(new[] { (double)az, Math.Round(h.Value, 2) });
                    buoni++;
                }
                /*  Due punti sono il minimo per interpolare. Sotto, quello che si e'
                 *  letto non e' un orizzonte e si scarta invece di mandarlo a meta'. */
                if (buoni >= 2) s.Orizzonte = punti.ToArray();
            }

            /*  L'SQM E' UNA MISURA. Arriva solo da un misuratore collegato — un SQM-LE,
             *  una stazione che lo espone. Se non c'e', resta nullo e lo dichiari tu:
             *  dedurlo dalle coordinate sarebbe indovinare il cielo dalla mappa, e fra
             *  il fondovalle e l'altopiano, a venti chilometri, ci sono due magnitudini. */
            var m = Protetto(() => meteo?.GetInfo());
            if (m is not null && Protetto(() => (bool?)m.Connected) == true) {
                s.Sqm = Finito(Protetto(() => (double?)m.SkyQuality));
                /*  StarFWHM e' il seeing misurato sulle stelle, quando la stazione lo
                 *  espone. Non tutte lo fanno, e chi non lo fa restituisce NaN. */
                s.Seeing = Finito(Protetto(() => (double?)m.StarFWHM));
            }

            /*  L'errore di guida in RMS totale, in arcosecondi. Il guider lo espone in
             *  pixel: si converte con la scala che il guider stesso dichiara. Senza
             *  scala non si converte, e si lascia nullo invece di stimarla. */
            var g = Protetto(() => guida?.GetInfo());
            if (g is not null && Protetto(() => (bool?)g.Connected) == true) {
                var scala = Finito(Protetto(() => (double?)g.PixelScale));
                var rmsPx = Finito(Protetto(() => (double?)g.RMSError.Total.Pixel));
                if (scala is > 0 && rmsPx is >= 0) s.Rms = rmsPx * scala;
            }

            return s;
        }

        private static T? Protetto<T>(Func<T?> f) { try { return f(); } catch (Exception) { return default; } }
        private static double? Finito(double? d) => d is not null && double.IsFinite(d.Value) && d.Value > 0 ? d : null;
        private static string? Testo(string? s) => string.IsNullOrWhiteSpace(s) ? null : s!.Trim();
    }
}
