﻿using Autofac;
using DotNetCore.Security;
using IGeekFan.FreeKit.Extras.Security;
using LinCms.Cms.Users;
using LinCms.Data;
using LinCms.Data.Enums;
using LinCms.Domain;
using LinCms.Entities;
using LinCms.Exceptions;
using LinCms.Extensions;
using LinCms.IRepositories;
using LinCms.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;

namespace LinCms.Controllers.Cms;

/// <summary>
/// 第三方登录
/// </summary>
[ApiExplorerSettings(GroupName = "cms")]
[Route("cms/oauth2")]
[ApiController]
public class Oauth2Controller(IUserIdentityService userCommunityService, 
        ILogger<Oauth2Controller> logger,
        IUserRepository userRepository,
        IJwtService jsonWebTokenService, 
        IComponentContext componentContext,
        ITokenManager tokenManager)
    : ControllerBase
{
    private const string LoginProviderKey = "LoginProvider";

    /// <summary>
    /// 授权成功后自动回调的地址
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="redirectUrl">授权成功后的跳转地址</param>
    /// <returns></returns>
    [HttpGet("signin-callback")]
    public async Task<IActionResult> Home(string provider, string redirectUrl = "")
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return BadRequest();
        }

        if (!await HttpContext.IsProviderSupportedAsync(provider))
        {
            return BadRequest();
        }

        AuthenticateResult authenticateResult = await HttpContext.AuthenticateAsync(provider);
        if (!authenticateResult.Succeeded) return Redirect(redirectUrl);
        var openIdClaim = authenticateResult.Principal?.FindFirst(ClaimTypes.NameIdentifier);
        if (openIdClaim == null || string.IsNullOrWhiteSpace(openIdClaim.Value))
            return Redirect(redirectUrl);

        List<string> supportProviders = new List<string> {
            LinUserIdentity.Gitee,
            LinUserIdentity.GitHub
        };

        if (!supportProviders.Contains(provider))
        {
            logger.LogError($"未知的privoder:{provider},redirectUrl:{redirectUrl}");
            throw new LinCmsException($"未知的privoder:{provider}！");
        }

        IOAuth2Service oAuth2Service = componentContext.ResolveNamed<IOAuth2Service>(provider);

        long id = await oAuth2Service.SaveUserAsync(authenticateResult.Principal, openIdClaim.Value);

        if (authenticateResult.Principal == null) throw new LinCmsException("无法获取第三方授权信息");

        //List<Claim> authClaims = authenticateResult.Principal.Claims.ToList();

        LinUser user = await userRepository.Select
            .IncludeMany(r => r.LinGroups)
            .WhereCascade(r => r.IsDeleted == false)
            .Where(r => r.Id == id)
            .FirstAsync();

        if (user == null)
        {
            throw new LinCmsException("第三方登录失败！");
        }

        UserAccessToken tokens = await tokenManager.CreateTokenAsync(user);

        return Redirect($"{redirectUrl}#login-result?token={tokens.AccessToken}");
    }

    /// <summary>
    /// https://localhost:5001/cms/oauth2/signin?provider=GitHub&redirectUrl=http://localhost:8080/
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="redirectUrl"></param>
    /// <returns></returns>
    [HttpGet("signin")]
    public async Task<IActionResult> SignIn(string provider, string redirectUrl)
    {
        // Note: the "provider" parameter corresponds to the external
        // authentication provider choosen by the user agent.
        if (string.IsNullOrWhiteSpace(provider))
        {
            return BadRequest();
        }

        if (!await HttpContext.IsProviderSupportedAsync(provider))
        {
            return BadRequest();
        }

        string url = $"{Request.PathBase}{Request.Path}-callback?provider={provider}" + $"&redirectUrl={redirectUrl}";

        logger.LogInformation($"SignIn-url:{url}");
        var properties = new AuthenticationProperties { RedirectUri = url };
        properties.Items[LoginProviderKey] = provider;
        return Challenge(properties, provider);

    }

    /// <summary>
    /// 第三方账号绑定回调
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="redirectUrl"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    [HttpGet("signin-bind-callback")]
    public async Task<IActionResult> SignInBindCallBack(string provider, string redirectUrl = "", string token = "")
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return BadRequest();
        }

        if (!await HttpContext.IsProviderSupportedAsync(provider))
        {
            return BadRequest();
        }

        if (token.IsNullOrEmpty() || !token.StartsWith("Bearer "))
        {
            return Redirect($"{redirectUrl}#bind-result?code={ErrorCode.Fail}&message={HttpUtility.UrlEncode("请先登录")}");
        }
        else
        {
            token = token.Remove(0, 7);
        }

        AuthenticateResult authenticateResult = await HttpContext.AuthenticateAsync(provider);
        if (!authenticateResult.Succeeded) return Redirect($"{redirectUrl}#bind-result?code=fail&message={authenticateResult.Failure?.Message}");
        var openIdClaim = authenticateResult.Principal?.FindFirst(ClaimTypes.NameIdentifier);
        if (openIdClaim == null || string.IsNullOrWhiteSpace(openIdClaim.Value))
            return Redirect($"{redirectUrl}#bind-result?code={ErrorCode.Fail}&message={HttpUtility.UrlEncode("未能获取openId")}");

        JwtPayload jwtPayload = (JwtPayload)jsonWebTokenService.Decode(token);
        string? nameIdentifier = jwtPayload.Claims.FirstOrDefault(r => r.Type == ClaimTypes.NameIdentifier)?.Value;
        if (nameIdentifier.IsNullOrWhiteSpace())
        {
            return Redirect($"{redirectUrl}#bind-result?code={ErrorCode.Fail}&message={HttpUtility.UrlEncode("请先登录")}");
        }
        long userId = long.Parse(nameIdentifier ?? "0");
        UnifyResponseDto unifyResponseDto;

        List<string> supportProviders = new() { LinUserIdentity.Gitee, LinUserIdentity.GitHub};

        if (!supportProviders.Contains(provider))
        {
            logger.LogError($"未知的privoder:{provider},redirectUrl:{redirectUrl}");
            unifyResponseDto = UnifyResponseDto.Error($"未知的privoder:{provider}！");
        }
        else
        {
            IOAuth2Service oAuth2Service = componentContext.ResolveNamed<IOAuth2Service>(provider);
            unifyResponseDto = await oAuth2Service.BindAsync(authenticateResult.Principal, provider, openIdClaim.Value, userId);
        }

        return Redirect($"{redirectUrl}#bind-result?code={unifyResponseDto.Code.ToString()}&message={HttpUtility.UrlEncode(unifyResponseDto.Message.ToString())}");
    }

    /// <summary>
    /// 第三方账号绑定，需要把token值传过来，用于与当前登录人绑定起来
    /// </summary>
    /// <param name="provider">GitHub/Gitee/QQ</param>
    /// <param name="redirectUrl">http://localhost:8081/   http://vvlog.igeekfan.cn/</param>
    /// <param name="token">Bearer {Token}</param>
    /// <returns></returns>

    [HttpGet("signin-bind")]
    public async Task<IActionResult> SignInBind(string provider, string redirectUrl, string token)
    {
        // Note: the "provider" parameter corresponds to the external
        // authentication provider choosen by the user agent.
        if (string.IsNullOrWhiteSpace(provider))
        {
            return BadRequest();
        }

        if (!await HttpContext.IsProviderSupportedAsync(provider))
        {
            return BadRequest();
        }

        string url = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{Request.Path}-callback?provider={provider}"
                     + $"&redirectUrl={redirectUrl}&token={token}";

        logger.LogInformation($"SignIn-url:{url}");
        var properties = new AuthenticationProperties
        {
            RedirectUri = url,
            Items =
            {
                [LoginProviderKey] = provider
            }
        };
        return Challenge(properties, provider);

    }

    /// <summary>
    /// 获取第三方登录的提供商
    /// </summary>
    /// <returns></returns>
    [HttpGet("external-providers")]
    public async Task<AuthenticationScheme[]> GetExternalProvidersAsync()
    {
        return await HttpContext.GetExternalProvidersAsync();
    }

    [HttpGet("signout")]
    public new IActionResult SignOut()
    {
        // Instruct the cookies middleware to delete the local cookie created
        // when the user agent is redirected from the external identity provider
        // after a successful authentication flow (e.g Google or Facebook).
        return base.SignOut(new AuthenticationProperties { RedirectUri = "/" }, CookieAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// 通过axios请求得到null，浏览器直接打开能得到github的id 
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    [HttpGet("OpenId")]
    public async Task<string?> OpenId(string provider)
    {
        AuthenticateResult authenticateResult = await HttpContext.AuthenticateAsync(provider);
        if (!authenticateResult.Succeeded) return null;
        Claim? openIdClaim = authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier);
        return openIdClaim?.Value;

    }

    /// <summary>
    /// 通过axios请求，请在header（请求头）携带上文中signin-callback生成的Token值.可以得到openid
    /// </summary>
    /// <returns></returns>
    [HttpGet("GetOpenIdByToken")]
    public string? GetOpenIdByTokenAsync()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// 得到当前用户绑定的第三方账号，除密码外
    /// </summary>
    /// <param name="currentUser"></param>
    /// <returns></returns>
    [Authorize]
    [HttpGet("bindlist")]
    public async Task<List<UserIdentityDto>> GetListAsync([FromServices] ICurrentUser currentUser)
    {
        return (await userCommunityService.GetListAsync(currentUser.FindUserId() ?? 0)).Where(r => r.IdentityType != LinUserIdentity.Password).ToList();
    }

    /// <summary>
    /// 解绑用户的第三方账号：当用户没有密码时，无法解绑最后一个账号,只可以解绑自己的账号
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [Authorize]
    [HttpDelete("unbind/{id}")]
    public Task UnBind(Guid id)
    {
        return userCommunityService.UnBind(id);
    }
}