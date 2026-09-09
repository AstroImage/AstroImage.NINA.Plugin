using System.Reflection;
using System.Runtime.InteropServices;

// IL MANIFEST DEL PLUGIN NON E' UN FILE: E' L'ASSEMBLY STESSO.
//
// PluginBase legge per riflessione gli attributi qui sotto e ne popola le proprieta'
// di IPluginManifest. Non esiste un manifest.json dentro il plugin — quello del
// catalogo e' un'altra cosa, e sta nel repository dei manifest di N.I.N.A.

// L'IDENTITA' PERMANENTE. N.I.N.A. usa questo GUID come chiave stabile del plugin, e il
// catalogo ufficiale ci lega tutte le versioni pubblicate. Cambiarlo dopo la prima
// distribuzione significa creare un plugin diverso agli occhi di tutti.
[assembly: Guid("3AC982AD-64D9-45F0-99EA-56063E94E206")]

// Il nome NON contiene trattini lunghi ne' caratteri fuori dall'ASCII, e la ragione e'
// pratica: la pagina delle opzioni si aggancia con una chiave di DataTemplate che deve
// combaciare con AssemblyTitle CARATTERE PER CARATTERE. Un trattino lungo scritto in un
// posto e corto nell'altro produce una pagina opzioni vuota, senza nessun errore.
[assembly: AssemblyTitle("AstroImage Strategy Bridge")]
// La descrizione breve e' quella che il catalogo mostra sotto il nome, ed e' in
// inglese come tutto cio' che il registro pubblica: il catalogo lo apre gente in
// mezzo mondo. Le parole dell'interfaccia stanno invece in Localization/, in due
// lingue.
[assembly: AssemblyDescription("Brings a prescription computed by AstroImage-Strategy into the N.I.N.A. Advanced Sequencer.")]
[assembly: AssemblyCompany("Alessandro Curci")]
[assembly: AssemblyProduct("AstroImage Strategy Bridge")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Alessandro Curci")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: ComVisible(false)]

// --- METADATI CHE IL CATALOGO DI N.I.N.A. LEGGE ---
// Le chiavi sono quelle del template ufficiale isbeorn/nina.plugin.template.
[assembly: AssemblyMetadata("Id", "3AC982AD-64D9-45F0-99EA-56063E94E206")]
[assembly: AssemblyMetadata("Name", "AstroImage Strategy Bridge")]
[assembly: AssemblyMetadata("Author", "Alessandro Curci")]
[assembly: AssemblyMetadata("Homepage", "https://github.com/AstroImage/AstroImage.NINA.Plugin")]
[assembly: AssemblyMetadata("Repository", "https://github.com/AstroImage/AstroImage.NINA.Plugin")]
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("ChangelogURL", "")]
[assembly: AssemblyMetadata("Tags", "AstroImage,Strategy,Sequencer,Planning")]

// La versione di N.I.N.A. sotto la quale questo plugin non va caricato. Coincide con
// quella contro cui si compila: si dichiara cio' che si e' davvero provato.
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.9001")]

[assembly: AssemblyMetadata("FeaturedImageURL", "")]
[assembly: AssemblyMetadata("ScreenshotURL", "")]
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
/*  LA DESCRIZIONE E' LA PRIMA COSA CHE UN REVISORE LEGGE, e per nove commit ha detto
 *  «questa versione non fa ancora nulla» mentre il ponte consegnava bersagli su una
 *  montatura vera. Un testo falso lasciato li' diventa un testo che nessuno rilegge:
 *  va corretto quando cambia il fatto, non alla vigilia della candidatura. */
[assembly: AssemblyMetadata("LongDescription",
    "Brings into N.I.N.A. the prescription computed by AstroImage-Strategy.\n\n" +
    "Strategy decides what to shoot \u2014 which channels, how many hours, what sub length, " +
    "which gain mode \u2014 starting from the physics of the object, the sensor, the filters " +
    "you actually have in the wheel and the sky above you. This bridge decides nothing: it " +
    "takes a prescription that has already been worked out and builds it in the Advanced " +
    "Sequencer. It never starts anything: pressing Play is your job.\n\n" +
    "YOU ALSO NEED THE ENGINE. On its own this plugin does nothing: on the other side " +
    "there must be AstroImage-Strategy answering on an HTTP port. Without it the panel " +
    "opens and tells you the engine is not answering.\n\n" +
    "WHAT IT DOES. It asks for a prescription and shows it; it lets you declare which " +
    "filters are really in your wheel, once per N.I.N.A. profile; it builds the target in " +
    "the Advanced Sequencer with the exposure time, the number of exposures, the gain and " +
    "the filter taken from the prescription. If a value does not land where it should, the " +
    "target is not delivered and you are told why: a sequence that does not follow the " +
    "prescription looks, at a glance, exactly like one that does.\n\n" +
    "THE IDEA. Planning a session is not reading a table of tips: it is working out a " +
    "consequence from the physics of the object, the sensor in use, the filter in front of " +
    "it and the sky of that particular night. Three rules follow from that. Every number " +
    "carries where it came from, and where a measurement does not exist the plugin says so " +
    "instead of inventing one. Nothing is inferred and everything is declared, because a " +
    "filter name does not yield nanometres. And nothing is ever corrected in silence, " +
    "because a sequence that departs from the prescription is indistinguishable, on " +
    "screen, from one that follows it.\n\n" +
    "WHO MADE IT. Author and maintainer: Alessandro Curci \u2014 his are the idea, the " +
    "architecture, the decisions and the testing under real skies, and his is the " +
    "scientific engine this bridge connects to. The code was largely written by an " +
    "artificial intelligence assistant (Claude, by Anthropic) under his direction and his " +
    "review: a tool the code was written with, not an author of the project.\n\n" +
    "LANGUAGE. The panel speaks English, and Italian when N.I.N.A. is set to Italian.")]
