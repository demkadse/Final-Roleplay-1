using System.Globalization;
using Dalamud.Bindings.ImGui;
using FRP1.Plugin.Localization;
using Lumina.Excel.Sheets;

namespace FRP1.Plugin.GameIntegration;

public sealed partial class GamePresentationModule
{
    // UI-thread only, inside the host's theme scope. Localization follows the shared interface language.
    public void DrawSettings()
    {
        if (Volatile.Read(ref disposed) != 0) return;
        ImGui.TextUnformatted(T("Nur lokal · standardmäßig aus", "Local only · off by default"));
        var names = options.Nameplates;
        if (ImGui.Checkbox(T("RP-Namen über Charakteren##gameNames", "RP names on nameplates##gameNames"), ref names))
            SetOptions(options with { Nameplates = names });
        ImGui.BeginDisabled(!options.Nameplates);
        var titles = options.Titles;
        if (ImGui.Checkbox(T("RP-Titel übernehmen##gameTitles", "Use RP titles##gameTitles"), ref titles))
            SetOptions(options with { Titles = titles });
        var colors = options.ProfileColors;
        if (ImGui.Checkbox(T("Profilfarben übernehmen##gameColors", "Use profile colors##gameColors"), ref colors))
            SetOptions(options with { ProfileColors = colors });
        var icons = options.ProfileIcons;
        if (ImGui.Checkbox(T("Profilicon links vom Namen anzeigen##gameIcons", "Show profile icon beside the name##gameIcons"), ref icons))
            SetOptions(options with { ProfileIcons = icons });
        ImGui.EndDisabled();
        var chatNames = options.ChatNames;
        if (ImGui.Checkbox(T("RP-Namen im lokalen Chat anzeigen##gameChat", "Show RP names in local chat##gameChat"), ref chatNames))
            SetOptions(options with { ChatNames = chatNames });
        if (ImGui.IsItemHovered() || ImGui.IsItemFocused())
        {
            ImGui.BeginTooltip(); ImGui.PushTextWrapPos(ImGui.GetFontSize() * 26);
            ImGui.TextWrapped(T("Im Chat werden ausschließlich Namen jugendfreier Profile ersetzt. Namen aus Ab-18-Profilen bleiben unverändert, " +
                "weil gedruckte Chatgeschichte bei Widerruf oder Sperren nicht rückwirkend zuverlässig bereinigt werden kann. " +
                "Chattext, echtes Klickziel und Spielschriften bleiben erhalten.",
                "Chat names are replaced only for general-audience profiles. Names from mature profiles remain unchanged because printed chat history " +
                "cannot reliably be cleaned up after withdrawal or blocking. Message text, real player links and game fonts stay intact."));
            ImGui.PopTextWrapPos(); ImGui.EndTooltip();
        }
        if (ImGui.CollapsingHeader(T("Hinweise zur Spielanzeige", "Game presentation notes")))
        {
            ImGui.TextWrapped(T("Nur bekannte, von dir zugeordnete und aktuell erlaubte Profile ersetzen Namen. Eine Zuordnung verifiziert kein Spielkonto. Chatnamen werden nur für jugendfreie Profile ersetzt.",
                "Only known, explicitly assigned and currently permitted profiles replace names. An assignment does not verify a game account. Chat names use general-audience profiles only."));
        }
        if (load is not null) ImGui.TextUnformatted(T("Einstellungen werden geladen …", "Loading settings …"));
        if (loadFailed || saveFailed)
        {
            ImGui.TextWrapped(loadFailed ? T("Lokale Anzeigeoptionen konnten nicht geladen werden. Standardwerte bleiben aktiv, soweit du nichts geändert hast.",
                "Local presentation settings could not load. Defaults remain active unless you changed them.") :
                T("Anzeigeoptionen konnten nicht gespeichert werden. Deine Wahl bleibt in dieser Sitzung erhalten.",
                "Presentation settings could not save. Your choices remain active this session."));
            if (ImGui.Button(T("Erneut versuchen##gameOptions", "Retry##gameOptions"))) RetryOptions();
        }
    }

