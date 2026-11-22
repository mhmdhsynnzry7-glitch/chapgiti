namespace ChapGiti.Application.Features;

public sealed class PublicAssetService(
    IAdminContentRepository repository,
    IPrivateFileStore files)
{
    public async Task<UseCaseResult> Get(string id)
    {
        var asset = await repository.FindAsset(id) ?? throw new UseCaseException(ErrorKind.NotFound, "تصویر پیدا نشد.");
        return UseCaseResult.File(await files.ReadImage(id), asset.Str("mime"));
    }
}
