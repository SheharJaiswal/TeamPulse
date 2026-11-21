namespace Habit.API.Security;

public sealed class HabitJwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience) ||
            string.IsNullOrWhiteSpace(SigningKey) || System.Text.Encoding.UTF8.GetByteCount(SigningKey) < 32)
            throw new System.InvalidOperationException(
                "Habit JWT configuration requires Issuer, Audience and a SigningKey of at least 32 UTF-8 bytes.");
    }
}
