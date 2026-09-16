namespace MonitorSuhu.Core;

public static class UpdateDownload
{
    public static async Task ToFileAsync(
        string url,
        string destPath,
        string userAgent,
        long expectedSize,
        string platform,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        var name = Path.GetFileName(destPath);
        if (!ReleaseAssets.IsTrusted(url, name, platform))
            throw new InvalidOperationException("Update file is not from GitHub Releases.");
        if (expectedSize > ReleaseAssets.MaxBytes)
            throw new InvalidOperationException("Update is larger than expected.");

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? expectedSize;
        if (total > ReleaseAssets.MaxBytes)
            throw new InvalidOperationException("Update is larger than expected.");

        var part = destPath + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(destPath) ?? Path.GetTempPath());
        await using (var src = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var dst = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            var buffer = new byte[81920];
            long read = 0;
            while (true)
            {
                var n = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (n == 0) break;
                read += n;
                if (read > ReleaseAssets.MaxBytes)
                    throw new InvalidOperationException("Update is larger than expected.");
                await dst.WriteAsync(buffer.AsMemory(0, n), cancellationToken);
                if (total > 0)
                    progress?.Report(Math.Clamp((double)read / total, 0, 1));
            }

            if (expectedSize > 0 && read != expectedSize)
                throw new InvalidOperationException("Download size did not match GitHub.");
        }

        File.Move(part, destPath, overwrite: true);
        progress?.Report(1);
    }
}
