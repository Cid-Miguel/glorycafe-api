namespace GloryCafe.API.Infrastructure;

public static class ImageUploadValidator
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, byte[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        ["image/png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        ["image/webp"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } }
    };

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    public static (bool ok, string? error, string? extension) Validate(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return (false, "Image file is required.", null);

        if (file.Length > MaxBytes)
            return (false, $"Image exceeds maximum size of {MaxBytes / (1024 * 1024)} MB.", null);

        if (!Extensions.TryGetValue(file.ContentType ?? string.Empty, out var ext))
            return (false, "Image must be JPEG, PNG, or WebP.", null);

        if (!HasValidSignature(file))
            return (false, "Image content does not match its declared type.", null);

        return (true, null, ext);
    }

    private static bool HasValidSignature(IFormFile file)
    {
        if (!Signatures.TryGetValue(file.ContentType, out var validSignatures))
            return false;

        using var stream = file.OpenReadStream();
        var maxSigLen = validSignatures.Max(s => s.Length);
        var buffer = new byte[maxSigLen];
        var read = stream.Read(buffer, 0, maxSigLen);
        if (read < maxSigLen)
            return false;

        return validSignatures.Any(sig => buffer.Take(sig.Length).SequenceEqual(sig));
    }
}
