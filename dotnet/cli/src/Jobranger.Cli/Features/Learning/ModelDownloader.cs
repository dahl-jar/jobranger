using System.Security.Cryptography;

namespace Jobranger.Cli.Features.Learning;

internal sealed class ModelDownloader(
    HttpClient http, string modelDir, IReadOnlyList<ModelFile> files, TimeSpan idleTimeout, TimeProvider time) {
    private const int bufferSize = 1 << 20;
    private const string partSuffix = ".part";

    private const int unbuffered = 0;

    private static readonly TimeSpan defaultIdleTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan stalePartAge = TimeSpan.FromMinutes(10);

    public ModelDownloader(HttpClient http, string modelDir, IReadOnlyList<ModelFile> files)
        : this(http, modelDir, files, defaultIdleTimeout, TimeProvider.System) {
    }

    public async Task EnsureAsync(CancellationToken cancellationToken) {
        Directory.CreateDirectory(modelDir);
        foreach (ModelFile file in files) {
            DeleteStaleParts(file);
            string target = Path.Combine(modelDir, file.Name);
            if (!File.Exists(target)) {
                await DownloadAsync(file, target, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void DeleteStaleParts(ModelFile file) {
        DateTime cutoff = DateTime.UtcNow - stalePartAge;
        foreach (string part in Directory.EnumerateFiles(modelDir, file.Name + ".*" + partSuffix)) {
            if (File.GetLastWriteTimeUtc(part) < cutoff) {
                File.Delete(part);
            }
        }
    }

    private async Task DownloadAsync(ModelFile file, string target, CancellationToken cancellationToken) {
        string part = $"{target}.{Guid.NewGuid():N}{partSuffix}";
        try {
            file.CheckHash(await SaveAsync(file, part, cancellationToken).ConfigureAwait(false));
            File.Move(part, target, overwrite: true);
        } finally {
            File.Delete(part);
        }
    }

    private async Task<string> SaveAsync(ModelFile file, string part, CancellationToken cancellationToken) {
        using HttpResponseMessage response = await http
            .GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (body.ConfigureAwait(false)) {
            FileStream output = new(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, unbuffered);
            await using (output.ConfigureAwait(false)) {
                using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                using CancellationTokenSource idle = new(idleTimeout, time);
                using CancellationTokenSource reading =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, idle.Token);
                byte[] buffer = new byte[bufferSize];
                long total = 0;
                int read;
                while ((read = await ReadAsync(file, body, buffer, idle, reading.Token, cancellationToken)
                    .ConfigureAwait(false)) > 0) {
                    total += read;
                    if (total > file.Size) {
                        throw new InvalidDataException($"{file.Name} is larger than the pinned {file.Size} bytes");
                    }
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
                return Convert.ToHexStringLower(hash.GetHashAndReset());
            }
        }
    }

    private async Task<int> ReadAsync(
        ModelFile file,
        Stream body,
        byte[] buffer,
        CancellationTokenSource idle,
        CancellationToken reading,
        CancellationToken cancellationToken) {
        idle.CancelAfter(idleTimeout);
        try {
            return await body.ReadAsync(buffer, reading).ConfigureAwait(false);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw new ModelDownloadException(file.Name, idleTimeout);
        }
    }
}
