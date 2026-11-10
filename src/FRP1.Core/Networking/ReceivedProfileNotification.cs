namespace FRP1.Core.Networking;

public sealed record ReceivedProfileNotification(string Fingerprint, Guid ProfileId, long Revision, DateTimeOffset ReceivedAtUtc);
