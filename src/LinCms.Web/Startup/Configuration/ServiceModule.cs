﻿using Autofac;
using LinCms.Cms.Account;
using LinCms.Cms.Files;
using LinCms.Cms.Users;
using LinCms.Entities;
using LinCms.Middleware;

namespace LinCms.Startup.Configuration;

/// <summary>
/// 注入Application层中的Service
/// </summary>
public class ServiceModule : Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AopCacheIntercept>();
        builder.RegisterType<AopCacheAsyncIntercept>();
        
        //一个接口多个实现，使用Named，区分
        builder.RegisterType<LocalFileService>().Named<IFileService>(LinFile.LocalFileService).InstancePerLifetimeScope();
        builder.RegisterType<QiniuService>().Named<IFileService>(LinFile.QiniuService).InstancePerLifetimeScope();

        builder.RegisterType<GithubOAuth2Serivice>().Named<IOAuth2Service>(LinUserIdentity.GitHub).InstancePerLifetimeScope();
        builder.RegisterType<GiteeOAuth2Service>().Named<IOAuth2Service>(LinUserIdentity.Gitee).InstancePerLifetimeScope();

    }
}