﻿using Autofac;
using EPC.Core;
using Hiwjcn.Bll.Auth;
using Hiwjcn.Core.Data;
using Hiwjcn.Dal;
using Hiwjcn.Framework;
using Hiwjcn.Framework.Factory;
using Hiwjcn.Framework.Tasks;
using Hiwjcn.Web.App_Start;
using Lib.core;
using Lib.data.ef;
using Lib.extension;
using Lib.helper;
using Lib.infrastructure.service;
using Lib.infrastructure.service.user;
using Lib.ioc;
using Lib.mvc;
using Lib.mvc.auth;
using Lib.mvc.auth.validation;
using Lib.mvc.user;
using Lib.task;
using Polly;
using Polly.Timeout;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Hiwjcn.Web
{
    public class MvcApplication : System.Web.HttpApplication
    {
        private readonly ISettings settting = ConfigHelper.Instance;

        #region Application
        protected void Application_Start()
        {
            try
            {
                Action<long, string> logger = (ms, name) =>
                {
                    $"{nameof(Application_Start)}|耗时：{ms}毫秒".AddBusinessInfoLog();
                };
                using (var timer = new CpuTimeLogger(logger))
                {
                    /*
                    if (!("config_1.json", "config_2.json").SameJsonStructure())
                    {
                        throw new Exception("正式机和测试机配置文件结构不相同");
                    }*/

                    //添加依赖注入
                    IocContext.Instance.AddExtraRegistrar(new CommonDependencyRegister());
                    IocContext.Instance.AddExtraRegistrar(new FullDependencyRegistrar());
                    IocContext.Instance.OnContainerBuilding += (ref ContainerBuilder builder) =>
                    {
                        Func<LoginStatus> CookieProvider = () => new LoginStatus($"auth_user_uid", $"auth_user_token", $"auth_user_session");

                        var server_host = string.Empty;
                        if (ValidateHelper.IsPlumpString(server_host))
                        {
                            builder.AuthBasicServerConfig<AuthLoginProvider>(() => new AuthServerConfig(server_host), CookieProvider);
                        }
                        else
                        {
                            builder.AuthBasicConfig<AuthLoginProvider, AuthApiService>(CookieProvider);
                        }
                    };

                    //disable "X-AspNetMvc-Version" header name
                    MvcHandler.DisableMvcResponseHeader = true;
                    AreaRegistration.RegisterAllAreas();
                    FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
                    RouteConfig.RegisterRoutes(RouteTable.Routes);
                    //用AutoFac接管控制器生成，从而实现依赖注入
                    //ControllerBuilder.Current.SetControllerFactory(typeof(AutoFacControllerFactory));
                    //使用autofac生成控制器
                    DependencyResolver.SetResolver(IocContext.Instance.Container.AutofacDependencyResolver_());

                    try
                    {
                        //断网的情况下这里不会抛异常，会长时间等待
                        Policy.Timeout(TimeSpan.FromSeconds(10), TimeoutStrategy.Pessimistic).Execute(() =>
                        {
                            //加速首次启动EF
                            EFManager.FastStart<EntityDB>();
                            EFManager.FastStart<EpcEntityDB>();
                        });
                    }
                    catch (Exception err)
                    {
                        throw new Exception("设置EF快速启动失败", err);
                    }

#if DEBUG
                    //安装数据库
                    this.InstallDatabase();
#endif

                    //启动后台服务
                    TaskManager.Start();
                }
            }
            catch (Exception e)
            {
                e.AddErrorLog("网站启动异常");
                throw e;
            }
        }

        /// <summary>
        /// 安装数据库
        /// </summary>
        private void InstallDatabase()
        {
            try
            {
                var app_data = Server.AppDataPath();
                new DirectoryInfo(app_data).CreateIfNotExist();
                var lock_file = Path.Combine(app_data, "database_installed.json");

                if (!File.Exists(lock_file))
                {
                    //尝试创建数据表
                    EFManager.TryInstallDatabase<EntityDB>();
                    EFManager.TryInstallDatabase<EpcEntityDB>();
                    //写文件
                    var data = new
                    {
                        msg = "数据库已经安装，要重新安装请删除这个文件并重启系统",
                        time = DateTime.Now
                    }.ToJson();
                    File.WriteAllText(lock_file, data, settting.SystemEncoding);
                }
            }
            catch (Exception e)
            {
                throw new Exception("尝试安装数据库失败", e);
            }
        }

        protected void Application_End(object sender, EventArgs e)
        {
            try
            {
                ActorsFactory.Dispose();
                //关闭的时候不等待任务完成
                TaskManager.Dispose();
                IocContext.Instance.Dispose();
                LibReleaseHelper.DisposeAll();

                //记录程序关闭
                nameof(Application_End).AddBusinessInfoLog();
            }
            catch (Exception ex)
            {
                ex.AddErrorLog("网站关闭异常");
                throw ex;
            }
        }

        /// <summary>
        /// 配置了iis错误页面，只有找不到action才会走到这里
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void Application_Error123(object sender, EventArgs e)
        {
            var ex = Server.GetLastError();
            if (ex == null) { return; }
            //记录错误
            if (ex is HttpException err)
            {
                int httpcode = err.GetHttpCode();
                if (new int[] { 404, 403, 500 }.Contains(httpcode))
                {
                    //进入处理范围
                    Server.ClearError();
                    Response.Clear();
                    Response.TrySkipIisCustomErrors = true;
                    string ActionName = string.Empty;

                    var controller = new Hiwjcn.Web.Controllers.ErrorController();

                    switch (httpcode)
                    {
                        case 404: ActionName = nameof(controller.Http404); break;
                        case 403: ActionName = nameof(controller.Http403); break;
                        case 500: ActionName = nameof(controller.Http500); break;
                        default: ActionName = nameof(controller.Http404); break;
                    }

                    var routingData = new RouteData();
                    // In case controller is in another area
                    //routingData.DataTokens["area"] = "AreaName"; 
                    routingData.Values["controller"] = "Error";
                    routingData.Values["action"] = ActionName;

                    IController c = controller;
                    c.Execute(new RequestContext(new HttpContextWrapper(Context), routingData));
                }
            }
            else
            {
                ex.AddErrorLog($"{nameof(Application_Error123)}捕捉到错误");
            }
        }
        #endregion

        #region Request
        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            //设置请求的唯一ID
            Com.SetNewRequestID();
            //开启跨域
            HttpContext.Current.AllowCrossDomainAjax();
        }
        #endregion

    }
}

