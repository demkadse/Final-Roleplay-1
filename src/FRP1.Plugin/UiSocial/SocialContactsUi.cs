using Dalamud.Bindings.ImGui;
using FRP1.Core.Profiles;
using FRP1.Plugin.Contacts;
using FRP1.Plugin.Presentation;

namespace FRP1.Plugin.UiSocial;

public sealed partial class SocialUiModule
{
    private string contactSearch = "";
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
        if (!AccessReady) { Text(T("Kontakte sind nach Hinweisbestätigung und Altersauswahl für deinen Spielcharakter verfügbar.", "Contacts become available for your character after guidance acknowledgement and age selection.")); return; }
        if (!register.IsLoaded)
        {
            Text(register.ErrorMessage is null ? T("Lokales Register wird geladen …", "Loading local register …") :
                T("Das lokale Register konnte nicht gelesen werden. Bitte erneut versuchen.", "The local register could not be read. Please retry."));
            if (!register.IsBusy && ImGui.Button(T("Register erneut laden", "Reload register"))) register.Retry();
            return;
        }
        if (feedback is not null) Text(feedback);
        if (register.ErrorMessage is not null)
        {
            Text(T("Die lokale Kontaktänderung konnte nicht gespeichert werden. Der gewünschte Zustand bleibt für die Wiederholung erhalten.",
                "The local contact change could not be saved. The requested state is retained for retry."));
            if (!register.IsBusy && ImGui.Button(T("Kontaktänderung erneut speichern", "Retry saving contact change"))) register.Retry();
        }
        ImGui.InputText(T("Suche nach RP-Name, lokalem Namen oder zugeordnetem Charakter", "Search by RP name, local name or assigned character"), ref contactSearch, 640);
        ImGui.Checkbox(T("Nur Favoriten", "Favorites only"), ref favoritesOnly);
        if (ImGui.BeginCombo(T("Beziehungsfilter", "Relationship filter"), relationshipFilter < 0 ? T("Alle", "All") : RelationshipLabel((RelationshipKind)relationshipFilter)))
        {
            if (ImGui.Selectable(T("Alle", "All"), relationshipFilter < 0)) relationshipFilter = -1;
            foreach (var relationship in Enum.GetValues<RelationshipKind>())
                if (ImGui.Selectable(RelationshipLabel(relationship), relationshipFilter == (int)relationship)) relationshipFilter = (int)relationship;
            ImGui.EndCombo();
        }
        if (ImGui.BeginCombo(T("Heimatweltfilter", "Home-world filter"), worldFilter.HasValue ? WorldLabel(worldFilter.Value) : T("Alle Heimatwelten", "All home worlds")))
        {
            if (ImGui.Selectable(T("Alle Heimatwelten", "All home worlds"), !worldFilter.HasValue)) worldFilter = null;
            var worlds = ActorSnapshots.Select(a => a.Actor.HomeWorldId).Concat(register.Snapshot.Contacts
                .Where(c => c.Binding is not null).Select(c => c.Binding!.Character.HomeWorldId)).Distinct().Order().ToArray();
            foreach (var world in worlds)
                if (ImGui.Selectable(WorldLabel(world), worldFilter == world)) worldFilter = world;
            ImGui.EndCombo();
        }
        var views = resolver.Search(contactSearch, homeWorldId: worldFilter, favoritesOnly: favoritesOnly,
            relationship: relationshipFilter < 0 ? null : (RelationshipKind)relationshipFilter);
        if (views.Count == 0) Text(T("Keine zulässigen Profile im lokalen Cache. Kontakte bewusst koppeln und Profile empfangen.", "No permitted profiles in the local cache. Explicitly pair with peers and receive profiles."));
        for (var index = 0; index < views.Count; index++)
        {
            var view = views[index];
            if (!view.IsReadable || view.Key is not { } key) continue;
            ImGui.PushID(index);
            if (ImGui.Button(T("Kontakt auswählen", "Select contact"))) SelectContact(key);
            ImGui.SameLine();
            ImGui.TextUnformatted(view.DisplayName);
            Text(view.Connected ? T("Direkt verbunden", "Directly connected") : T("Lokal zwischengespeichert – keine Aussage zur Spielanwesenheit", "Locally cached; does not indicate in-game presence"));
            if (view.ReceivedAt is { } at) Text(T("Zuletzt lokal gespeichert: ", "Last saved locally: ") + at.ToLocalTime().ToString("g"));
            ImGui.PopID();
        }
        if (selectedContact is { } selected)
        {
            var view = resolver.GetProfile(selected);
            if (!view.IsReadable) { selectedContact = null; ClearContactDraft(); }
            else DrawSelectedContact(view, selected);
        }
        DrawMutedManagement();
        if (ImGui.CollapsingHeader(T("Lokaler Begegnungs- und Besuchsverlauf", "Local encounter and visit history")))
            foreach (var entry in resolver.History().Take(100))
            {
                if (!entry.Profile.IsReadable) continue;
                Text(entry.At.ToLocalTime().ToString("g") + " · " + entry.Profile.DisplayName +
                    (entry.Kind == EncounterKind.Seen ? T(" · vor Ort gesehen", " · seen locally") : T(" · Profil geöffnet", " · profile opened")));
            }
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
        if (ImGui.Button(T("Vollständiges Profil öffnen", "Open full profile"))) Open(key);
        Text(T("Notizen, Beziehungen und Favoriten bleiben ausschließlich auf deinem Gerät.", "Notes, relationships and favorites remain only on your device."));
        ImGui.BeginDisabled(register.IsBusy);
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
        ImGui.BeginDisabled(!contactDirty);
        if (ImGui.Button(T("Profilnotiz und Beziehung lokal speichern", "Save profile note and relationship locally")))
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
        ImGui.EndDisabled();
        if (characterNoteKey is { } character)
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
        if (ImGui.Button(T("Profil stummschalten (nur Anzeige)", "Mute profile (presentation only)")))
        {
            var contact = view.Contact ?? new LocalContact { Profile = key };
            Queue(() => register.SetContactAsync(contact with { Muted = true }));
        }
        ImGui.SameLine();
        if (ImGui.Button(T("Peer sperren und Austausch unterbinden", "Block peer and prevent exchange")))
            Queue(() => register.SetPeerBlockedAsync(key.Fingerprint, true));
        DrawBindingUi(view, key);
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
        if (!ImGui.CollapsingHeader(T("Stummschaltungen verwalten", "Manage muted profiles"))) return;
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
        if (!AccessReady) { Text(T("Die Umgebung ist nach Hinweis- und Altersauswahl verfügbar.", "The local environment is available after guidance acknowledgement and age selection.")); return; }
        var known = options.OnlyKnownEnvironment;
        if (ImGui.Checkbox(T("Nur bekannte zulässige RP-Profile", "Only known permitted RP profiles"), ref known)) SetOptions(options with { OnlyKnownEnvironment = known });
        if (ImGui.BeginCombo(T("RP-Statusfilter", "RP status filter"), options.EnvironmentStatus is { } status ? StatusLabel(status) : T("Alle", "All")))
        {
            if (ImGui.Selectable(T("Alle", "All"), options.EnvironmentStatus is null)) SetOptions(options with { EnvironmentStatus = null });
            foreach (var value in Enum.GetValues<RpStatus>())
                if (ImGui.Selectable(StatusLabel(value), options.EnvironmentStatus == value)) SetOptions(options with { EnvironmentStatus = value });
            ImGui.EndCombo();
        }
        if (ImGui.BeginCombo(T("Verfügbarkeitsfilter", "Availability filter"), options.EnvironmentAvailability is { } availability ? AvailabilityLabel(availability) : T("Alle", "All")))
        {
            if (ImGui.Selectable(T("Alle", "All"), options.EnvironmentAvailability is null)) SetOptions(options with { EnvironmentAvailability = null });
            foreach (var value in Enum.GetValues<RpAvailability>())
                if (ImGui.Selectable(AvailabilityLabel(value), options.EnvironmentAvailability == value)) SetOptions(options with { EnvironmentAvailability = value });
            ImGui.EndCombo();
        }
        var hideEmpty = options.HideEmptyEnvironment;
        if (ImGui.Checkbox(T("Profile ohne ausgefüllte RP-Informationen ausblenden", "Hide profiles without written RP information"), ref hideEmpty))
            SetOptions(options with { HideEmptyEnvironment = hideEmpty });
        var filter = new EnvironmentFilter(options.EnvironmentStatus, options.EnvironmentAvailability, options.HideEmptyEnvironment);
        Text(T("Sichtbare Spielcharaktere sind keine automatisch entdeckten FRP1-Clients. Unbekannte Personen werden nicht kontaktiert.", "Visible game characters are not automatically discovered FRP1 clients. Unknown players are never contacted."));
        var index = 0;
        foreach (var actor in ActorSnapshots.OrderBy(a => a.Distance).Take(128))
        {
            var view = resolver.Resolve(actor.Actor);
            if (view.State is ProfilePresentationState.Restricted or ProfilePresentationState.Blocked or ProfilePresentationState.Muted or ProfilePresentationState.Ambiguous ||
                (known && !view.IsReadable) || !MatchesEnvironmentFilter(view, filter)) continue;
            ImGui.PushID(index++);
            ImGui.TextUnformatted(view.IsReadable ? view.DisplayName : actor.Actor.Name);
            if (float.IsFinite(actor.Distance)) { ImGui.SameLine(); ImGui.TextUnformatted(actor.Distance.ToString("0.0") + " m"); }
            if (view.IsReadable && view.Key is { } key)
            {
                if (ImGui.Button(T("Profil öffnen", "Open profile"))) Open(key);
                ImGui.SameLine();
            }
            else Text(T("Kein bekanntes RP-Profil", "No known RP profile"));
            if (ImGui.Button(T("Spielcharakter anvisieren", "Target game character")))
            {
                var current = objects.OfType<Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter>().FirstOrDefault(p =>
                    p.EntityId == actor.Actor.LocalEntityId && p.HomeWorld.RowId == actor.Actor.HomeWorldId && p.Name.TextValue == actor.Actor.Name);
                if (current is not null) targets.Target = current;
            }
            ImGui.PopID();
        }
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
