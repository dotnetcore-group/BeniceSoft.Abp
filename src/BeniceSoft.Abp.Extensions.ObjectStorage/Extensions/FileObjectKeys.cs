namespace BeniceSoft.Abp.Extensions.ObjectStorage;

/// <summary>
/// 对象存储 ObjectKey 统一生成：{scene}/{yyyy}-{MM}-{dd}/{guid}{ext}
/// </summary>
public static class FileObjectKeys
{
    public static string Build(string? scene, string fileName, DateTime dateTime)
    {
        var safeScene = FileScenes.Normalize(scene);
        var ext = Path.GetExtension(fileName);
        return $"{safeScene}/{dateTime:yyyy-MM-dd}/{Guid.NewGuid():N}{ext}";
    }

    /// <summary>
    /// 缩略图对象键：在原 ObjectKey 去扩展名后追加 .thumb.jpg
    /// </summary>
    public static string BuildThumbObjectKey(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new ArgumentException("objectKey 不能为空", nameof(objectKey));
        }

        var ext = Path.GetExtension(objectKey);
        var withoutExt = string.IsNullOrEmpty(ext) ? objectKey : objectKey[..^ext.Length];
        return $"{withoutExt}.thumb.jpg";
    }
}
