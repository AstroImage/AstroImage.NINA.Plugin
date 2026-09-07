using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace AstroImage.NINA.Plugin.Models.Json {

    /// <summary>
    /// Scrive un istante ESATTAMENTE come lo scrive il motore, e lo rilegge com'e'.
    ///
    /// <para>
    /// Gli istanti del contratto nascono da <c>Date.toISOString()</c> di JavaScript, che
    /// ha una forma sola e sempre quella: <c>2026-09-01T19:45:00.000Z</c> — UTC, la Z
    /// finale, tre decimali anche quando sono zeri. Il serializzatore di .NET, lasciato
    /// a se', riscriverebbe lo stesso istante come <c>2026-09-01T19:45:00+00:00</c>:
    /// stesso momento, altro testo.
    /// </para>
    ///
    /// <para>
    /// Non sarebbe una perdita di dati, ma costerebbe una cosa che vale: oggi le fixture
    /// del motore, passate per questo modello, tornano indietro IDENTICHE carattere per
    /// carattere, e c'e' un test che lo verifica. E' la garanzia piu' forte che il ponte
    /// non stia riscrivendo di nascosto cio' che gli e' stato dato. Un convertitore di
    /// dodici righe la mantiene.
    /// </para>
    ///
    /// <para>
    /// IN LETTURA SI ACCETTA QUALUNQUE ISO 8601, MA UN ISTANTE SENZA FUSO E' UN ISTANTE
    /// IN UTC. Non e' un dettaglio: <c>2026-09-01T19:45:00</c> senza la Z e' ISO 8601
    /// legittimo, e il lettore di serie lo attaccherebbe al fuso della MACCHINA. Lo
    /// stesso file darebbe le 19:45 a Roma, le 19:45 a Los Angeles e due istanti diversi
    /// di sette ore; poi la riscrittura fisserebbe lo spostamento nella forma canonica,
    /// e da quel momento non sarebbe piu' riconoscibile come tale. Un contratto che
    /// cambia significato a seconda di dove lo si apre non e' un contratto.
    /// </para>
    ///
    /// <para>
    /// IN SCRITTURA SI E' RIGOROSI MA NON SI PERDE NIENTE. La forma del contratto ha tre
    /// decimali, ed e' quella che si usa finche' basta. Un istante piu' fine del
    /// millesimo — e <c>DateTimeOffset.UtcNow</c> ne ha sette — verrebbe TRONCATO in
    /// silenzio, e un modello riletto non sarebbe piu' uguale a quello scritto. Quando
    /// la precisione c'e' la si scrive: il testo del motore resta identico a se stesso,
    /// e un istante nato da questa parte del confine non viene limato di nascosto.
    /// </para>
    /// </summary>
    public sealed class IstanteUtcConverter : JsonConverter<DateTimeOffset> {

        private const string FORMA = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
        private const string FORMA_PRECISA = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type tipo, JsonSerializerOptions opzioni) {
            var testo = reader.GetString();
            return DateTimeOffset.Parse(testo!, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset valore, JsonSerializerOptions opzioni) {
            var utc = valore.ToUniversalTime();
            var forma = utc.Ticks % TimeSpan.TicksPerMillisecond == 0 ? FORMA : FORMA_PRECISA;
            writer.WriteStringValue(utc.ToString(forma, CultureInfo.InvariantCulture));
        }
    }
}
