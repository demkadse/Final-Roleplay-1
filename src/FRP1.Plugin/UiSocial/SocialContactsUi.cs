using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using FRP1.Core.Profiles;
using FRP1.Plugin.Contacts;
using FRP1.Plugin.Presentation;

namespace FRP1.Plugin.UiSocial;

public sealed partial class SocialUiModule
{
    private string contactSearch = "";
    private bool environmentView;
    private bool favoritesOnly;
    private int relationshipFilter = -1;
    private uint? worldFilter;
    private string aliasDraft = "";
    private string profileNoteDraft = "";
    private string characterNoteDraft = "";
    private string customRelationshipDraft = "";
    private RelationshipKind relationshipDraft;
    private bool favoriteDraft;
    private bool contactDirty;
    private bool characterNoteDirty;
    private long contactDraftVersion = -1;
    private CharacterKey? characterNoteKey;
    private ActorSnapshot? bindingChoice;
    private readonly Dictionary<ProfileKey, bool> environmentMatches = [];
    private long environmentMatchesVersion = -1;
    private EnvironmentFilter? environmentMatchesFilter;

    public void DrawContactsTab()
    {
        if (!AccessReady) { Text(T("Kontakte nach Hinweis- und Altersauswahl verfügbar.", "Contacts available after guidance and age selection.")); return; }
        if (!register.IsLoaded)
        {
            Text(register.ErrorMessage is null ? T("Register wird geladen …", "Loading register …") : T("Register konnte nicht geladen werden.", "Register could not load."));
            if (!register.IsBusy && ImGui.Button(T("Erneut laden", "Reload"))) register.Retry();
            return;
        }
        if (ImGui.Button(T("Kontakte", "Contacts"))) environmentView = false;
        ImGui.SameLine();
        if (ImGui.Button(T("Umgebung", "Nearby"))) environmentView = true;
        if (feedback is not null) Text(feedback);
        if (register.ErrorMessage is not null)
        {
            Text(T("Änderung noch nicht gespeichert.", "Change not saved yet."));
            if (!register.IsBusy && ImGui.Button(T("Erneut speichern", "Retry saving"))) register.Retry();
        }
        if (environmentView) { DrawEnvironmentTab(); return; }
        ImGui.SetNextItemWidth(MathF.Max(80, ImGui.GetContentRegionAvail().X - 90 * ImGuiHelpers.GlobalScale));
        ImGui.InputTextWithHint("##contact-search", T("Kontakte suchen", "Search contacts"), ref contactSearch, 160);
        if (ImGui.IsItemHovered() || ImGui.IsItemFocused())
        { ImGui.BeginTooltip(); Text(T("RP-Name, lokaler Name oder Charakter", "RP name, local name or character")); ImGui.EndTooltip(); }
        ImGui.SameLine();
        if (ImGui.Button(T("Filter", "Filters"))) ImGui.OpenPopup("contact-filters");
        if (ImGui.BeginPopup("contact-filters"))
        {
            try { DrawContactFilters(); }
            finally { ImGui.EndPopup(); }
        }
        var wide = ImGui.GetContentRegionAvail().X >= 540 * ImGuiHelpers.GlobalScale;
        if (wide && ImGui.BeginTable("##contact-layout", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
        {
            try
            {
                ImGui.TableSetupColumn("Contacts", ImGuiTableColumnFlags.WidthStretch, 1);
                ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch, 1.7f);
                ImGui.TableNextColumn();
                ImGui.BeginChild("##contact-list", Vector2.Zero, false);
                try { DrawContactList(); }
                finally { ImGui.EndChild(); }
                ImGui.TableNextColumn();
                ImGui.BeginChild("##contact-details", Vector2.Zero, false);
                try { DrawContactDetails(); }
                finally { ImGui.EndChild(); }
            }
            finally { ImGui.EndTable(); }
        }
        else if (!wide)
        {
            ImGui.BeginChild("##contact-list", new Vector2(0, 150 * ImGuiHelpers.GlobalScale), false);
            try { DrawContactList(); }
            finally { ImGui.EndChild(); }
            ImGui.Separator(); DrawContactDetails();
        }
    }

    private void DrawContactFilters()
    {
        ImGui.Checkbox(T("Nur Favoriten", "Favorites only"), ref favoritesOnly);
        if (ImGui.BeginCombo(T("Beziehung", "Relationship"), relationshipFilter < 0 ? T("Alle", "All") : RelationshipLabel((RelationshipKind)relationshipFilter)))
        {
            if (ImGui.Selectable(T("Alle", "All"), relationshipFilter < 0)) relationshipFilter = -1;
            foreach (var relationship in Enum.GetValues<RelationshipKind>())
                if (ImGui.Selectable(RelationshipLabel(relationship), relationshipFilter == (int)relationship)) relationshipFilter = (int)relationship;
            ImGui.EndCombo();
        }
        if (ImGui.BeginCombo(T("Heimatwelt", "Home world"), worldFilter.HasValue ? WorldLabel(worldFilter.Value) : T("Alle", "All")))
        {
            if (ImGui.Selectable(T("Alle", "All"), !worldFilter.HasValue)) worldFilter = null;
            var worlds = ActorSnapshots.Select(a => a.Actor.HomeWorldId).Concat(register.Snapshot.Contacts
                .Where(c => c.Binding is not null).Select(c => c.Binding!.Character.HomeWorldId)).Distinct().Order();
            foreach (var world in worlds)
                if (ImGui.Selectable(WorldLabel(world), worldFilter == world)) worldFilter = world;
            ImGui.EndCombo();
        }
    }

    private void DrawContactList()
    {
        var views = resolver.Search(contactSearch, homeWorldId: worldFilter, favoritesOnly: favoritesOnly,
            relationship: relationshipFilter < 0 ? null : (RelationshipKind)relationshipFilter);
        if (views.Count == 0) Text(T("Keine passenden empfangenen Profile.", "No matching received profiles."));
        for (var index = 0; index < views.Count; index++)
        {
            var view = views[index];
            if (!view.IsReadable || view.Key is not { } key) continue;
            ImGui.PushID(key.Fingerprint + ":" + key.ProfileId.ToString("N"));
            var name = view.DisplayName;
            if (!view.IsReadable || view.Key != key) { ImGui.PopID(); continue; }
            var before = ImGui.GetCursorPos();
            var clicked = ImGui.Selectable("##contact-row", selectedContact == key);
            var hovered = ImGui.IsItemHovered() || ImGui.IsItemFocused();
            var after = ImGui.GetCursorPos();
            ImGui.SetCursorPos(before);
            if (view.IsReadable && view.Key == key) ImGui.TextUnformatted((view.Contact?.Favorite == true ? "* " : "") + name);
            ImGui.SetCursorPos(after);
            if (clicked) SelectContact(key);
            if (hovered)
            {
                ImGui.BeginTooltip();
                try
                {
                    if (view.IsReadable && view.Key == key)
                    {
                        Text(view.Connected ? T("Direkt verbunden", "Directly connected") : T("Lokale Profilkopie · keine Anwesenheitsanzeige", "Local profile copy · not a presence indicator"));
                        if (view.ReceivedAt is { } at) Text(at.ToLocalTime().ToString("g"));
                    }
                }
                finally { ImGui.EndTooltip(); }
            }
            ImGui.PopID();
        }
        ImGui.Separator(); DrawMutedManagement();
        if (ImGui.CollapsingHeader(T("Verlauf", "History")))
            foreach (var entry in resolver.History().Take(100))
            {
                if (!entry.Profile.IsReadable) continue;
                Text(entry.At.ToLocalTime().ToString("g") + " · " + entry.Profile.DisplayName +
                    (entry.Kind == EncounterKind.Seen ? T(" · gesehen", " · seen") : T(" · geöffnet", " · opened")));
            }
    }

    private void DrawContactDetails()
    {
        if (selectedContact is not { } selected) { Text(T("Kontakt auswählen", "Select a contact")); return; }
        var view = resolver.GetProfile(selected);
        if (!view.IsReadable) { selectedContact = null; ClearContactDraft(); return; }
        DrawSelectedContact(view, selected);
    }

    private void SelectContact(ProfileKey key)
    {
        if (register.IsBusy) { feedback = T("Bitte die laufende Kontaktänderung abschließen lassen.", "Please wait for the pending contact change."); return; }
        if (contactDirty || characterNoteDirty) { feedback = T("Bitte den offenen Notizentwurf zuerst speichern oder verwerfen.", "Please save or discard the open note draft first."); return; }
        if (!resolver.GetProfile(key).IsReadable) return;
        selectedContact = key;
        LoadContactDraft(key);
    }

    private void LoadContactDraft(ProfileKey key)
    {
        var view = resolver.GetProfile(key);
        if (!view.IsReadable) { ClearContactDraft(); return; }
        var contact = view.Contact ?? new LocalContact { Profile = key };
        aliasDraft = contact.Alias; profileNoteDraft = contact.ProfileNote;
        favoriteDraft = contact.Favorite; relationshipDraft = contact.Relationship;
        customRelationshipDraft = contact.CustomRelationship;
        characterNoteKey = contact.Binding?.Character;
        characterNoteDraft = characterNoteKey is { } character ? register.GetCharacterNote(character) : "";
        contactDirty = characterNoteDirty = false;
        contactDraftVersion = register.Version;
    }

    private void ClearContactDraft()
    {
        aliasDraft = profileNoteDraft = characterNoteDraft = customRelationshipDraft = "";
        contactDirty = characterNoteDirty = false;
        characterNoteKey = null; bindingChoice = null; contactDraftVersion = -1;
    }

    private void DrawSelectedContact(ResolvedProfile view, ProfileKey key)
    {
        if (!view.IsReadable) return;
        if (contactDraftVersion != register.Version && !contactDirty && !characterNoteDirty) LoadContactDraft(key);
        ImGui.Separator();
        ImGui.TextUnformatted(view.DisplayName);
        if (ImGui.Button(T("Profil öffnen", "Open profile"))) Open(key);
        if (view.Contact is { } details) Text(RelationshipLabel(details.Relationship) + (details.Favorite ? T(" · Favorit", " · Favorite") : ""));
        var showNotes = ImGui.CollapsingHeader(T("Private Notizen und Beziehung", "Private notes and relationship"));
        ImGui.BeginDisabled(register.IsBusy);
        if (showNotes)
        {
            contactDirty |= ImGui.InputText(T("Eigener Kontaktname", "Your local contact name"), ref aliasDraft, 640);
            contactDirty |= ImGui.Checkbox(T("Favorit", "Favorite"), ref favoriteDraft);
            if (ImGui.BeginCombo(T("Beziehung", "Relationship"), RelationshipLabel(relationshipDraft)))
            {
                foreach (var relation in Enum.GetValues<RelationshipKind>())
                    if (ImGui.Selectable(RelationshipLabel(relation), relationshipDraft == relation))
                    { relationshipDraft = relation; contactDirty = true; }
                ImGui.EndCombo();
            }
            if (relationshipDraft == RelationshipKind.Custom)
                contactDirty |= ImGui.InputText(T("Eigene Beziehungsbezeichnung", "Custom relationship label"), ref customRelationshipDraft, 320);
            contactDirty |= ImGui.InputTextMultiline(T("Private Notiz zu diesem Profil", "Private note about this profile"), ref profileNoteDraft, 32768, new(-1, 110));
        }
        if (contactDirty && ImGui.Button(T("Änderungen lokal speichern", "Save changes locally")))
        {
            var contact = view.Contact ?? new LocalContact { Profile = key };
            var snapshot = contact with { Alias = aliasDraft, ProfileNote = profileNoteDraft, Favorite = favoriteDraft,
                Relationship = relationshipDraft, CustomRelationship = customRelationshipDraft };
            Queue(() => register.SetContactAsync(snapshot), () =>
            {
                if (selectedContact == key && resolver.GetProfile(key).IsReadable)
                { contactDirty = false; contactDraftVersion = register.Version; }
            });
        }
        if (showNotes && characterNoteKey is { } character)
        {
            Text(T("Von dir zugeordnet: ", "Assigned by you: ") + character.Name + T(" · Heimatwelt ", " · home world ") + character.HomeWorldId);
            characterNoteDirty |= ImGui.InputTextMultiline(T("Private Notiz zum Spielcharakter", "Private note about the game character"), ref characterNoteDraft, 32768, new(-1, 95));
            ImGui.BeginDisabled(!characterNoteDirty);
            if (ImGui.Button(T("Charakternotiz lokal speichern", "Save character note locally")))
            {
                var note = characterNoteDraft;
                Queue(() => register.SetCharacterNoteAsync(character, note), () =>
                { if (selectedContact == key) { characterNoteDirty = false; contactDraftVersion = register.Version; } });
            }
            ImGui.EndDisabled();
        }
        ImGui.BeginDisabled(register.IsDirty);
        if ((contactDirty || characterNoteDirty) && ImGui.Button(T("Offenen Entwurf verwerfen", "Discard open draft"))) LoadContactDraft(key);
        ImGui.EndDisabled();
        ImGui.BeginDisabled(contactDirty || characterNoteDirty);
        if (ImGui.CollapsingHeader(T("Stummschalten und Sperren", "Mute and block")))
        {
            if (ImGui.Button(T("Profil stummschalten (nur Anzeige)", "Mute profile (presentation only)")))
            {
                var contact = view.Contact ?? new LocalContact { Profile = key };
                Queue(() => register.SetContactAsync(contact with { Muted = true }));
            }
            ImGui.SameLine();
            if (ImGui.Button(T("Peer sperren und Austausch unterbinden", "Block peer and prevent exchange")))
                Queue(() => register.SetPeerBlockedAsync(key.Fingerprint, true));
        }
        if (ImGui.CollapsingHeader(T("Charakter zuordnen", "Assign character"))) DrawBindingUi(view, key);
        ImGui.EndDisabled();
        ImGui.EndDisabled();
    }

    private void DrawBindingUi(ResolvedProfile view, ProfileKey key)
    {
        Text(T("Eine Charakterzuordnung ist deine lokale Bestätigung, kein verifizierter Accountnachweis.", "A character assignment is your local confirmation, not proof of a verified account."));
        var label = bindingChoice?.Name ?? T("Sichtbaren Spielcharakter wählen", "Choose a visible game character");
        if (ImGui.BeginCombo(T("Zuordnung", "Assignment"), label))
        {
            var index = 0;
            foreach (var actor in ActorSnapshots)
            {
                ImGui.PushID(index++);
                if (ImGui.Button(T("Diesen Charakter wählen", "Choose this character"))) bindingChoice = actor.Actor;
                ImGui.SameLine(); ImGui.TextUnformatted(actor.Actor.Name);
                ImGui.SameLine(); ImGui.TextUnformatted(T("· Welt ", "· world ") + actor.Actor.HomeWorldId);
                ImGui.PopID();
            }
            ImGui.EndCombo();
        }
        if (bindingChoice is { } choice && ImGui.Button(T("Zuordnung ausdrücklich bestätigen", "Explicitly confirm assignment")))
            Queue(() => register.ConfirmBindingAsync(key, new CharacterKey(choice.Name, choice.HomeWorldId)), () =>
            { if (selectedContact == key) LoadContactDraft(key); });
        if (view.Character.HasValue && ImGui.Button(T("Lokale Zuordnung entfernen", "Remove local assignment")))
            Queue(() => register.ClearBindingAsync(key), () => { if (selectedContact == key) LoadContactDraft(key); });
    }

    private void DrawMutedManagement()
    {
        if (!ImGui.CollapsingHeader(T("Stummgeschaltet", "Muted"))) return;
        Text(T("Stummschalten versteckt ein Profil. Es beendet keine Verbindung. Gesperrte Peers verwaltest du in den Direktaustausch-Einstellungen.", "Muting hides a profile. It does not disconnect a peer. Manage blocked peers in the direct-networking settings."));
        var index = 0;
        foreach (var contact in register.Snapshot.Contacts.Where(c => c.Muted))
        {
            ImGui.PushID(index++);
            Text(T("Stummgeschalteter Kontakt", "Muted contact"));
            ImGui.BeginDisabled(register.IsBusy);
            if (ImGui.Button(T("Stummschaltung aufheben", "Unmute"))) Queue(() => register.SetContactAsync(contact with { Muted = false }));
            ImGui.EndDisabled();
            ImGui.PopID();
        }
    }

    public void DrawEnvironmentTab()
    {
        if (!AccessReady) { Text(T("Umgebung nach Hinweis- und Altersauswahl verfügbar.", "Nearby actors available after guidance and age selection.")); return; }
        if (ImGui.Button(T("Filter##nearby", "Filters##nearby"))) ImGui.OpenPopup("environment-filters");
        ImGui.SameLine(); Text(T("Nur lokal sichtbare Charaktere", "Currently visible characters only"));
        if (ImGui.BeginPopup("environment-filters"))
        {
            try
            {
                var known = options.OnlyKnownEnvironment;
                if (ImGui.Checkbox(T("Nur bekannte RP-Profile", "Known RP profiles only"), ref known)) SetOptions(options with { OnlyKnownEnvironment = known });
                if (ImGui.BeginCombo(T("RP-Status", "RP status"), options.EnvironmentStatus is { } status ? StatusLabel(status) : T("Alle", "All")))
                {
                    if (ImGui.Selectable(T("Alle", "All"), options.EnvironmentStatus is null)) SetOptions(options with { EnvironmentStatus = null });
                    foreach (var value in Enum.GetValues<RpStatus>())
                        if (ImGui.Selectable(StatusLabel(value), options.EnvironmentStatus == value)) SetOptions(options with { EnvironmentStatus = value });
                    ImGui.EndCombo();
                }
                if (ImGui.BeginCombo(T("Anspielbarkeit", "Availability"), options.EnvironmentAvailability is { } availability ? AvailabilityLabel(availability) : T("Alle", "All")))
                {
                    if (ImGui.Selectable(T("Alle", "All"), options.EnvironmentAvailability is null)) SetOptions(options with { EnvironmentAvailability = null });
                    foreach (var value in Enum.GetValues<RpAvailability>())
                        if (ImGui.Selectable(AvailabilityLabel(value), options.EnvironmentAvailability == value)) SetOptions(options with { EnvironmentAvailability = value });
                    ImGui.EndCombo();
                }
                var hideEmpty = options.HideEmptyEnvironment;
                if (ImGui.Checkbox(T("Leere Profile ausblenden", "Hide empty profiles"), ref hideEmpty))
                    SetOptions(options with { HideEmptyEnvironment = hideEmpty });
            }
            finally { ImGui.EndPopup(); }
        }
        var filter = new EnvironmentFilter(options.EnvironmentStatus, options.EnvironmentAvailability, options.HideEmptyEnvironment);
        var index = 0;
        foreach (var actor in ActorSnapshots.OrderBy(a => a.Distance).Take(128))
        {
            var view = resolver.Resolve(actor.Actor);
            if (view.State is ProfilePresentationState.Restricted or ProfilePresentationState.Blocked or ProfilePresentationState.Muted or ProfilePresentationState.Ambiguous ||
                (options.OnlyKnownEnvironment && !view.IsReadable) || !MatchesEnvironmentFilter(view, filter)) continue;
            ImGui.PushID(index++);
            var key = view.Key;
            var name = view.IsReadable ? view.DisplayName : actor.Actor.Name;
            ImGui.BeginDisabled(key is null);
            if (ImGui.SmallButton(T("Profil", "Profile")) && key is { } currentKey) Open(currentKey);
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.SmallButton(T("Ziel", "Target")))
            {
                var current = objects.OfType<Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter>().FirstOrDefault(p =>
                    p.EntityId == actor.Actor.LocalEntityId && p.HomeWorld.RowId == actor.Actor.HomeWorldId && p.Name.TextValue == actor.Actor.Name);
                if (current is not null) targets.Target = current;
            }
            ImGui.SameLine();
            if (key is null || (view.IsReadable && view.Key == key))
                Text(name + (float.IsFinite(actor.Distance) ? " · " + actor.Distance.ToString("0.0") + " m" : ""));
            ImGui.PopID();
        }
        if (index == 0) Text(T("Keine passenden Charaktere in der Nähe.", "No matching characters nearby."));
    }

