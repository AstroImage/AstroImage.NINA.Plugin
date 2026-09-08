using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AstroImage.NINA.Plugin.Models.Setup;
using NINA.Core.Enum;
using NINA.Core.Model.Equipment;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
/*  Un alias, e non per vezzo: dentro AstroImage.NINA.Plugin il nome «NINA» risolve
 *  prima sul nostro namespace, quindi un nome pienamente qualificato scritto nel corpo
 *  non trova niente. Qui in testa al file la risoluzione e' globale e funziona. */
using RMSUnit = NINA.Equipment.Equipment.MyGuider.RMSUnit;

#nullable enable

namespace AstroImage.NINA.Plugin.Services {

    /*  IL LETTORE: da N.I.N.A. viva a SetupModel.
     *
     *  E' l'unico pezzo del ponte che tocca l'hardware, ed e' di proposito la parte
     *  piu' stupida che esista: chiede, copia, e dove non trova niente lascia nullo.
     *
     *  TRE REGOLE.
     *
     *  1. NIENTE SI INVENTA. Se un driver non espone una grandezza, quella grandezza
     *     resta `null`. Mai zero, mai falso, mai stringa vuota. `collegato: false` e'
     *     un'informazione — «c'e' ma e' spento»; `collegato: null` vuol dire che non si
     *     e' potuto nemmeno chiedere.
     *
     *  2. NIENTE SI DEDUCE. Dal nome di un filtro non escono nanometri, dal tipo di
     *     sensore non esce una classificazione piu' comoda. Si porta cio' che c'e'.
     *
     *  3. NIENTE SOLLEVA. Un lettore che si rompe perche' un driver risponde male
     *     lascia l'utente senza niente proprio nel momento in cui gli serviva sapere
     *     che qualcosa non va. Ogni blocco e' protetto da se': se la camera fa i
     *     capricci si perde la camera, non il resto della lettura.
     *
     *  Quello che il lettore NON sa fare, e non deve: confrontare, giudicare, decidere
     *  se il profilo ha ragione o il driver. Porta tutti e due i numeri e li fa vedere.
     */
    public sealed class LettoreSetup {

        private readonly IProfileService profilo;
        private readonly ICameraMediator camera;
        private readonly ITelescopeMediator montatura;
        private readonly IFilterWheelMediator ruota;
        private readonly IFocuserMediator focheggiatore;
        private readonly IRotatorMediator rotatore;
        private readonly IGuiderMediator guida;
        private readonly IWeatherDataMediator meteo;

        public LettoreSetup(IProfileService profilo,
                            ICameraMediator camera,
                            ITelescopeMediator montatura,
                            IFilterWheelMediator ruota,
                            IFocuserMediator focheggiatore,
                            IRotatorMediator rotatore,
                            IGuiderMediator guida,
                            IWeatherDataMediator meteo) {
            this.profilo = profilo;
            this.camera = camera;
            this.montatura = montatura;
            this.ruota = ruota;
            this.focheggiatore = focheggiatore;
            this.rotatore = rotatore;
            this.guida = guida;
            this.meteo = meteo;
        }

        /// <summary>
        /// Legge tutto quello che si puo' sapere adesso. Non solleva mai: cio' che non
        /// si e' potuto leggere resta nullo.
        /// </summary>
        /// <param name="versioneApplicazione">
        /// La versione di N.I.N.A., che il lettore non sa ricavare da se': gliela passa
        /// chi lo chiama, che sta dentro l'applicazione e la conosce.
        /// </param>
        public SetupModel Leggi(string? versioneApplicazione = null, string? versionePonte = null) {
            var m = new SetupModel {
                Versione = 1,
                Letto = DateTimeOffset.UtcNow,
                Applicazione = new Applicazione {
                    Nome = "N.I.N.A.",
                    Versione = versioneApplicazione,
                    Ponte = versionePonte,
                },
            };

            var p = Protetto(() => profilo?.ActiveProfile);
            m.Profilo = p is null ? null : new Profilo {
                Nome = Protetto(() => p.Name),
                Id = Protetto(() => p.Id.ToString()),
            };

            m.Sito = SitoDalProfilo(p);
            m.Ottica = OtticaDalProfilo(p);
            m.Camera = LeggiCamera(p);
            m.Montatura = LeggiMontatura();
            m.Ruota = LeggiRuota(p);
            m.Focheggiatore = LeggiFocheggiatore();
            m.Rotatore = LeggiRotatore();
            m.Guida = LeggiGuida();
            m.Meteo = LeggiMeteo();
            return m;
        }

