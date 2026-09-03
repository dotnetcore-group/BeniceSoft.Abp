namespace BeniceSoft.Abp.Auth.Core;

public static class BeniceSoftAuthConstants
{
    public static class ClaimTypes
    {
        public const string Avatar = "avatar";

        public const string RoleId = "role_id";

        public const string DepartmentName = "department_name";
    }

    public static class Cache
    {
        /// <summary>
        /// 缓存 Token 过期时间(小时)
        /// 此值最好设置的比AM颁发token设置的过期时间长，避免在临界值导致401
        /// AM过期颁发新token，拿的就是最新的token，如果am没有过期在临界点万一存在毫秒差异，缓存失效了，也401了
        /// </summary>
        public const int AccessTokenLifetime = 3;

        /// <summary>
        /// 用户token生命周期缓存 Key 前缀
        /// </summary>
        public const string UserSessionKeyPrefix = "BeniceSoft:Auth:UserSession:";

        /// <summary>
        /// 获取用户token生命周期缓存 Key
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="clientId"></param>
        /// <returns></returns>
        public static string GetUserSessionKey(long userId, string clientId) => $"{UserSessionKeyPrefix}{userId}:{clientId}";

        /// <summary>
        /// 用户权限缓存 key 前缀
        /// </summary>
        public const string UserPermissionKeyPrefix = "BeniceSoft:Auth:UserPermissions:";

        /// <summary>
        /// 生成用户行数据权限缓存 key
        /// </summary>
        public static string GetUserRowPermissionKey(long userId) => $"{UserPermissionKeyPrefix}Row:{userId}";

        /// <summary>
        /// 生成用户字段权限缓存 key
        /// </summary>
        public static string GetUserFieldPermissionKey(long userId) => $"{UserPermissionKeyPrefix}Field:{userId}";

        /// <summary>
        /// 生成用户方法/API功能权限缓存 key
        /// </summary>
        public static string GetUserFunctionPermissionKey(long userId) => $"{UserPermissionKeyPrefix}Function:{userId}";
    }
}