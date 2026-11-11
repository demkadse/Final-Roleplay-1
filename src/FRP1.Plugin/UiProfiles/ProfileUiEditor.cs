using System.Numerics;
using Dalamud.Bindings.ImGui;
using FRP1.Core.Profiles;

namespace FRP1.Plugin.UiProfiles;

public sealed partial class ProfileUiModule
{
    private ExtendedProfileDraft? activeDraft;
    private Action? draftChanged;
    private ContentRating editorRating;
    private Task<PortraitThumbnail>? importing;
    private CancellationTokenSource? importCancellation;
    private long importGeneration;
    private string? sourcePath;
    private PortraitThumbnail? candidate;
    private float cropX = .5f;
    private float cropY = .5f;
    private float cropZoom = 1f;
    private bool cropChanged;
    private string? portraitFeedback;

    public void ResetEditorContext()
    {
        editorGeneration++;
        importCancellation?.Cancel();
        activeDraft = null; draftChanged = null; candidate = null; sourcePath = null;
        picker.IsOpen = false; iconCallback = null;
        dialogs.Reset();
        editorPortrait.Clear();
        portraitFeedback = null;
    }

    public Action<string, string>? HelpHeading { get; set; }

    private bool PrepareEditor(ExtendedProfileDraft draft, Action changed, ContentRating? contentRating)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(changed);
        var rating = contentRating ?? draft.ContentRating;
        if (!ReferenceEquals(activeDraft, draft) || rating != editorRating)
        {
            ResetEditorContext();
            activeDraft = draft;
            editorRating = rating;
        }
        draftChanged = changed;
        return EditorAllowed();
    }

    // Kept as the narrow character-fields entry point for existing host callers.
    public void DrawEditor(ExtendedProfileDraft draft, Action changed, ContentRating? contentRating = null) =>
        DrawCharacterData(draft, changed, contentRating);

    public void DrawCharacterData(ExtendedProfileDraft draft, Action changed, ContentRating? contentRating = null)
    {
        if (!PrepareEditor(draft, changed, contentRating)) return;
        if (ImGui.InputText(ProfileUiText.Id("Augenfarbe"), ref draft.EyeColor, 320)) changed();
        if (ImGui.InputText(ProfileUiText.Id("Größe"), ref draft.Height, 320)) changed();
        if (ImGui.InputText(ProfileUiText.Id("Gewicht"), ref draft.Weight, 320)) changed();
        if (ImGui.InputText(ProfileUiText.Id("Geburtsort"), ref draft.Birthplace, 640)) changed();
        if (ImGui.InputText(ProfileUiText.Id("Wohnort"), ref draft.Residence, 640)) changed();
        Wrapped(ProfileUiText.Get("RP-Sprachen: eine frei beschriebene Sprache je Zeile, höchstens zwölf."));
        if (ImGui.InputTextMultiline("##RpLanguages", ref draft.LanguagesText, 4096, new Vector2(-1, 85))) changed();
    }

    public void DrawExperience(ExtendedProfileDraft draft, Action changed, ContentRating? contentRating = null)
    {
        if (!PrepareEditor(draft, changed, contentRating)) return;
        Wrapped(ProfileUiText.Get("Diese Angaben sind Gesprächshinweise. Sie erteilen keine Einwilligung und keine Altersfreigabe."));
        if (ImGui.BeginCombo(ProfileUiText.Id("RP-Erfahrung"), Experience(draft.Preferences.Experience)))
        {
            foreach (var experience in Enum.GetValues<RpExperience>())
                if (ImGui.Selectable(Experience(experience), draft.Preferences.Experience == experience))
                { draft.Preferences = draft.Preferences with { Experience = experience }; changed(); }
            ImGui.EndCombo();
        }
        var style = draft.Preferences.PreferredStyle;
        if (ImGui.InputTextMultiline(ProfileUiText.Id("Bevorzugter RP-Stil"), ref style, 2048, new Vector2(-1, 70)))
        { draft.Preferences = draft.Preferences with { PreferredStyle = style }; changed(); }
        Preference(ProfileUiText.Id("Verletzungen"), draft.Preferences.Injury, value => draft.Preferences = draft.Preferences with { Injury = value }, changed);
        Preference(ProfileUiText.Id("Charaktertod"), draft.Preferences.CharacterDeath, value => draft.Preferences = draft.Preferences with { CharacterDeath = value }, changed);
        Preference(ProfileUiText.Id("Romantik"), draft.Preferences.Romance, value => draft.Preferences = draft.Preferences with { Romance = value }, changed);
        Preference(ProfileUiText.Id("Kriminalität"), draft.Preferences.Crime, value => draft.Preferences = draft.Preferences with { Crime = value }, changed);
        Preference(ProfileUiText.Id("Kontrollverlust"), draft.Preferences.LossOfControl, value => draft.Preferences = draft.Preferences with { LossOfControl = value }, changed);
    }

    public void DrawHeaderDesign(ExtendedProfileDraft draft, Action changed, ContentRating? contentRating = null)
    {
        if (!PrepareEditor(draft, changed, contentRating)) return;
        editorPortrait.Set(candidate ?? draft.Portrait);
        if (editorPortrait.Current is { } portrait) ImGui.Image(portrait.Handle, new Vector2(64));
        DrawIcon(draft.Appearance.IconId, new Vector2(28));
        if (ImGui.Button(ProfileUiText.Id("Portrait und Profilgestaltung"))) ImGui.OpenPopup("profile-header-design");
        var available = Vector2.Max(Vector2.One, ImGui.GetMainViewport().WorkSize - new Vector2(16));
        var width = MathF.Min(540, available.X);
        ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0), new Vector2(width, available.Y));
        if (!ImGui.BeginPopup("profile-header-design")) return;
        try
        {
            if (!EditorAllowed()) { ImGui.CloseCurrentPopup(); return; }
            DrawHelpHeading("Öffentliche Profilgestaltung", "Formatting");
            Wrapped(ProfileUiText.Get("Diese Gestaltung wird mit deinem Profil geteilt. Das persönliche FRP1-Fensterdesign bleibt davon getrennt."));
            if (ImGui.Button(ProfileUiText.Id("Profilicon auswählen")))
                OpenIconPicker(draft.Appearance.IconId, id => { draft.Appearance = draft.Appearance with { IconId = id }; changed(); }, editorRating);
            var nameColor = draft.Appearance.NameColor;
            var accent = draft.Appearance.AccentColor;
            if (ColorEditor(ProfileUiText.Id("Namensfarbe"), ref nameColor)) { draft.Appearance = draft.Appearance with { NameColor = nameColor }; changed(); }
            if (ColorEditor(ProfileUiText.Id("Akzentfarbe"), ref accent)) { draft.Appearance = draft.Appearance with { AccentColor = accent }; changed(); }
            if (ImGui.BeginCombo(ProfileUiText.Id("Profilvorlage"), Layout(draft.Appearance.Layout)))
            {
                foreach (var layout in Enum.GetValues<AboutLayout>())
                    if (ImGui.Selectable(Layout(layout), layout == draft.Appearance.Layout))
                    { draft.Appearance = draft.Appearance with { Layout = layout }; changed(); }
                ImGui.EndCombo();
            }
            DrawHelpHeading("Portrait", "Portrait");
            DrawPortraitEditor(draft, changed);
            if (ImGui.Button(ProfileUiText.Id("Schließen"))) ImGui.CloseCurrentPopup();
        }
        finally { ImGui.EndPopup(); }
    }

    private void DrawHelpHeading(string title, string topic)
    {
        if (HelpHeading is { } heading) heading(ProfileUiText.Get(title), topic);
        else ImGui.TextUnformatted(ProfileUiText.Get(title));
    }
    private void DrawPortraitEditor(ExtendedProfileDraft draft, Action changed)
    {
        ImGui.Separator();
        Wrapped(ProfileUiText.Get("Portrait: lokales PNG oder JPEG wählen, quadratischen Ausschnitt bestätigen. Das geteilte Vorschaubild enthält keine Bildmetadaten."));
        ImGui.BeginDisabled(importing is not null);
        if (ImGui.Button(ProfileUiText.Id("Portraitdatei auswählen")))
        {
            var generation = editorGeneration;
            dialogs.OpenFileDialog(ProfileUiText.Id("Lokales Portrait auswählen"), ProfileUiText.Get("Bilder") + "{.png,.jpg,.jpeg}", (selected, path) =>
            {
                if (!selected || generation != editorGeneration || !EditorAllowed()) return;
                sourcePath = path; cropX = cropY = .5f; cropZoom = 1; cropChanged = false;
                BeginPortraitImport();
            });
        }
        ImGui.EndDisabled();
        if (ImGui.Button(ProfileUiText.Id("Portrait entfernen")))
        {
            importCancellation?.Cancel(); editorGeneration++;
            draft.Portrait = null; candidate = null; sourcePath = null; editorPortrait.Clear(); changed();
        }
        if (sourcePath is not null)
        {
            cropChanged |= ImGui.SliderFloat(ProfileUiText.Id("Ausschnitt horizontal"), ref cropX, 0, 1);
            cropChanged |= ImGui.SliderFloat(ProfileUiText.Id("Ausschnitt vertikal"), ref cropY, 0, 1);
            cropChanged |= ImGui.SliderFloat(ProfileUiText.Id("Ausschnitt vergrößern"), ref cropZoom, 1, 4);
            ImGui.BeginDisabled(importing is not null);
            if (ImGui.Button(ProfileUiText.Id("Ausschnitt als Vorschau aktualisieren"))) BeginPortraitImport();
            ImGui.EndDisabled();
        }
        editorPortrait.Set(candidate ?? draft.Portrait);
        if (editorPortrait.Current is { } texture) ImGui.Image(texture.Handle, new Vector2(112));
        if (candidate is not null)
        {
            ImGui.BeginDisabled(importing is not null || cropChanged);
            if (ImGui.Button(ProfileUiText.Id("Portrait übernehmen")))
            { draft.Portrait = candidate; candidate = null; sourcePath = null; changed(); }
            ImGui.EndDisabled();
            if (cropChanged) Wrapped(ProfileUiText.Get("Bitte die geänderte Vorschau zuerst aktualisieren."));
        }
        if (importing is not null) Wrapped(ProfileUiText.Get("Das Vorschaubild wird lokal vorbereitet …"));
        if (portraitFeedback is not null) Wrapped(ProfileUiText.Get(portraitFeedback));
    }

    private void BeginPortraitImport()
    {
        if (sourcePath is null || !EditorAllowed() || importing is not null) return;
        importCancellation?.Dispose();
        importCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        importGeneration = editorGeneration;
        var path = sourcePath;
        var crop = new PortraitCrop(cropX, cropY, cropZoom);
        var token = importCancellation.Token;
        importing = Task.Run(() => PortraitNormalizer.FromFileAsync(path, crop, token), token);
        cropChanged = false;
        portraitFeedback = null;
    }

    private void PollPortraitImport()
    {
        if (importing is not { IsCompleted: true } completed) return;
        importing = null;
        try
        {
            var result = completed.GetAwaiter().GetResult();
            if (importGeneration == editorGeneration && EditorAllowed())
            { candidate = result; portraitFeedback = "Vorschau bereit. Übernehmen speichert sie zunächst nur im Entwurf."; }
        }
        catch (Exception ex) when (Expected(ex))
        { if (importGeneration == editorGeneration) portraitFeedback = "Das Bild konnte nicht verwendet werden. Bitte ein gültiges lokales PNG/JPEG innerhalb der Grenzen wählen."; }
        finally { importCancellation?.Dispose(); importCancellation = null; }
    }

    private bool EditorAllowed()
    {
        if (!HasAccess || activeDraft is null) return false;
        try { policy.EnsureAllowed(new ProfileDocument { ContentRating = editorRating }); return true; }
        catch (InvalidDataException) { return false; }
    }

    private static bool ColorEditor(string label, ref string hex)
    {
        ImGui.PushID(label);
        var color = hex.Length == 7 ? ImGui.ColorConvertU32ToFloat4(RichTextView.Color(hex)) : ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        var rgb = new Vector3(color.X, color.Y, color.Z);
        var changed = ImGui.ColorEdit3(label, ref rgb, ImGuiColorEditFlags.NoInputs);
        if (changed) hex = $"#{(int)Math.Clamp(MathF.Round(rgb.X * 255), 0, 255):X2}{(int)Math.Clamp(MathF.Round(rgb.Y * 255), 0, 255):X2}{(int)Math.Clamp(MathF.Round(rgb.Z * 255), 0, 255):X2}";
        if (ImGui.Button(ProfileUiText.Id("Standardfarbe"))) { hex = ""; changed = true; }
        ImGui.PopID();
        return changed;
    }

    private static void Preference(string label, PreferenceAnswer current, Action<PreferenceAnswer> apply, Action changed)
    {
        if (!ImGui.BeginCombo(label, Answer(current))) return;
        foreach (var value in Enum.GetValues<PreferenceAnswer>())
            if (ImGui.Selectable(Answer(value), value == current)) { apply(value); changed(); }
        ImGui.EndCombo();
    }

    private static IEnumerable<(string Label, PreferenceAnswer Value)> PreferenceValues(RpPreferences value) =>
    [ (ProfileUiText.Get("Verletzungen"), value.Injury), (ProfileUiText.Get("Charaktertod"), value.CharacterDeath), (ProfileUiText.Get("Romantik"), value.Romance),
        (ProfileUiText.Get("Kriminalität"), value.Crime), (ProfileUiText.Get("Kontrollverlust"), value.LossOfControl) ];

    private static string Answer(PreferenceAnswer value) => value switch
    { PreferenceAnswer.Yes => ProfileUiText.Get("Ja"), PreferenceAnswer.No => ProfileUiText.Get("Nein"), PreferenceAnswer.AskFirst => ProfileUiText.Get("Nur nach Absprache"), _ => ProfileUiText.Get("Keine Angabe") };
    private static string Experience(RpExperience value) => value switch
    { RpExperience.Newcomer => ProfileUiText.Get("Neu im RP"), RpExperience.Experienced => ProfileUiText.Get("Erfahren"), RpExperience.Guide => ProfileUiText.Get("Ansprechpartner"), _ => ProfileUiText.Get("Keine Angabe") };
    private static string Layout(AboutLayout value) => value switch
    { AboutLayout.Chronicle => ProfileUiText.Get("Chronik"), AboutLayout.Compact => ProfileUiText.Get("Kompakt"), _ => ProfileUiText.Get("Abschnitte") };
    private static string CompanionType(CompanionKind value) => value switch
    { CompanionKind.Mount => ProfileUiText.Get("Reittier"), CompanionKind.Other => ProfileUiText.Get("Sonstiger Begleiter"), _ => ProfileUiText.Get("Haustier / Gefährte") };
    private static string Status(RpStatus value) => value switch
    { RpStatus.Ic => "IC", RpStatus.Afk => "AFK", _ => "OOC" };
    private static string Availability(RpAvailability value) => value switch
    { RpAvailability.Open => ProfileUiText.Get("Offen für RP"), RpAvailability.Busy => ProfileUiText.Get("Beschäftigt"), _ => ProfileUiText.Get("Kein RP") };
    private static string Label(string key) => key switch
    {
        "FirstName" => ProfileUiText.Get("Vorname"), "LastName" => ProfileUiText.Get("Nachname"), "Race" => ProfileUiText.Get("Volk"), "Clan" => ProfileUiText.Get("Clan"),
        "Age" => ProfileUiText.Get("Charakteralter"), "Gender" => ProfileUiText.Get("Geschlecht"), "Origin" => ProfileUiText.Get("Herkunft"), "Occupation" => ProfileUiText.Get("Beruf"),
        "EyeColor" => ProfileUiText.Get("Augenfarbe"), "Height" => ProfileUiText.Get("Größe"), "Weight" => ProfileUiText.Get("Gewicht"), "Birthplace" => ProfileUiText.Get("Geburtsort"),
        "Residence" => ProfileUiText.Get("Wohnort"), "FreeCompany" => ProfileUiText.Get("Freie Gesellschaft"), "Appearance" => ProfileUiText.Get("Aussehen"),
        "Personality" => ProfileUiText.Get("Persönlichkeit"), "Background" => ProfileUiText.Get("Hintergrund"), _ => key,
    };
}
