using System;
using System.IO;
using System.Linq;
using AstroImage.NINA.Plugin.Services;
using global::NINA.Core.Enum;
using global::NINA.Core.Model;
using global::NINA.Core.Model.Equipment;
using global::NINA.Core.Utility;
using global::NINA.Profile;
using global::NINA.Profile.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests.Montaggio {

    /*  IL PROFILO PER LA PAGINA, LETTO DA OGGETTI VERI DI N.I.N.A. (21 ottobre 2026).
     *
     *  Le impostazioni sono le classi di N.I.N.A., non finte: i nomi delle proprieta' li controlla il compilatore, e
     *  l'orizzonte e' un CustomHorizon letto da un file come lo legge N.I.N.A. Finto e' solo il profilo che le tiene
     *  insieme, e chiedergli altro fa cadere la prova: si vede che cosa il lettore tocca.
     */
    [TestClass]
    public class ProfiloDaNinaTests {

        private const string Hrz = "# orizzonte di prova\n0 10\n90 20\n180 30\n270 20\n";

        private static IProfileService Servizio(IProfile? attivo) =>
            PersistenzaDelProfiloTests.Finto.Crea<IProfileService>((m, a) => m.Name switch {
                "get_ActiveProfile" => attivo,
                _ => throw new NotSupportedException("il lettore ha chiesto al servizio dei profili " + m.Name),
            });

        private static IProfile Profilo(AstrometrySettings a, BayerPatternEnum matrice = BayerPatternEnum.None) {
            var c = new CameraSettings { Id = "0F3C2A10-0000-4000-8000-00000000C0DE", PixelSize = 3.75, BayerPattern = matrice };
            var t = new TelescopeSettings { Name = "RC8", MountName = "AM5", FocalLength = 1295, FocalRatio = 6.5 };
            var f = new FramingAssistantSettings { CameraWidth = 6248, CameraHeight = 4176 };
            var w = new FilterWheelSettings {
                FilterWheelFilters = new ObserveAllCollection<FilterInfo> { new FilterInfo("L", 0, 0), new FilterInfo("Ha", 0, 5) },
            };
            return PersistenzaDelProfiloTests.Finto.Crea<IProfile>((m, x) => m.Name switch {
                "get_Name" => "Profilo di prova",
                "get_AstrometrySettings" => a,
                "get_CameraSettings" => c,
                "get_TelescopeSettings" => t,
                "get_FramingAssistantSettings" => f,
                "get_FilterWheelSettings" => w,
                _ => throw new NotSupportedException("il lettore ha chiesto al profilo " + m.Name),
            });
        }

        private static AstrometrySettings Sito(bool conOrizzonte = true) => new AstrometrySettings {
            Latitude = 45.95, Longitude = 10.2, Elevation = 1000,
            HorizonFilePath = @"D:\Orizzonti\Casa .hrz",
            Horizon = conOrizzonte ? CustomHorizon.FromReader_Standard(new StringReader(Hrz)) : null,
        };

        [TestMethod]
        public void IlBancoSiLeggeComE_CampoPerCampo() {
            var p = new ProfiloDaNina(Servizio(Profilo(Sito()))).Leggi();
            Assert.IsNotNull(p);
            Assert.AreEqual("Profilo di prova", p!.Nome);
            Assert.AreEqual(45.95, p.Latitudine);
            Assert.AreEqual(10.2, p.Longitudine);
            Assert.AreEqual(1000, p.Quota);
            Assert.AreEqual("0F3C2A10-0000-4000-8000-00000000C0DE", p.CameraId);
            Assert.AreEqual(3.75, p.PixelUm);
            Assert.AreEqual("None", p.Matrice, "la matrice deve uscire col nome di N.I.N.A., come la scrive l'Advanced API");
            Assert.AreEqual("RC8", p.Telescopio);
            Assert.AreEqual("AM5", p.Montatura);
            Assert.AreEqual(1295, p.FocaleMm);
            Assert.AreEqual(6.5, p.Rapporto);
            Assert.AreEqual(6248, p.LarghezzaPx);
            Assert.AreEqual(4176, p.AltezzaPx);
            CollectionAssert.AreEqual(new[] { "L", "Ha" }, p.Filtri.Select(x => x.Nome).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 5 }, p.Filtri.Select(x => x.Posizione).ToArray());
            Assert.AreEqual("RGGB", new ProfiloDaNina(Servizio(Profilo(Sito(), BayerPatternEnum.RGGB))).Leggi()!.Matrice);
        }

        [TestMethod]
        public void DelFileDellOrizzonte_IlNome_ENonIlPercorso() {
            var p = new ProfiloDaNina(Servizio(Profilo(Sito()))).Leggi()!;
            Assert.AreEqual("Casa .hrz", p.FileOrizzonte);
        }

        [TestMethod]
        public void LOrizzonte_DaZeroA360_ComeLAdvancedApi() {
            var o = new ProfiloDaNina(Servizio(Profilo(Sito()))).Leggi()!.Orizzonte;
            Assert.IsNotNull(o);
            Assert.AreEqual(361, o!.Azimut.Length);
            Assert.AreEqual(361, o.Altezza.Length);
            Assert.AreEqual(0, o.Azimut[0]);
            Assert.AreEqual(360, o.Azimut[360]);
            Assert.AreEqual(10, o.Altezza[0], 1e-9);
            Assert.AreEqual(20, o.Altezza[90], 1e-9);
            Assert.AreEqual(30, o.Altezza[180], 1e-9);
            Assert.AreEqual(o.Altezza[0], o.Altezza[360], 1e-9, "360 gradi sono 0: N.I.N.A. li riporta allo stesso punto");
        }

        [TestMethod]
        public void SenzaOrizzonte_Null_ESenzaProfilo_Null() {
            Assert.IsNull(new ProfiloDaNina(Servizio(Profilo(Sito(false)))).Leggi()!.Orizzonte);
            Assert.IsNull(new ProfiloDaNina(Servizio(null)).Leggi());
            Assert.IsNull(new ProfiloDaNina(null).Leggi());
        }
    }
}
