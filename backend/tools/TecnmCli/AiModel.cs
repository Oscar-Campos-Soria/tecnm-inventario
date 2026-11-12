using System.Security.Cryptography;

namespace TecnmCli;

/// <summary>
/// Modelo de IA local de la carga masiva: Qwen3.5-2B (texto e imágenes) + su módulo de visión (mmproj).
/// Fijado por nombre, tamaño y SHA256: cambiar de modelo es cambiar esta lista y los -m/--mmproj
/// de database/docker-compose.prod.yml.
/// </summary>
internal static class AiModel
{
    public sealed record ModelFile(string FileName, long Size, string Sha256)
    {
        public string Url => "https://huggingface.co/unsloth/Qwen3.5-2B-GGUF/resolve/main/" + FileName;
    }

    public static readonly ModelFile[] Files =
    [
        new("Qwen3.5-2B-Q4_K_M.gguf", 1_280_835_840, "aaf42c8b7c3cab2bf3d69c355048d4a0ee9973d48f16c731c0520ee914699223"),
        new("mmproj-F16.gguf", 668_227_264, "7035e9cb8d7c6a9681d07eef9a364783e86ea4cd73faab2eabb4f43a101830c7"),
    ];

    public const string Profile = "ia";

    /// <summary>¿El archivo es exactamente el esperado? (tamaño primero: evita hashear 1 GB de basura).</summary>
    public static bool Matches(string path, long size, string sha256)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != size) return false;
        using var stream = info.OpenRead();
        return Convert.ToHexStringLower(SHA256.HashData(stream)) == sha256;
    }

    public static bool Matches(string path, ModelFile file) => Matches(path, file.Size, file.Sha256);

    /// <summary>.gguf de la carpeta que ya no están en la lista (modelos anteriores).</summary>
    public static IEnumerable<string> Stale(IEnumerable<string> fileNames) =>
        fileNames.Where(f => f.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) && Files.All(m => m.FileName != f));

    /// <summary>¿.env.prod tiene encendido el perfil de IA?</summary>
    public static bool Enabled(IReadOnlyDictionary<string, string> env) =>
        env.TryGetValue("COMPOSE_PROFILES", out var p) && p.Split(',').Select(s => s.Trim()).Contains(Profile);
}