        // ------------------------------------------------------------- il profilo

        private Sito? SitoDalProfilo(IProfile? p) {
            var a = Protetto(() => p?.AstrometrySettings);
            if (a is null) return null;
            return new Sito {
                Lat = Finito(Protetto(() => (double?)a.Latitude)),
                Lon = Finito(Protetto(() => (double?)a.Longitude)),
                ElevazioneM = Finito(Protetto(() => (double?)a.Elevation)),
            };
        }

        private Ottica? OtticaDalProfilo(IProfile? p) {
            var t = Protetto(() => p?.TelescopeSettings);
            if (t is null) return null;
            return new Ottica {
                /*  N.I.N.A. ha due nomi per il tubo: quello del driver e quello scritto
                 *  a mano. Si prende quello scritto a mano se c'e', perche' e' quello
                 *  che l'utente riconosce. */
                Nome = Testo(Protetto(() => t.Name)) ?? Testo(Protetto(() => t.MountName)),
                FocaleMm = Positivo(Protetto(() => (double?)t.FocalLength)),
                Rapporto = Positivo(Protetto(() => (double?)t.FocalRatio)),
            };
        }

        // ------------------------------------------------------------- la camera

        private Camera? LeggiCamera(IProfile? p) {
            var i = Protetto(() => camera?.GetInfo());
            var cfg = Protetto(() => p?.CameraSettings);
            if (i is null && cfg is null) return null;

            var c = new Camera {
                Collegato = Protetto(() => (bool?)i!.Connected),
                Nome = Testo(Protetto(() => i!.DisplayName)) ?? Testo(Protetto(() => i!.Name)),
                Driver = Testo(Protetto(() => i!.DriverInfo)),
                PixelUmProfilo = Positivo(Protetto(() => (double?)cfg!.PixelSize)),
                MatriceProfilo = Testo(Protetto(() => cfg!.BayerPattern.ToString())),
            };

            /*  Il guadagno e l'offset del profilo valgono anche a camera spenta: sono
             *  cio' che l'utente ha deciso di usare. */
            var gainProfilo = Protetto(() => (int?)cfg!.Gain);
            var offsetProfilo = Protetto(() => (int?)cfg!.Offset);

            if (i is null) {
                if (gainProfilo is not null) c.Gain = new Scala { Profilo = gainProfilo };
                if (offsetProfilo is not null) c.Offset = new Scala { Profilo = offsetProfilo };
                return c;
            }

            c.PixelUm = Positivo(Protetto(() => (double?)i.PixelSize));
            c.LarghezzaPx = Positivo(Protetto(() => (int?)i.XSize));
            c.AltezzaPx = Positivo(Protetto(() => (int?)i.YSize));
            c.Sensore = Testo(Protetto(() => i.SensorType.ToString()));
            /*  Solo un confronto con l'enumerazione, non una classificazione: tutto cio'
             *  che non e' monocromatico e' QUALCHE matrice, e quale conta — resta in
             *  `sensore` con il suo nome. */
            c.Monocromatico = Protetto(() => (bool?)(i.SensorType == SensorType.Monochrome));
            c.Bit = Positivo(Protetto(() => (int?)i.BitDepth));
            c.ElettroniPerAdu = Positivo(Protetto(() => (double?)i.ElectronsPerADU));
            c.PosaMinS = Positivo(Protetto(() => (double?)i.ExposureMin));
            c.PosaMaxS = Positivo(Protetto(() => (double?)i.ExposureMax));

            c.Gain = new Scala {
                Attuale = Protetto(() => (int?)i.Gain),
                Min = Protetto(() => (int?)i.GainMin),
                Max = Protetto(() => (int?)i.GainMax),
                Predefinito = Protetto(() => (int?)i.DefaultGain),
                Valori = Elenco(Protetto(() => i.Gains?.Cast<int>())),
                Profilo = gainProfilo,
            };
            c.Offset = new Scala {
                Attuale = Protetto(() => (int?)i.Offset),
                Min = Protetto(() => (int?)i.OffsetMin),
                Max = Protetto(() => (int?)i.OffsetMax),
                Predefinito = Protetto(() => (int?)i.DefaultOffset),
                Profilo = offsetProfilo,
            };

            c.ModiLettura = Elenco(Protetto(() => i.ReadoutModes));
            c.ModoLettura = Protetto(() => (int?)i.ReadoutMode);
            /*  I binning si portano come «1x1», «2x2»: e' la forma che si legge, e non
             *  costringe chi riceve a conoscere un tipo di N.I.N.A. */
            c.Binning = Elenco(Protetto(() => i.BinningModes?.Select(b => b.X + "x" + b.Y)));

            var freddo = new Raffreddamento {
                Disponibile = Protetto(() => (bool?)i.CanSetTemperature),
                TemperaturaC = Finito(Protetto(() => (double?)i.Temperature)),
                SetpointC = Finito(Protetto(() => (double?)i.TemperatureSetPoint)),
                Acceso = Protetto(() => (bool?)i.CoolerOn),
                PotenzaPct = Finito(Protetto(() => (double?)i.CoolerPower)),
                SetpointProfiloC = Finito(Protetto(() => (double?)cfg!.Temperature)),
                /*  `Finito` e non `Positivo`: ZERO MINUTI E' UNA SCELTA, vuol dire
                 *  «non aspettare». Scartarlo come se fosse un dato mancante
                 *  cancellerebbe una decisione dell'utente. Sul banco in campo questi
                 *  valgono 1 e 5, ma il giorno che uno mette 0 deve arrivare 0. */
                MinutiFreddo = Finito(Protetto(() => (double?)cfg!.CoolingDuration)),
                MinutiCaldo = Finito(Protetto(() => (double?)cfg!.WarmingDuration)),
            };
            c.Raffreddamento = freddo;
            return c;
        }