    public void DrawMapPanel()
    {
        if (Volatile.Read(ref disposed) != 0) return;
        ImGui.TextUnformatted(T("Bekannte Charaktere in der Nähe", "Known characters nearby"));
        if (ImGui.IsItemHovered() || ImGui.IsItemFocused())
        {
            ImGui.BeginTooltip(); ImGui.PushTextWrapPos(ImGui.GetFontSize() * 26);
            ImGui.TextUnformatted(T("Nur lokal sichtbare, zugeordnete Profile. Positionen bleiben lokal; die Karte öffnet sich erst beim Anklicken.", "Only locally visible, assigned profiles. Positions stay local; the map opens when clicked."));
            ImGui.PopTextWrapPos(); ImGui.EndTooltip();
        }
        var snapshot = Volatile.Read(ref published);
        if (snapshot is null || !CanUse(snapshot) || snapshot.Entries.Count == 0)
        {
            ImGui.TextUnformatted(T("Keine bekannten erlaubten Charaktere in der aktuellen Umgebung.", "No known permitted characters in the current area."));
            return;
        }
        Map? map;
        try { map = data.GetExcelSheet<Map>()?.GetRowOrDefault(snapshot.Map); }
        catch (Exception ex) when (Expected(ex))
        {
            ImGui.TextWrapped(T("Lokale Kartendaten konnten nicht gelesen werden.", "Local map data could not be read."));
            return;
        }
        if (map is null || map.Value.TerritoryType.RowId != snapshot.Territory)
        {
            ImGui.TextWrapped(T("Für das aktuelle Gebiet sind keine passenden Kartendaten verfügbar.", "Matching map data is unavailable for this area."));
            return;
        }
        ImGui.BeginChild("##gameKnownMap", new System.Numerics.Vector2(0, 320), true);
        var index = 0;
        foreach (var entry in snapshot.Entries.Values.OrderBy(value => value.Distance))
        {
            if (!CanUse(snapshot)) break;
            ImGui.PushID(index++);
            if (GameDisplayRules.TryMapCoordinates(entry.Position, map.Value.SizeFactor, map.Value.OffsetX, map.Value.OffsetY, out var position))
            {
                if (ImGui.SmallButton(T("Karte", "Map")) && CanUse(snapshot))
                {
                    try
                    {
                        // The official world-position overload performs native MapLink conversion, including map metadata.
                        if (!game.OpenMapWithMapLink(snapshot.Territory, snapshot.Map, entry.Position))
                            mapFailed = true;
                        else mapFailed = false;
                    }
                    catch (Exception ex) when (Expected(ex)) { mapFailed = true; }
                }
                if ((ImGui.IsItemHovered() || ImGui.IsItemFocused()) && CanUse(snapshot))
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted(string.Format(CultureInfo.CurrentCulture, "({0:0.0}, {1:0.0})", position.X, position.Y));
                    ImGui.EndTooltip();
                }
            }
            else ImGui.TextUnformatted("—");
            ImGui.SameLine();
            if (CanUse(snapshot)) ImGui.TextUnformatted(entry.RpName + " · " + entry.Distance.ToString("0.0", CultureInfo.CurrentCulture) + " m");
            ImGui.Separator(); ImGui.PopID();
        }
        ImGui.EndChild();
        if (mapFailed) ImGui.TextWrapped(T("Die lokale Spielkarte konnte nicht geöffnet werden.", "The local game map could not open."));
    }

    private bool mapFailed;

    public void SetOptions(GamePresentationOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SchemaVersion != 1) throw new InvalidDataException("Unsupported game display settings.");
        if (Volatile.Read(ref disposed) != 0 || value == options) return;
        options = value; optionsVersion++; savePending = true; saveFailed = false; loadFailed = false;
        Invalidate(); StartSave();
    }

    private void PollOptions()
    {
        if (load?.IsCompleted == true)
        {
            try
            {
                var loaded = load.GetAwaiter().GetResult();
                if (loadVersion == optionsVersion) { options = loaded; Invalidate(); }
                loadFailed = false;
            }
            catch (Exception ex) when (Expected(ex)) { if (loadVersion == optionsVersion) loadFailed = true; }
            finally { load = null; }
        }
        if (save?.IsCompleted == true)
        {
            try { save.GetAwaiter().GetResult(); saveFailed = false; }
            catch (Exception ex) when (Expected(ex)) { saveFailed = true; }
            finally { save = null; }
        }
        if (savePending) StartSave();
    }

    private void StartSave()
    {
        if (save is not null || !savePending || Volatile.Read(ref disposed) != 0) return;
        var desired = options; savePending = false;
        save = Task.Run(() => store.SaveAsync(desired, lifetime.Token));
    }

    private void RetryOptions()
    {
        if (loadFailed && optionsVersion == 0 && load is null)
        {
            loadVersion = optionsVersion; loadFailed = false;
            load = Task.Run(() => store.LoadAsync(lifetime.Token));
        }
        else { loadFailed = false; savePending = true; StartSave(); }
    }

    private static string T(string german, string english) => UiText.CurrentLanguage == UiLanguage.English ? english : german;
}
