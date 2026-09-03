using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace BeniceSoft.Abp.Sample.Application.Contracts
{
    public class AmUserRoleDto : EntityDto<long>
    {
        public string Name { get; set; } = string.Empty;

        public string NormalizedName { get; set; } = string.Empty;

        /// <summary>
        /// 启用/禁用
        /// </summary>
        public bool IsEnabled { get; set; }

        public RoleTypeEnum? RoleType { get; set; }
    }
}

public enum RoleTypeEnum
{
    /// <summary>
    /// 管理员
    /// </summary>
    [Description("管理员")]
    Admin,

    /// <summary>
    /// 牛马
    /// </summary>
    [Description("牛马")]
    BeastsOfBurden
}