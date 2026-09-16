using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AstroImage.NINA.Plugin.Models;
using AstroImage.NINA.Plugin.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#nullable enable

namespace AstroImage.NINA.Plugin.Tests {

    /*  RILEGGERE SENZA EREDITARE, E RITIRARE SENZA RIETICHETTARE.
     *
     *  Il gesto del cambio di profilo, senza N.I.N.A.: una memoria finta con due profili
     *  dietro, e il profilo attivo che si sposta. Che il pannello vero lo chiami sull'evento
     *  lo prova CambioDiProfiloTests, nel banco di Montaggio.
     */
    [TestClass]
    public class DichiarazioniDelProfiloTests {

        private sealed class MemoriaDiDueProfili : IMemoriaRuota {
            public readonly Dictionary<string, Dictionary<string, string>> Profili =
                new() { ["A"] = new(), ["B"] = new() };
            public string Attivo = "A";
            public string? Leggi(string chiave) => Profili[Attivo].TryGetValue(chiave, out var v) ? v : null;
            public bool Scrivi(string chiave, string documento, out string? perCheNo) {
                perCheNo = null; Profili[Attivo][chiave] = documento; return true;
            }
        }

        private static EsitoPrescrizione EsitoConUnaNotte() {
            var seq = new List<SequenzaDiNotte> { new SequenzaDiNotte { Notte = 1, Modello = new SequenceModel() } };
            return typeof(EsitoPrescrizione)
                .GetMethod("Riuscita", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, new object?[] { 200, "{}", seq, 0, "1", 1.0 }) as EsitoPrescrizione
                ?? throw new InvalidOperationException();
        }

        [TestMethod]
        public void CambiandoProfilo_RuotaESitoSonoQuelliDelProfiloNuovo_ONiente() {
            var m = new MemoriaDiDueProfili();
            var d = new DichiarazioniDelProfilo(m);
            Assert.IsTrue(d.SalvaRuota(new RuotaVirtuale { Vetri = { new VoceRuota { Nina = "ULTIMATE", Motore = "lult" } } }, out _));
            Assert.IsTrue(d.SalvaSito(new SitoDichiarato { Sqm = 20.8 }, out _));
            var p = new PrescrizioneCorrente();
            var id = p.Prendi(EsitoConUnaNotte());

            m.Attivo = "B";
            CambioDiProfilo.Applica(d, p);

            Assert.AreEqual(0, DichiarazioneRuota.IdDichiarati(d.Ruota).Count, "la ruota di A non si eredita");
            Assert.IsNull(d.Sito.Sqm, "il sito di A non si eredita");
            Assert.IsNull(p.Notte(id, 1, out var codice, out _));
            Assert.AreEqual("prescrizione_ritirata", codice);

            m.Attivo = "A";
            CambioDiProfilo.Applica(d, p);
            CollectionAssert.AreEqual(new[] { "lult" }, DichiarazioneRuota.IdDichiarati(d.Ruota).ToArray());
            Assert.AreEqual(20.8, d.Sito.Sqm);
        }

        /*  IL BANCO DICHIARATO NON SI EREDITA: un banco di un altro profilo sono numeri plausibili di un altro telescopio. */
        [TestMethod]
        public void CambiandoProfilo_IlBancoDichiaratoEQuelloDelProfiloNuovo_ONiente() {
            var m = new MemoriaDiDueProfili();
            var d = new DichiarazioniDelProfilo(m);
            var banco = DichiarazioneBanco.DalMessaggio(
                System.Text.Json.Nodes.JsonNode.Parse("{\"banco\":{\"tel.id\":\"askar71f\",\"tel.apertura_mm\":71}}"), out var perCheNo);
            Assert.IsNotNull(banco, perCheNo);
            Assert.IsTrue(d.SalvaBanco(banco, out _));
            var p = new PrescrizioneCorrente();

            m.Attivo = "B";
            CambioDiProfilo.Applica(d, p);
            Assert.AreEqual(0, d.Banco.Valori.Count, "il banco di A non si eredita in B");

            m.Attivo = "A";
            CambioDiProfilo.Applica(d, p);
            Assert.AreEqual("askar71f", d.Banco.Valori["tel.id"].GetString(), "tornati ad A, si rilegge il banco di A");
            Assert.AreEqual(71, d.Banco.Valori["tel.apertura_mm"].GetDouble());
        }

        /*  Ritirata e lasciata non sono la stessa cosa, e chi preme «manda» deve sentire la differenza. */
        [TestMethod]
        public void UnaPrescrizioneRitirata_DiceCheERitirata_EUnaNuovaLaRimette() {
            var p = new PrescrizioneCorrente();
            var id = p.Prendi(EsitoConUnaNotte());
            p.Ritira();
            Assert.IsNull(p.Notte(id, 1, out var codice, out _));
            Assert.AreEqual("prescrizione_ritirata", codice);

            var nuovo = p.Prendi(EsitoConUnaNotte());
            Assert.IsNotNull(p.Notte(nuovo, 1, out var c2, out _), "una prescrizione nuova si consegna");
            Assert.IsNull(c2);

            p.Lascia();
            Assert.IsNull(p.Notte(nuovo, 1, out var c3, out _));
            Assert.AreEqual("nessuna_prescrizione", c3, "lasciata non e' ritirata");
        }
    }
}