        // ----------------------------------------------------------- gli altri pezzi

        private Montatura? LeggiMontatura() {
            var i = Protetto(() => montatura?.GetInfo());
            if (i is null) return null;
            var m = new Montatura {
                Collegato = Protetto(() => (bool?)i.Connected),
                Nome = Testo(Protetto(() => i.DisplayName)) ?? Testo(Protetto(() => i.Name)),
                Driver = Testo(Protetto(() => i.DriverInfo)),
                Insegue = Protetto(() => (bool?)i.TrackingEnabled),
                PuoTornareACasa = Protetto(() => (bool?)i.CanFindHome),
                PuoParcheggiare = Protetto(() => (bool?)i.CanPark),
                InParcheggio = Protetto(() => (bool?)i.AtPark),
            };
            /*  Il sito della MONTATURA. Non e' quello del profilo, e quando i due non
             *  coincidono qualcuno sta per riprendere con effemeridi sbagliate. */
            var lat = Finito(Protetto(() => (double?)i.SiteLatitude));
            var lon = Finito(Protetto(() => (double?)i.SiteLongitude));
            var alt = Finito(Protetto(() => (double?)i.SiteElevation));
            if (lat is not null || lon is not null || alt is not null)
                m.Sito = new Sito { Lat = lat, Lon = lon, ElevazioneM = alt };
            return m;
        }

