﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Autofac;
using IGeekFan.FreeKit.Extras.FreeSql;
using LinCms.Blog.Tags;
using LinCms.Cms.Files;
using LinCms.Data;
using LinCms.Data.Options;
using LinCms.Entities.Blog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LinCms.Controllers.v1;

/// <summary>
/// 七牛云上传服务
/// </summary>
[ApiExplorerSettings(GroupName = "v1")]
[Route("v1/qiniu")]
[ApiController]
public class QiniuController(IWebHostEnvironment hostingEnv, IOptionsSnapshot<FileStorageOption> optionsSnapshot,
        IComponentContext componentContext, ITagService tagService, IAuditBaseRepository<Tag> tagAuditBaseRepository)
    : ControllerBase
{
    private readonly IFileService _fileService = componentContext.ResolveNamed<IFileService>(optionsSnapshot.Value.ServiceName);

    /// <summary>
    /// 将掘金中的取所有标签存到七牛云上，基本信息存入数据库
    /// 从百度云下载后，放到wwwwroot中，swagger上执行下此方法，需要等很久，提取其中的tags，将Icon上传到七牛云上，tag信息存到数据库中。
    /// 链接：https://pan.baidu.com/s/1H3VkNOxybo2Vr-wTCrdIvQ?pwd=qblv 提取码：qblv
    /// </summary>
    /// <returns></returns>
    [HttpPost("tag")]
    public async Task<UnifyResponseDto> UploadTagByJson()
    {
        string tagPath = Path.Combine(hostingEnv.WebRootPath, "json-tag.json");
        string text = System.IO.File.ReadAllText(tagPath);

        JObject? json = JsonConvert.DeserializeObject<JObject>(text);
        if (json == null) return UnifyResponseDto.Success();
        var jsonData = json["data"];
        if (jsonData == null) return UnifyResponseDto.Success();
        foreach (var tag in jsonData)
        {

            string? tagName = tag["tag"]["tag_name"]?.ToString();
            bool valid = await tagAuditBaseRepository.Where(r => r.TagName == tagName).AnyAsync();

            if (valid)
            {
                Console.WriteLine($"{tagName}已存在，不需要生成");
                continue;
            }

            try
            {

                FileDto fileDto = await UploadToQiniu(tag["tag"]["icon"].ToString());

                var tagEntity = new CreateUpdateTagDto()
                {
                    TagName = tagName,
                    Alias = tag["tag"]["tag_alias"].ToString(),
                    Status = true,
                    Thumbnail = fileDto.Path
                };
                await tagService.CreateAsync(tagEntity);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        return UnifyResponseDto.Success();
    }

    private async Task<FileDto> UploadToQiniu(string remoteImagePath)
    {
        IFormFile file = GetUrlFormFile(remoteImagePath);
        return await _fileService.UploadAsync(file);
    }


    /// <summary>
    /// 获取远程服务器内容，并转换成流
    /// </summary>
    /// <param name="path">https://p1-jj.byteimg.com/tos-cn-i-t2oaga2asx/leancloud-assets/bac28828a49181c34110.png</param>
    /// <returns></returns>
    private IFormFile GetUrlFormFile(string path)
    {
        //png ~tplv-t2oaga2asx-image.image
        //jpg ~tplv-t2oaga2asx-no-mark:200:200:0:0.awebp

        //       ~tplv-k3u1fbpfcp-watermark.image?
        //       ~tplv-k3u1fbpfcp-no-mark:200:200:0:0.awebp

        //string suffix = Path.GetExtension(path);//jpg
        string suffix = ".webp";

        //去掉水印
        path = path.Replace("watermark.image", "no-mark:200:200:0:0.awebp");
        string lastsuffix = path.Split('~')[1];
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(path);
        Console.WriteLine(path);
        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
        Stream responseStream = response.GetResponseStream();
        List<byte> btlst = new List<byte>();
        int b = responseStream.ReadByte();
        while (b > -1)
        {
            btlst.Add((byte)b);
            b = responseStream.ReadByte();
        }
        byte[] bts = btlst.ToArray();
        var ms = new MemoryStream();
        ms.Seek(0, SeekOrigin.Begin);
        ms.Write(bts);
        path = path.Replace("~" + lastsuffix, "");
        string fileName = path.Substring(path.LastIndexOf("/") + 1, path.Length - path.LastIndexOf("/") - 1);
        if (Path.GetExtension(path).IsNullOrEmpty())
        {
            fileName = fileName + suffix;
        }
        return new FormFile(ms, 0, bts.Length, "file", fileName);
    }
}