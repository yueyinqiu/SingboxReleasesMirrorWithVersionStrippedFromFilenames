#:package Octokit@14.0.0

using Octokit;

var token = Environment.GetEnvironmentVariable("TOKEN");

var client = new GitHubClient(new ProductHeaderValue("SingboxReleasesMirrorWithVersionStrippedFromFilenames"))
{
    Credentials = new Credentials(token)
};

var origin = await client.Repository.Release.GetLatest("SagerNet", "sing-box");
Console.WriteLine($"ORIGIN: {origin.TagName}");

var mirror = await client.Repository.Release.GetLatest("yueyinqiu", "SingboxReleasesMirrorWithVersionStrippedFromFilenames");
Console.WriteLine($"MIRROR: {mirror.TagName}");

if (origin.TagName == mirror.TagName)
{
    Console.WriteLine("It is up to date. Finished.");
    return;
}

Console.WriteLine($"Drafting release...");
var release = await client.Repository.Release.Create("yueyinqiu", "SingboxReleasesMirrorWithVersionStrippedFromFilenames",
    new NewRelease(origin.TagName)
    {
        Draft = true
    });

var version = origin.TagName[1..];
using var httpClient = new HttpClient();

await Parallel.ForEachAsync(origin.Assets, async (asset, cancellationToken) =>
{
    Console.WriteLine($"Converting {asset.Name}...");
    using var stream = await httpClient.GetStreamAsync(asset.BrowserDownloadUrl, cancellationToken);

    using var memoryStream = new MemoryStream();
    await stream.CopyToAsync(memoryStream, cancellationToken);
    memoryStream.Position = 0;

    await client.Repository.Release.UploadAsset(
        release,
        new ReleaseAssetUpload(
            asset.Name.Replace($"{version}-", "").Replace($"{version}_", ""),
            asset.ContentType,
            memoryStream,
            null
        ),
        cancellationToken);
});

Console.WriteLine($"Publishing release...");
await client.Repository.Release.Edit("yueyinqiu", "SingboxReleasesMirrorWithVersionStrippedFromFilenames",
    release.Id, new ReleaseUpdate() { Draft = false });
