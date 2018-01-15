﻿using Hiwjcn.Bll.User;
using Hiwjcn.Core.Domain.User;
using Hiwjcn.Core.Domain;
using Hiwjcn.Framework;
using Lib.extension;
using Lib.mvc.auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Lib.mvc;
using WebCore.MvcLib.Controller;

namespace Hiwjcn.Web.Controllers
{
    public class UserController : UserBaseController
    {
        private readonly IUserService _userService;
        private readonly IUserLoginService _login;
        private readonly IAuthApi _authApi;

        public UserController(
            IUserService _userService,
            IUserLoginService _login,
            IAuthApi _authApi)
        {
            this._userService = _userService;
            this._login = _login;
            this._authApi = _authApi;
        }

        /// <summary>
        /// 用户列表
        /// </summary>
        /// <param name="name"></param>
        /// <param name="email"></param>
        /// <param name="q"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        [HttpPost]
        //[EpcAuth(Permission = "manage.user.query")]
        [EpcAuth]
        public async Task<ActionResult> Query(string name, string email, string q, int? page)
        {
            return await RunActionAsync(async () =>
            {
                page = CheckPage(page);

                var data = await this._userService.QueryUserList(
                    name: name, email: email, keyword: q,
                    page: page.Value, pagesize: this.PageSize);

                foreach (var m in data.DataList)
                {
                    m.PassWord = null;
                }

                return GetJson(new _()
                {
                    success = true,
                    data = data
                });
            });
        }

        /// <summary>
        /// 搜索建议
        /// </summary>
        /// <param name="q"></param>
        /// <returns></returns>
        [HttpPost]
        [EpcAuth]
        public async Task<ActionResult> UserSuggest(string q)
        {
            return await RunActionAsync(async () =>
            {
                var data = await this._userService.UserSuggest(q, 10);
                return GetJson(new _()
                {
                    success = true,
                    data = data
                });
            });
        }

        [HttpPost]
        [EpcAuth]
        public async Task<ActionResult> ChangePwd(string data)
        {
            return await RunActionAsync(async () =>
            {
                var model = data?.JsonToEntity<UserEntity>(throwIfException: false);
                if (model == null)
                {
                    return GetJsonRes("参数错误");
                }
                var res = await this._login.ChangePwd(model);
                if (res.error)
                {
                    return GetJsonRes(res.msg);
                }

                return GetJsonRes(string.Empty);
            });
        }

        [HttpPost]
        [EpcAuth]
        public async Task<ActionResult> UpdateUser(string data)
        {
            return await RunActionAsync(async () =>
            {
                var model = data?.JsonToEntity<UserEntity>(throwIfException: false);
                if (model == null)
                {
                    return GetJsonRes("参数错误");
                }

                var res = await this._userService.UpdateUser(model);
                if (res.error)
                {
                    return GetJsonRes(res.msg);
                }

                return GetJsonRes(string.Empty);
            });
        }

    }
}