namespace FRP1.Plugin.Utility;

public enum RpTextTemplate { Speech, Emote }

// Session-local text preparation. No game commands or public profile fields are produced.
public sealed record RpTextDraft(string Name = "", string Text = "", RpTextTemplate Template = RpTextTemplate.Speech)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Template) || Name is null || Name.Length > 80 || Name.Any(char.IsControl) ||
            Text is null || Text.Length > 2048 || Text.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t'))
            throw new InvalidDataException("Invalid local text draft.");
    }

    public string Preview()
    {
        Validate();
        if (string.IsNullOrWhiteSpace(Text)) return "";
        var name = string.IsNullOrWhiteSpace(Name) ? "…" : Name.Trim();
        return Template == RpTextTemplate.Speech ? $"{name}: „{Text}“" : $"* {name} {Text} *";
    }
}
