﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Lib.mvc;
using Lib.io;
using Lib.extension;
using Lib.helper;
using Lib.data.ef;
using Lib.mvc.auth;
using EPC.Core.Entity;
using Hiwjcn.Service;
using Hiwjcn.Service.Epc.InputsType;
using Hiwjcn.Framework;
using Hiwjcn.Service.Epc;

namespace Hiwjcn.Web.Areas.Epc.Controllers
{
    public class FcController : EpcBaseController
    {
        private readonly ICheckLogService _logService;

        public FcController(
            ICheckLogService _logService)
        {
            this._logService = _logService;
        }

        /// <summary>
        /// 翟瑞
        /// 设备最近点检时间
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost]
        [EpcAuth]
        public async Task<ActionResult> QueryDeviceLastLogTimeWithInMonth(string data)
        {
            return await RunActionAsync(async () =>
            {
                var uids = data?.JsonToEntity<string[]>(throwIfException: false);
                if (!ValidateHelper.IsPlumpList(uids))
                {
                    return GetJsonRes("参数错误");
                }

                var org_uid = this.GetSelectedOrgUID();
                var loginuser = await this.ValidMember(org_uid, this.AnyRole);

                var model = await this._logService.QueryLastCheckLogTime(org_uid, uids, DateTime.Now.AddMonths(-1));
                return GetJson(new _()
                {
                    success = true,
                    data = model.Select(x => new { x.DeviceUID, x.LogTime }).ToList()
                });
            });
        }

        [HttpPost]
        [EpcAuth]
        public async Task<ActionResult> QueryCheckLog()
        {
            return await RunActionAsync(async () =>
            {
                var org_uid = this.GetSelectedOrgUID();
                var loginuser = await this.ValidMember(org_uid, this.AnyRole);

                var data = await this._logService.QueryCheckLog(org_uid, 30);

                return GetJson(new _()
                {
                    success = true,
                    data = data
                });
            });
        }

        /// <summary>
        /// 翟瑞
        /// 提交点检数据
        /// </summary>
        /// <param name="org_uid"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost]
        [EpcAuth]
        public async Task<ActionResult> SubmitCheckData(string data)
        {
            return await RunActionAsync(async () =>
            {
                var model = data?.JsonToEntity<DeviceInputData>(throwIfException: false);
                if (model == null)
                {
                    return GetJsonRes("参数错误");
                }

                var org_uid = this.GetSelectedOrgUID();
                var loginuser = await this.ValidMember(org_uid, this.AnyRole);

                model.OrgUID = org_uid;
                model.UserUID = loginuser?.UserID;

                var res = await this._logService.SubmitCheckLog(model);
                if (res.error)
                {
                    return GetJsonRes(res.msg);
                }

                return GetJson(new _()
                {
                    success = true,
                    data = res.data
                });
            });
        }
    }
}