        private Ruota? LeggiRuota(IProfile? p) {
            var i = Protetto(() => ruota?.GetInfo());
            var cfg = Protetto(() => p?.FilterWheelSettings);
            if (i is null && cfg is null) return null;

            var r = new Ruota {
                Collegato = Protetto(() => (bool?)i!.Connected),
                Nome = Testo(Protetto(() => i!.DisplayName)) ?? Testo(Protetto(() => i!.Name)),
                Driver = Testo(Protetto(() => i!.DriverInfo)),
            };

            /*  L'ELENCO DEI FILTRI VIENE DAL PROFILO, non dalla ruota: il dispositivo sa
             *  solo quale slot e' montato adesso. Vuoto e nullo sono cose diverse —
             *  vuoto vuol dire ruota configurata senza filtri, nullo vuol dire che non
             *  si e' potuto leggere. Sui sette profili di questa macchina l'elenco e'
             *  vuoto in tutti e sette: e' un caso normale, non un guasto. */
            var filtri = Protetto(() => cfg?.FilterWheelFilters);
            if (filtri is not null)
                r.Filtri = filtri.Where(f => f is not null).Select(DaFiltro).ToList();

            var attuale = Protetto(() => i!.SelectedFilter);
            if (attuale is not null) r.FiltroAttuale = DaFiltro(attuale);
            return r;
        }

        private static Filtro DaFiltro(FilterInfo f) => new Filtro {
            Nome = Testo(Protetto(() => f.Name)),
            Posizione = Protetto(() => (int?)f.Position),
            OffsetFuoco = Protetto(() => (int?)f.FocusOffset),
            AutofocusPosaS = Positivo(Protetto(() => (double?)f.AutoFocusExposureTime)),
            AutofocusBinning = Testo(Protetto(() => f.AutoFocusBinning?.ToString())),
            AutofocusGain = Protetto(() => (int?)f.AutoFocusGain),
        };

        private Focheggiatore? LeggiFocheggiatore() {
            var i = Protetto(() => focheggiatore?.GetInfo());
            if (i is null) return null;
            return new Focheggiatore {
                Collegato = Protetto(() => (bool?)i.Connected),
                Nome = Testo(Protetto(() => i.DisplayName)) ?? Testo(Protetto(() => i.Name)),
                Driver = Testo(Protetto(() => i.DriverInfo)),
                Posizione = Protetto(() => (int?)i.Position),
                TemperaturaC = Finito(Protetto(() => (double?)i.Temperature)),
                PassoUm = Positivo(Protetto(() => (double?)i.StepSize)),
            };
        }

        private Rotatore? LeggiRotatore() {
            var i = Protetto(() => rotatore?.GetInfo());
            if (i is null) return null;
            return new Rotatore {
                Collegato = Protetto(() => (bool?)i.Connected),
                Nome = Testo(Protetto(() => i.DisplayName)) ?? Testo(Protetto(() => i.Name)),
                Driver = Testo(Protetto(() => i.DriverInfo)),
                PosizioneGradi = Finito(Protetto(() => (double?)i.Position)),
                MeccanicaGradi = Finito(Protetto(() => (double?)i.MechanicalPosition)),
                PuoInvertire = Protetto(() => (bool?)i.CanReverse),
            };
        }

