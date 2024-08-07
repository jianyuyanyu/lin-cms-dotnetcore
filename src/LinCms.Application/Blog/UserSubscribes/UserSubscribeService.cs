﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNetCore.CAP;
using IGeekFan.FreeKit.Extras.Dto;
using IGeekFan.FreeKit.Extras.FreeSql;
using LinCms.Blog.Notifications;
using LinCms.Cms.Users;
using LinCms.Entities;
using LinCms.Entities.Blog;
using LinCms.Exceptions;
using LinCms.Extensions;
using LinCms.IRepositories;
using LinCms.Security;

namespace LinCms.Blog.UserSubscribes;

/// <summary>
/// 用户关注（订阅）服务接口
/// </summary>
public class UserSubscribeService(IAuditBaseRepository<UserSubscribe, Guid> userSubscribeRepository,
        IUserRepository userRepository, ICapPublisher capBus, IFileRepository fileRepository)
    : ApplicationService, IUserSubscribeService
{
    public async Task<List<long>> GetSubscribeUserIdAsync(long userId)
    {
        List<long> subscribeUserIds = await userSubscribeRepository
            .Select.Where(r => r.CreateUserId == userId)
            .ToListAsync(r => r.SubscribeUserId);
        return subscribeUserIds;
    }

    public PagedResultDto<UserSubscribeDto> GetUserSubscribeeeList(UserSubscribeSearchDto searchDto)
    {
        List<UserSubscribeDto> userSubscribes = userSubscribeRepository.Select.Include(r => r.SubscribeUser)
            .Where(r => r.CreateUserId == searchDto.UserId)
            .OrderByDescending(r => r.CreateTime)
            .ToPager(searchDto, out long count)
            .ToList(r => new UserSubscribeDto
            {
                CreateUserId = r.CreateUserId.Value,
                SubscribeUserId = r.SubscribeUserId,
                Subscribeer = new OpenUserDto()
                {
                    Id = r.SubscribeUser.Id,
                    Introduction = r.SubscribeUser.Introduction,
                    Nickname = !r.SubscribeUser.IsDeleted ? r.SubscribeUser.Nickname : "该用户已注销",
                    Avatar = r.SubscribeUser.Avatar,
                    Username = r.SubscribeUser.Username,
                },
                IsSubscribeed = userSubscribeRepository.Select.Any(u => u.CreateUserId ==  CurrentUser.FindUserId() && u.SubscribeUserId == r.SubscribeUserId)
            });

        userSubscribes.ForEach(r => { r.Subscribeer.Avatar = fileRepository.GetFileUrl(r.Subscribeer.Avatar); });

        return new PagedResultDto<UserSubscribeDto>(userSubscribes, count);
    }

    public PagedResultDto<UserSubscribeDto> GetUserFansList(UserSubscribeSearchDto searchDto)
    {
        List<UserSubscribeDto> userSubscribes = userSubscribeRepository.Select.Include(r => r.LinUser)
            .Where(r => r.SubscribeUserId == searchDto.UserId)
            .OrderByDescending(r => r.CreateTime)
            .ToPager(searchDto, out long count)
            .ToList(r => new UserSubscribeDto
            {
                CreateUserId = r.CreateUserId.Value,
                SubscribeUserId = r.SubscribeUserId,
                Subscribeer = new OpenUserDto()
                {
                    Id = r.LinUser.Id,
                    Introduction = r.LinUser.Introduction,
                    Nickname = !r.LinUser.IsDeleted ? r.LinUser.Nickname : "该用户已注销",
                    Avatar = r.LinUser.Avatar,
                    Username = r.LinUser.Username,
                },
                //当前登录的用户是否关注了这个粉丝
                IsSubscribeed = userSubscribeRepository.Select.Any(
                    u => u.CreateUserId ==  CurrentUser.FindUserId() && u.SubscribeUserId == r.CreateUserId)
            });

        userSubscribes.ForEach(r => { r.Subscribeer.Avatar = fileRepository.GetFileUrl(r.Subscribeer.Avatar); });

        return new PagedResultDto<UserSubscribeDto>(userSubscribes, count);
    }

    [Transactional]
    public async Task CreateAsync(long subscribeUserId)
    {
        if (subscribeUserId ==  CurrentUser.FindUserId())
        {
            throw new LinCmsException("您无法关注自己");
        }

        LinUser linUser = userRepository.Select.Where(r => r.Id == subscribeUserId).ToOne();
        if (linUser == null)
        {
            throw new LinCmsException("该用户不存在");
        }

        if (!linUser.IsActive())
        {
            throw new LinCmsException("该用户已被拉黑");
        }

        bool any = userSubscribeRepository.Select.Any(r =>
            r.CreateUserId ==  CurrentUser.FindUserId() && r.SubscribeUserId == subscribeUserId);
        if (any)
        {
            throw new LinCmsException("您已关注该用户");
        }

        using ICapTransaction capTransaction = UnitOfWorkManager.Current.BeginTransaction(capBus, false);

        UserSubscribe userSubscribe = new() { SubscribeUserId = subscribeUserId };
        await userSubscribeRepository.InsertAsync(userSubscribe);

        await capBus.PublishAsync(CreateNotificationDto.CreateOrCancelAsync, new CreateNotificationDto()
        {
            NotificationType = NotificationType.UserLikeUser,
            NotificationRespUserId = subscribeUserId,
            UserInfoId =  CurrentUser.FindUserId() ?? 0,
            CreateTime = DateTime.Now,
        });

        capTransaction.Commit(UnitOfWorkManager.Current);
    }

    [Transactional]
    public async Task DeleteAsync(long subscribeUserId)
    {
        bool any = await userSubscribeRepository.Select.AnyAsync(r =>
            r.CreateUserId ==  CurrentUser.FindUserId() && r.SubscribeUserId == subscribeUserId);
        if (!any)
        {
            throw new LinCmsException("已取消关注");
        }


        using ICapTransaction capTransaction = UnitOfWorkManager.Current.BeginTransaction(capBus, false);

        await userSubscribeRepository.DeleteAsync(r => r.SubscribeUserId == subscribeUserId && r.CreateUserId ==  CurrentUser.FindUserId());

        await capBus.PublishAsync(CreateNotificationDto.CreateOrCancelAsync, new CreateNotificationDto()
        {
            NotificationType = NotificationType.UserLikeUser,
            NotificationRespUserId = subscribeUserId,
            UserInfoId =  CurrentUser.FindUserId() ?? 0,
            CreateTime = DateTime.Now,
            IsCancel = true
        });

        capTransaction.Commit(UnitOfWorkManager.Current);

    }

}