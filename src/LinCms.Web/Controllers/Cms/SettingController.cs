﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IGeekFan.FreeKit.Extras.Dto;
using IGeekFan.FreeKit.Extras.Security;
using LinCms.Aop.Filter;
using LinCms.Cms.Settings;
using LinCms.Data;
using LinCms.IRepositories;
using LinCms.Security;
using Microsoft.AspNetCore.Mvc;

namespace LinCms.Controllers.Cms;

/// <summary>
/// 设置
/// </summary>
[ApiExplorerSettings(GroupName = "cms")]
[Route("cms/settings")]
[ApiController]
public class SettingController(ISettingService settingService, 
        ICurrentUser currentUser,
        ISettingRepository settingRepository)
    : ControllerBase
{
    [LinCmsAuthorize("得到所有设置", "设置")]
    [HttpGet]
    public async Task<PagedResultDto<SettingDto>> GetPagedListAsync([FromQuery] PageDto pageDto)
    {
        return await settingService.GetPagedListAsync(pageDto);
    }

    [LinCmsAuthorize("删除设置", "设置")]
    [HttpDelete("{id}")]
    public async Task Delete(Guid id)
    {
        await settingRepository.DeleteAsync(id);
    }

    [LinCmsAuthorize("新增设置", "设置")]
    [HttpPost]
    public async Task CreateAsync([FromBody] CreateUpdateSettingDto createSetting)
    {
        await settingService.CreateAsync(createSetting);
    }

    [LinCmsAuthorize("修改设置", "设置")]
    [HttpPut("{id}")]
    public async Task UpdateAsync(Guid id, [FromBody] CreateUpdateSettingDto updateSettingDto)
    {
        await settingService.UpdateAsync(id, updateSettingDto);
    }

    [HttpGet("{id}")]
    public Task<SettingDto> Get(Guid id)
    {
        return settingService.GetAsync(id);
    }

    [HttpPost("set-values")]
    public async Task SetSettingValues(IDictionary<string, string> settingValues)
    {
        foreach (var kValue in settingValues)
        {
            string key = kValue.Key;
            CreateUpdateSettingDto createSetting = new CreateUpdateSettingDto
            {
                Value = kValue.Value,
                ProviderName = "U",
                ProviderKey = currentUser.FindUserId().ToString(),
                Name = key
            };
            await settingService.SetAsync(createSetting);
        }
    }

    [HttpGet("key/{key}")]
    public async Task<string> GetSettingByKey(string key)
    {
        string providerName = "U";
        string? providerKey = currentUser.FindUserId().ToString();
        return await settingService.GetOrNullAsync(key, providerName, providerKey);
    }

    [HttpGet("keys")]
    public async Task<IDictionary<string, string>> GetSettingKeys([FromQuery(Name = "keys[]")] List<string> keys)
    {
        string providerName = "U";
        string? providerKey = currentUser.FindUserId().ToString();
        IDictionary<string, string> values = new Dictionary<string, string>();

        foreach (var key in keys)
        {
            string value = await settingService.GetOrNullAsync(key, providerName, providerKey);
            values.Add(key, value);
        }
        return values;
    }
}