    private bool MatchesEnvironmentFilter(ResolvedProfile view, EnvironmentFilter filter)
    {
        var currentVersion = resolver.Version;
        if (environmentMatchesVersion != currentVersion || environmentMatchesFilter != filter)
        {
            environmentMatches.Clear(); environmentMatchesVersion = currentVersion; environmentMatchesFilter = filter;
        }
        if (!filter.Status.HasValue && !filter.Availability.HasValue && !filter.HideEmpty) return true;
        if (!view.IsReadable || view.Key is not { } key) return SocialUiRules.MatchesEnvironment(null, filter);
        if (!environmentMatches.TryGetValue(key, out var matches))
        {
            matches = SocialUiRules.MatchesEnvironment(view.Profile, filter);
            if (currentVersion != resolver.Version || !view.IsReadable || view.Key != key) return false;
            // Keys originate from the bounded authenticated catalog; only the filter result is retained.
            environmentMatches[key] = matches;
        }
        return matches && view.IsReadable && view.Key == key;
    }

    private static string RelationshipLabel(RelationshipKind value) => value switch
    {
        RelationshipKind.Unspecified => T("Keine Angabe", "Unspecified"), RelationshipKind.Acquaintance => T("Bekanntschaft", "Acquaintance"),
        RelationshipKind.Friend => T("Freundschaft", "Friendship"), RelationshipKind.Family => T("Familie", "Family"), RelationshipKind.Partner => T("Partnerschaft", "Partnership"),
        RelationshipKind.Rival => T("Rivalität", "Rivalry"), RelationshipKind.Custom => T("Eigene Bezeichnung", "Custom label"), _ => T("Keine Angabe", "Unspecified"),
    };

    private string WorldLabel(uint world) => ActorSnapshots.FirstOrDefault(a => a.Actor.HomeWorldId == world)?.HomeWorldName is { Length: > 0 } name
        ? name : T("Heimatwelt ", "Home world ") + world;
}
