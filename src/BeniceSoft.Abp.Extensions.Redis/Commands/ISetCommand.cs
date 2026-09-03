namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// Redis Set 命令接口
/// </summary>
public interface ISetCommand
{
    /// <summary>
    /// 将一个或多个 member 元素加入到集合 key 当中，已经存在于集合的 member 元素将被忽略。
    /// 假如 key 不存在，则创建一个只包含 member 元素作成员的集合。
    /// 当 key 不是集合类型时，返回一个错误。
    /// 时间复杂度：O(N)， N 是被添加的元素的数量。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="members"></param>
    /// <returns>被添加到集合中的新元素的数量，不包括被忽略的元素。</returns>
    long SAdd<T>(string key, params T[] members);

    /// <summary>
    /// 将一个或多个 member 元素加入到集合 key 当中，已经存在于集合的 member 元素将被忽略。
    /// </summary>
    Task<long> SAddAsync<T>(string key, params T[] members);

    /// <summary>
    /// 返回集合 key 的基数(集合中元素的数量)。
    /// 时间复杂度：O(1)
    /// </summary>
    long SCard(string key);

    /// <summary>
    /// 返回集合 key 的基数(集合中元素的数量)。
    /// </summary>
    Task<long> SCardAsync(string key);

    /// <summary>
    /// 返回集合 key 中的所有成员。
    /// 不存在的 key 被视为空集合。
    /// 时间复杂度：O(N)， N 为集合的基数。
    /// </summary>
    HashSet<T?> SMembers<T>(string key);

    /// <summary>
    /// 返回集合 key 中的所有成员。
    /// </summary>
    Task<HashSet<T?>> SMembersAsync<T>(string key);

    /// <summary>
    /// 判断 member 元素是否集合 key 的成员。
    /// 时间复杂度：O(1)
    /// </summary>
    bool SIsMember<T>(string key, T member);

    /// <summary>
    /// 判断 member 元素是否集合 key 的成员。
    /// </summary>
    Task<bool> SIsMemberAsync<T>(string key, T member);

    /// <summary>
    /// 移除集合 key 中的一个或多个 member 元素，不存在的 member 元素会被忽略。
    /// 当 key 不是集合类型，返回一个错误。
    /// 时间复杂度：O(N)， N 为给定 member 元素的数量。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="members">被成功移除的元素的数量，不包括被忽略的元素。</param>
    long SRem<T>(string key, params T[] members);

    /// <summary>
    /// 移除集合 key 中的一个或多个 member 元素。
    /// </summary>
    Task<long> SRemAsync<T>(string key, params T[] members);

    /// <summary>
    /// 移除并返回集合中的一个随机元素。
    /// 时间复杂度：O(1)
    /// </summary>
    T? SPop<T>(string key);

    /// <summary>
    /// 移除并返回集合中的一个随机元素。
    /// </summary>
    Task<T?> SPopAsync<T>(string key);

    /// <summary>
    /// 返回集合中的一个随机元素。
    /// </summary>
    T? SRandMember<T>(string key);

    /// <summary>
    /// 返回集合中的一个随机元素。
    /// </summary>
    Task<T?> SRandMemberAsync<T>(string key);

    /// <summary>
    /// 返回一个集合的全部成员，该集合是所有给定集合之间的差集。
    /// 不存在的 key 被视为空集。
    /// </summary>
    HashSet<T?> SDiff<T>(params string[] keys);

    /// <summary>
    /// 返回一个集合的全部成员，该集合是所有给定集合之间的差集。
    /// </summary>
    Task<HashSet<T?>> SDiffAsync<T>(params string[] keys);

    /// <summary>
    /// 返回一个集合的全部成员，该集合是所有给定集合的交集。
    /// </summary>
    HashSet<T?> SInter<T>(params string[] keys);

    /// <summary>
    /// 返回一个集合的全部成员，该集合是所有给定集合的交集。
    /// </summary>
    Task<HashSet<T?>> SInterAsync<T>(params string[] keys);

    /// <summary>
    /// 返回一个集合的全部成员，该集合是所有给定集合的并集。
    /// </summary>
    HashSet<T?> SUnion<T>(params string[] keys);

    /// <summary>
    /// 返回一个集合的全部成员，该集合是所有给定集合的并集。
    /// </summary>
    Task<HashSet<T?>> SUnionAsync<T>(params string[] keys);

    /// <summary>
    /// SMOVE 是原子性操作。
    /// 将 member 元素从 source 集合移动到 destination 集合。
    /// </summary>
    bool SMove<T>(string source, string destination, T member);

    /// <summary>
    /// SMOVE 是原子性操作。
    /// 将 member 元素从 source 集合移动到 destination 集合。
    /// </summary>
    Task<bool> SMoveAsync<T>(string source, string destination, T member);
}

