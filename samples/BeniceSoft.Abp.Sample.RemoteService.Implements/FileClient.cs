using BeniceSoft.Abp.Sample.RemoteService.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.Content;
using Wecharmer.FileCenter.Application.Contracts.Dtos.ImportExport;
using Wecharmer.FileCenter.Interfaces;

namespace BeniceSoft.Abp.Sample.RemoteService.Implements;

public class FileClient: IFileClient
{
    private readonly ILogger<FileClient> _logger;
    private readonly IImportExportAppService _importExportAppService;
    private readonly IFileAppService _fileAppService;

    public FileClient(
        ILogger<FileClient> logger,
        IImportExportAppService importExportAppService,
        IFileAppService fileAppService)
    {
        _logger = logger;
        _importExportAppService = importExportAppService;
        _fileAppService = fileAppService;
    }

    public async Task ReportAsync(long taskId, bool success, long? resultFileId = null, string? message = null)
    {
        try
        {
            await _importExportAppService.ReportAsync(new ReportImportExportTaskReq
            {
                TaskId = taskId,
                Success = success,
                ResultFileId = resultFileId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上报文件状态异常");
        }
    }

    public async Task<long> UploadAsync(
        Stream content,
        string fileName,
        string contentType = "application/octet-stream",
        string scene = "")
    {
        Stream uploadStream = content;
        MemoryStream? ownedStream = null;
        try
        {
            if (!content.CanSeek)
            {
                ownedStream = new MemoryStream();
                await content.CopyToAsync(ownedStream);
                ownedStream.Position = 0;
                uploadStream = ownedStream;
            }
            else
            {
                content.Position = 0;
            }

            var file = await _fileAppService.UploadAsync(
                new RemoteStreamContent(uploadStream, fileName, contentType),
                scene);
            return file.Id;
        }
        finally
        {
            if (ownedStream is not null)
            {
                await ownedStream.DisposeAsync();
            }
        }
    }
}
