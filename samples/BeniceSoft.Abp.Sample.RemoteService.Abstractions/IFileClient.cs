using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Sample.RemoteService.Abstractions
{
    public interface IFileClient : IScopedDependency
    {
        /// <summary>
        /// 回填导入导出任务结果
        /// </summary>
        Task ReportAsync(long taskId, bool success, long? resultFileId = null, string? message = null);

        /// <summary>
        /// 上传文件，返回文件对象 Id
        /// </summary>
        Task<long> UploadAsync(Stream content, string fileName, string contentType = "application/octet-stream", string scene = "");
    }
}