        private Guida? LeggiGuida() {
            var i = Protetto(() => guida?.GetInfo());
            if (i is null) return null;
            var g = new Guida {
                Collegato = Protetto(() => (bool?)i.Connected),
                Nome = Testo(Protetto(() => i.DisplayName)) ?? Testo(Protetto(() => i.Name)),
                Driver = Testo(Protetto(() => i.DriverInfo)),
                ScalaArcsecPx = Positivo(Protetto(() => (double?)i.PixelScale)),
            };
            var r = Protetto(() => i.RMSError);
            if (r is not null) {
                /*  N.I.N.A. da' gia' tutte e due le unita': non c'e' niente da
                 *  convertire, e non convertire e' sempre meglio che convertire bene. */
                g.Rms = new Rms {
                    Ra = DaUnita(Protetto(() => r.RA)),
                    Dec = DaUnita(Protetto(() => r.Dec)),
                    Totale = DaUnita(Protetto(() => r.Total)),
                    PiccoRa = DaUnita(Protetto(() => r.PeakRA)),
                    PiccoDec = DaUnita(Protetto(() => r.PeakDec)),
                };
            }
            return g;
        }

        private Meteo? LeggiMeteo() {
            var i = Protetto(() => meteo?.GetInfo());
            if (i is null) return null;
            return new Meteo {
                Collegato = Protetto(() => (bool?)i.Connected),
                Nome = Testo(Protetto(() => i.DisplayName)) ?? Testo(Protetto(() => i.Name)),
                Driver = Testo(Protetto(() => i.DriverInfo)),
                Sqm = Finito(Protetto(() => (double?)i.SkyQuality)),
                FwhmArcsec = Positivo(Protetto(() => (double?)i.StarFWHM)),
                TemperaturaC = Finito(Protetto(() => (double?)i.Temperature)),
                UmiditaPct = Finito(Protetto(() => (double?)i.Humidity)),
                PuntoRugiadaC = Finito(Protetto(() => (double?)i.DewPoint)),
                NuvolePct = Finito(Protetto(() => (double?)i.CloudCover)),
                VentoMs = Finito(Protetto(() => (double?)i.WindSpeed)),
                PressioneHpa = Finito(Protetto(() => (double?)i.Pressure)),
            };
        }

        // ------------------------------------------------------------- gli attrezzi

        /*  I DRIVER SONO SOFTWARE DI TERZI, e non tutti si comportano bene. Un
         *  ASCOM che solleva su una proprieta' che dichiara di avere non e' un caso
         *  di scuola: e' martedi'. Ogni lettura passa di qui, e una che va male
         *  diventa un nullo invece di far cadere l'intera lettura. */
        private static T? Protetto<T>(Func<T?> leggi) {
            try { return leggi(); } catch { return default; }
        }

        /// <summary>Un `NaN` o un infinito non sono un dato: diventano nulli.</summary>
        private static double? Finito(double? x) =>
            x is null || double.IsNaN(x.Value) || double.IsInfinity(x.Value) ? null : x;

        /// <summary>
        /// Zero e i negativi non sono valori validi per una focale, una posa, un passo
        /// di pixel: molti driver li usano proprio per dire «non lo so». Diventano nulli
        /// perche' un pixel da zero micrometri manderebbe a zero un campionamento.
        /// </summary>
        private static double? Positivo(double? x) {
            var f = Finito(x);
            return f is null || f.Value <= 0 ? null : f;
        }

        private static int? Positivo(int? x) => x is null || x.Value <= 0 ? null : x;

        /// <summary>Una stringa vuota o di soli spazi non e' un nome: e' un nulla.</summary>
        private static string? Testo(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>
        /// Un RMS di N.I.N.A. porta il valore in pixel e in secondi d'arco insieme.
        /// Nullo per intero se non c'e' nessuna delle due.
        /// </summary>
        private static Misura? DaUnita(RMSUnit? u) {
            if (u is null) return null;
            var px = Finito(Protetto(() => (double?)u.Pixel));
            var arc = Finito(Protetto(() => (double?)u.Arcseconds));
            return px is null && arc is null ? null : new Misura { Px = px, Arcsec = arc };
        }

        /// <summary>Un elenco vuoto resta vuoto, ma un elenco che non c'e' resta nullo.</summary>
        private static List<T>? Elenco<T>(IEnumerable<T>? x) => x is null ? null : x.ToList();
    }
}
