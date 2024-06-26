﻿using IGeekFan.FreeKit.Extras.FreeSql;
using IGeekFan.FreeKit.Extras.Security;
using LinCms.Common;
using LinCms.Data;
using LinCms.Entities;
using LinCms.Security;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;

namespace LinCms.Cms.Permissions;

public class PermissionService(IAuditBaseRepository<LinPermission, long> permissionRepository,
        IAuditBaseRepository<LinGroupPermission, long> groupPermissionRepository)
    : ApplicationService, IPermissionService
{

    public async Task<List<PermissionTreeNode>> GetPermissionTreeNodes()
    {
        var permissionList = await permissionRepository.Select.OrderBy(r => r.SortCode).ToListAsync();
        var nodes = Mapper.Map<List<LinPermission>, List<PermissionTreeNode>>(permissionList);
        return new TreeBuilder().BuildPermissionTree(nodes);
    }

    /// <summary>
    /// 检查当前登录的用户的分组权限
    /// </summary>
    /// <param name="permission">权限名</param>
    /// <returns></returns>
    public async Task<bool> CheckPermissionAsync(string permission)
    {
        //默认Admin角色拥有所有权限
        if (CurrentUser.IsInGroup(LinConsts.Group.Admin)) return true;
        long[] groups = CurrentUser.FindGroupIds().Select(long.Parse).ToArray();

        LinPermission linPermission = await permissionRepository.Where(r => r.PermissionType == PermissionType.Permission && r.Name == permission).FirstAsync();

        if (linPermission == null || groups.Length == 0) return false;

        bool existPermission = await groupPermissionRepository.Select
            .AnyAsync(r => groups.Contains(r.GroupId) && r.PermissionId == linPermission.Id);

        return existPermission;
    }


    public Task DeletePermissionsAsync(RemovePermissionDto permissionDto)
    {
        return groupPermissionRepository.DeleteAsync(r =>
            permissionDto.PermissionIds.Contains(r.PermissionId) && r.GroupId == permissionDto.GroupId);
    }

    public Task DispatchPermissions(DispatchPermissionsDto permissionDto, List<PermissionDefinition> permissionDefinitions)
    {
        List<LinGroupPermission> linPermissions = new();
        permissionDto.PermissionIds.ForEach(permissionId =>
        {
            linPermissions.Add(new LinGroupPermission(permissionDto.GroupId, permissionId));
        });
        return groupPermissionRepository.InsertAsync(linPermissions);
    }

    public async Task<List<LinPermission>> GetPermissionByGroupIds(List<long> groupIds)
    {
        List<long> permissionIds = groupPermissionRepository
            .Where(a => groupIds.Contains(a.GroupId))
            .ToList(r => r.PermissionId);

        List<LinPermission> listPermissions = await permissionRepository
            .Where(a => permissionIds.Contains(a.Id))
            .ToListAsync();

        return listPermissions;

    }

    public List<IDictionary<string, object>> StructuringPermissions(List<LinPermission> permissions)
    {
        var groupPermissions = permissions.Where(r => r.PermissionType == PermissionType.Folder).GroupBy(r => r.Name).Select(r => new
        {
            r.Key,
            Children = r.Select(u => u.Name).ToList()
        }).ToList();

        List<IDictionary<string, object>> list = new();

        foreach (var groupPermission in groupPermissions)
        {
            IDictionary<string, object> moduleExpandoObject = new ExpandoObject();
            List<IDictionary<string, object>> perExpandList = new();
            groupPermission.Children.ForEach(permission =>
            {
                IDictionary<string, object> perExpandObject = new ExpandoObject();
                perExpandObject["module"] = groupPermission.Key;
                perExpandObject["permission"] = permission;
                perExpandList.Add(perExpandObject);
            });

            moduleExpandoObject[groupPermission.Key] = perExpandList;
            list.Add(moduleExpandoObject);
        }

        return list;
    }

    public async Task UpdateAsync(int id, PermissioCreateUpdateDto createUpdateDto)
    {
        LinPermission permission = await permissionRepository.GetAsync(id);
        Mapper.Map(createUpdateDto, permission);
        await permissionRepository.UpdateAsync(permission);
    }

    public async Task CreateAsync(PermissioCreateUpdateDto createUpdateDto)
    {
        await permissionRepository.UpdateAsync(Mapper.Map<LinPermission>(createUpdateDto));
    }

    public  async Task DeleteAsync(int id)
    {
        await permissionRepository.DeleteAsync(id);
    }

    public Task<LinPermission> GetAsync(string permissionName)
    {
        return permissionRepository.Where(r => r.Name == permissionName).FirstAsync();
    }
}