/*
·         Application_Init：在应用程序被实例化或第一次被调用时，该事件被触发。对于所有的HttpApplication 对象实例，它都会被调用。

·         Application_Disposed：在应用程序被销毁之前触发。这是清除以前所用资源的理想位置。

·         Application_Error：当应用程序中遇到一个未处理的异常时，该事件被触发。

·         Application_Start：在HttpApplication 类的第一个实例被创建时，该事件被触发。它允许你创建可以由所有HttpApplication 实例访问的对象。

·         Application_End：在HttpApplication 类的最后一个实例被销毁时，该事件被触发。在一个应用程序的生命周期内它只被触发一次。

·         Application_BeginRequest：在接收到一个应用程序请求时触发。对于一个请求来说，它是第一个被触发的事件，请求一般是用户输入的一个页面请求（URL）。

·         Application_EndRequest：针对应用程序请求的最后一个事件。

·         Application_PreRequestHandlerExecute：在 ASP.NET 页面框架开始执行诸如页面或 Web 服务之类的事件处理程序之前，该事件被触发。

·         Application_PostRequestHandlerExecute：在 ASP.NET 页面框架结束执行一个事件处理程序时，该事件被触发。

·         Applcation_PreSendRequestHeaders：在 ASP.NET 页面框架发送 HTTP 头给请求客户（浏览器）时，该事件被触发。

·         Application_PreSendContent：在 ASP.NET 页面框架发送内容给请求客户（浏览器）时，该事件被触发。

·         Application_AcquireRequestState：在 ASP.NET 页面框架得到与当前请求相关的当前状态（Session 状态）时，该事件被触发。

·         Application_ReleaseRequestState：在 ASP.NET 页面框架执行完所有的事件处理程序时，该事件被触发。这将导致所有的状态模块保存它们当前的状态数据。

·         Application_ResolveRequestCache：在 ASP.NET 页面框架完成一个授权请求时，该事件被触发。它允许缓存模块从缓存中为请求提供服务，从而绕过事件处理程序的执行。

·         Application_UpdateRequestCache：在 ASP.NET 页面框架完成事件处理程序的执行时，该事件被触发，从而使缓存模块存储响应数据，以供响应后续的请求时使用。

·         Application_AuthenticateRequest：在安全模块建立起当前用户的有效的身份时，该事件被触发。在这个时候，用户的凭据将会被验证。

·         Application_AuthorizeRequest：当安全模块确认一个用户可以访问资源之后，该事件被触发。

·         Session_Start：在一个新用户访问应用程序 Web 站点时，该事件被触发。

·         Session_End：在一个用户的会话超时、结束或他们离开应用程序 Web 站点时，该事件被触发。
 */
