﻿using System.Threading.Tasks;
using IGeekFan.FreeKit.Extras.Dto;
using IGeekFan.FreeKit.Extras.FreeSql;
using IGeekFan.FreeKit.Extras.Security;
using LinCms.Blog.UserSubscribes;
using LinCms.Entities.Blog;
using LinCms.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinCms.Controllers.Blog;

/// <summary>
/// 用户订阅
/// </summary>
[ApiExplorerSettings(GroupName = "blog")]
[Area("blog")]
[Route("api/blog/subscribe")]
[ApiController]
[Authorize]
public class UserSubscribeController(IAuditBaseRepository<UserSubscribe> userSubscribeRepository,
        ICurrentUser currentUser,
        IUserSubscribeService userSubscribeService,
        IAuditBaseRepository<UserTag> userTagRepository)
    : ControllerBase
{
    /// <summary>
    /// 判断当前登录的用户是否关注了beSubscribeUserId
    /// </summary>
    /// <param name="subscribeUserId"></param>
    /// <returns></returns>
    [HttpGet("{subscribeUserId}")]
    [AllowAnonymous]
    public bool Get(long subscribeUserId)
    {
        if (currentUser.FindUserId() == null) return false;
        return userSubscribeRepository.Select.Any(r => r.SubscribeUserId == subscribeUserId && r.CreateUserId == currentUser.FindUserId());
    }

    /// <summary>
    /// 取消关注用户
    /// </summary>
    /// <param name="subscribeUserId"></param>
    [HttpDelete("{subscribeUserId}")]
    public Task DeleteAsync(long subscribeUserId)
    {
        return userSubscribeService.DeleteAsync(subscribeUserId);
    }

    /// <summary>
    /// 关注用户
    /// </summary>
    /// <param name="subscribeUserId"></param>
    [HttpPost("{subscribeUserId}")]
    public Task CreateAsync(long subscribeUserId)
    {
        return userSubscribeService.CreateAsync(subscribeUserId);
    }

    /// <summary>
    /// 得到某个用户的关注
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [AllowAnonymous]
    public PagedResultDto<UserSubscribeDto> GetUserSubscribeeeList([FromQuery] UserSubscribeSearchDto searchDto)
    {
        return userSubscribeService.GetUserSubscribeeeList(searchDto);
    }

    /// <summary>
    /// 得到某个用户的粉丝
    /// </summary>
    /// <returns></returns>
    [HttpGet("fans")]
    [AllowAnonymous]
    public PagedResultDto<UserSubscribeDto> GetUserFansList([FromQuery] UserSubscribeSearchDto searchDto)
    {
        return userSubscribeService.GetUserFansList(searchDto);
    }

    /// <summary>
    /// 得到某个用户的关注了、关注者、标签总数
    /// </summary>
    /// <param name="userId"></param>
    [HttpGet("user/{userId}")]
    [AllowAnonymous]
    public SubscribeCountDto GetUserSubscribeInfo(long userId)
    {
        long subscribeCount = userSubscribeRepository.Select
            .Where(r => r.CreateUserId == userId)
            .Count();

        long fansCount = userSubscribeRepository.Select
            .Where(r => r.SubscribeUserId == userId)
            .Count();

        long tagCount = userTagRepository.Select.Include(r => r.Tag)
            .Where(r => r.CreateUserId == userId).Count();

        return new SubscribeCountDto
        {
            SubscribeCount = subscribeCount,
            FansCount = fansCount,
            TagCount = tagCount
        };
    }
}