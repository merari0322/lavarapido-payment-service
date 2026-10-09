namespace PaymentService.Api.Configuration;

/// <summary>
/// Lee lavarapido-infra/.env (o un .env junto al servicio) y pone sus valores como variables de
/// entorno, sin pisar las que ya existan. Es lo mismo que hacen los servicios Java con
/// spring.config.import.
/// </summary>
public static class EnvFile
{
    public static void Load()
    {
        foreach (var path in Candidates())
        {
            if (!File.Exists(path)) continue;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim().Trim('"');
                if (Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, value);
            }
            return;
        }
    }

    // sube desde la carpeta actual buscando lavarapido-infra/.env
    private static IEnumerable<string> Candidates()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            yield return Path.Combine(dir.FullName, "lavarapido-infra", ".env");
            yield return Path.Combine(dir.FullName, "..", "lavarapido-infra", ".env");
        }
        yield return Path.Combine(Directory.GetCurrentDirectory(), ".env");
    }
}
