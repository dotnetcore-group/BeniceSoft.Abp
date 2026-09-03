namespace BeniceSoft.Abp.Extensions.Redis;

public interface ISortedSetCommand
{
    /// <summary>
    /// 将一个或多个 member 元素及其 score 值加入到有序集 key 当中。
    /// 如果某个 member 已经是有序集的成员，那么更新这个 member 的 score 值，并通过重新插入这个 member 元素，来保证该 member 在正确的位置上。
    /// score 值可以是整数值或双精度浮点数。
    /// 如果 key 不存在，则创建一个空的有序集并执行 ZADD 操作。
    /// 当 key 存在但不是有序集类型时，返回一个错误。
    /// 时间复杂度:O(M* log(N))， N 是有序集的基数， M 为成功添加的新成员的数量。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="members"></param>
    /// <returns></returns>
    long ZAdd<T>(string key, Dictionary<T, double> members) where T : notnull;

    /// <summary>
    /// 将一个或多个 member 元素及其 score 值加入到有序集 key 当中。
    /// 如果某个 member 已经是有序集的成员，那么更新这个 member 的 score 值，并通过重新插入这个 member 元素，来保证该 member 在正确的位置上。
    /// score 值可以是整数值或双精度浮点数。
    /// 如果 key 不存在，则创建一个空的有序集并执行 ZADD 操作。
    /// 当 key 存在但不是有序集类型时，返回一个错误。
    /// 时间复杂度:O(M* log(N))， N 是有序集的基数， M 为成功添加的新成员的数量。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="members"></param>
    /// <returns></returns>
    Task<long> ZAddAsync<T>(string key, Dictionary<T, double> members) where T : notnull;

    /// <summary>
    /// 返回有序集 key 的基数。
    /// 时间复杂度:O(1)
    /// </summary>
    /// <param name="key"></param>
    /// <returns>当 key 存在且是有序集类型时，返回有序集的基数。
    /// 当 key 不存在时，返回 0 。</returns>
    long ZCard(string key);

    /// <summary>
    /// 返回有序集 key 的基数。
    /// 时间复杂度:O(1)
    /// </summary>
    /// <param name="key"></param>
    /// <returns>当 key 存在且是有序集类型时，返回有序集的基数。
    /// 当 key 不存在时，返回 0 。</returns>
    Task<long> ZCardAsync(string key);

    /// <summary>
    /// 返回有序集 key 中， score 值在 min 和 max 之间(默认包括 score 值等于 min 或 max )的成员的数量。
    /// 时间复杂度:O(log(N)+M)， N 为有序集的基数， M 为值在 min 和 max 之间的元素的数量。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns>score 值在 min 和 max 之间的成员的数量。</returns>
    long ZCount(string key, double min, double max);

    /// <summary>
    /// 返回有序集 key 中， score 值在 min 和 max 之间(默认包括 score 值等于 min 或 max )的成员的数量。
    /// 时间复杂度:O(log(N)+M)， N 为有序集的基数， M 为值在 min 和 max 之间的元素的数量。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns>score 值在 min 和 max 之间的成员的数量。</returns>
    Task<long> ZCountAsync(string key, double min, double max);

    /// <summary>
    /// 为有序集 key 的成员 member 的 score 值加上增量 increment 。
    /// 可以通过传递一个负数值 increment ，让 score 减去相应的值，比如 ZINCRBY key -5 member ，就是让 member 的 score 值减去 5 。
    /// 当 key 不存在，或 member 不是 key 的成员时， ZINCRBY key increment member 等同于 ZADD key increment member 。
    /// 当 key 不是有序集类型时，返回一个错误。
    /// 时间复杂度:O(log(N))
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="member"></param>
    /// <param name="score"></param>
    /// <returns>member 成员的新 score 值</returns>
    double ZIncrBy<T>(string key, T member, double score);

    /// <summary>
    /// 为有序集 key 的成员 member 的 score 值加上增量 increment 。
    /// 可以通过传递一个负数值 increment ，让 score 减去相应的值，比如 ZINCRBY key -5 member ，就是让 member 的 score 值减去 5 。
    /// 当 key 不存在，或 member 不是 key 的成员时， ZINCRBY key increment member 等同于 ZADD key increment member 。
    /// 当 key 不是有序集类型时，返回一个错误。
    /// 时间复杂度:O(log(N))
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="member"></param>
    /// <param name="score"></param>
    /// <returns>member 成员的新 score 值</returns>
    Task<double> ZIncrByAsync<T>(string key, T member, double score);

    /// <summary>
    /// 返回有序集 key 中，指定区间内的成员。
    /// 其中成员的位置按 score 值递增(从小到大)来排序。
    /// 具有相同 score 值的成员按字典序(lexicographical order)来排列
    /// 如果你需要成员按 score 值递减(从大到小)来排列，请使用 ZREVRANGE 命令。
    /// 下标参数 start 和 stop 都以 0 为底，也就是说，以 0 表示有序集第一个成员，以 1 表示有序集第二个成员，以此类推
    /// 你也可以使用负数下标，以 -1 表示最后一个成员， -2 表示倒数第二个成员，以此类推。
    /// 超出范围的下标并不会引起错误
    /// 比如说，当 start 的值比有序集的最大下标还要大，或是 start > stop 时， ZRANGE 命令只是简单地返回一个空列表。
    /// 另一方面，假如 stop 参数的值比有序集的最大下标还要大，那么 Redis 将 stop 当作最大下标来处理。
    /// 时间复杂度: O(log(N)+M)， N 为有序集的基数，而 M 为结果集的基数。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="start"></param>
    /// <param name="stop"></param>
    /// <returns>指定区间内，带有 score 值(可选)的有序集成员的列表。</returns>
    Dictionary<T, double> ZRange<T>(string key, long start, long stop) where T : notnull;

    /// <summary>
    /// 返回有序集 key 中，指定区间内的成员。
    /// 其中成员的位置按 score 值递增(从小到大)来排序。
    /// 具有相同 score 值的成员按字典序(lexicographical order)来排列
    /// 如果你需要成员按 score 值递减(从大到小)来排列，请使用 ZREVRANGE 命令。
    /// 下标参数 start 和 stop 都以 0 为底，也就是说，以 0 表示有序集第一个成员，以 1 表示有序集第二个成员，以此类推
    /// 你也可以使用负数下标，以 -1 表示最后一个成员， -2 表示倒数第二个成员，以此类推。
    /// 超出范围的下标并不会引起错误
    /// 比如说，当 start 的值比有序集的最大下标还要大，或是 start > stop 时， ZRANGE 命令只是简单地返回一个空列表。
    /// 另一方面，假如 stop 参数的值比有序集的最大下标还要大，那么 Redis 将 stop 当作最大下标来处理。
    /// 时间复杂度: O(log(N)+M)， N 为有序集的基数，而 M 为结果集的基数。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="start"></param>
    /// <param name="stop"></param>
    /// <returns>指定区间内，带有 score 值(可选)的有序集成员的列表。</returns>
    Task<Dictionary<T, double>> ZRangeAsync<T>(string key, long start, long stop) where T : notnull;

    /// <summary>
    /// 返回有序集 key 中，指定区间内的成员。
    /// 其中成员的位置按 score 值递减(从大到小)来排列。
    /// 具有相同 score 值的成员按字典序的逆序(reverse lexicographical order)排列。
    /// 时间复杂度: O(log(N)+M)， N 为有序集的基数，而 M 为结果集的基数。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="start"></param>
    /// <param name="stop"></param>
    /// <returns>指定区间内，带有 score 值(可选)的有序集成员的列表。</returns>
    Dictionary<T, double> ZRevRange<T>(string key, long start, long stop) where T : notnull;

    /// <summary>
    /// 返回有序集 key 中，指定区间内的成员。
    /// 其中成员的位置按 score 值递减(从大到小)来排列。
    /// 具有相同 score 值的成员按字典序的逆序(reverse lexicographical order)排列。
    /// 时间复杂度: O(log(N)+M)， N 为有序集的基数，而 M 为结果集的基数。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="start"></param>
    /// <param name="stop"></param>
    /// <returns>指定区间内，带有 score 值(可选)的有序集成员的列表。</returns>
    Task<Dictionary<T, double>> ZRevRangeAsync<T>(string key, long start, long stop) where T : notnull;

    /// <summary>
    /// 返回有序集 key 中，所有 score 值介于 min 和 max 之间(包括等于 min 或 max )的成员。有序集成员按 score 值递增(从小到大)次序排列。
    /// 具有相同 score 值的成员按字典序(lexicographical order)来排列(该属性是有序集提供的，不需要额外的计算)。
    /// 可选的 LIMIT 参数指定返回结果的数量及区间(就像SQL中的 SELECT LIMIT offset, count )，注意当 offset 很大时，定位 offset 的操作可能需要遍历整个有序集，此过程最坏复杂度为 O(N) 时间。
    /// 时间复杂度:O(log(N)+M)， N 为有序集的基数， M 为结果集的基数。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns>指定区间内，带有 score 值(可选)的有序集成员的列表。</returns>
    Dictionary<T, double> ZRangeByScore<T>(string key, double min, double max) where T : notnull;

    /// <summary>
    /// 返回有序集 key 中，所有 score 值介于 min 和 max 之间(包括等于 min 或 max )的成员。有序集成员按 score 值递增(从小到大)次序排列。
    /// 具有相同 score 值的成员按字典序(lexicographical order)来排列(该属性是有序集提供的，不需要额外的计算)。
    /// 可选的 LIMIT 参数指定返回结果的数量及区间(就像SQL中的 SELECT LIMIT offset, count )，注意当 offset 很大时，定位 offset 的操作可能需要遍历整个有序集，此过程最坏复杂度为 O(N) 时间。
    /// 时间复杂度:O(log(N)+M)， N 为有序集的基数， M 为结果集的基数。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns>指定区间内，带有 score 值(可选)的有序集成员的列表。</returns>
    Task<Dictionary<T, double>> ZRangeByScoreAsync<T>(string key, double min, double max) where T : notnull;

    /// <summary>
    /// 返回有序集 key 中， score 值介于 max 和 min 之间(默认包括等于 max 或 min )的所有的成员。有序集成员按 score 值递减(从大到小)的次序排列。
    /// 具有相同 score 值的成员按字典序的逆序(reverse lexicographical order )排列。
    /// 时间复杂度:O(log(N)+M)， N 为有序集的基数， M 为结果集的基数。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns>指定区间内，带有 score 值(可选)的有序集成员的列表。</returns>
    Dictionary<T, double> ZRevRangeByScore<T>(string key, double min, double max) where T : notnull;

    /// <summary>
    /// 返回有序集 key 中， score 值介于 max 和 min 之间(默认包括等于 max 或 min )的所有的成员。有序集成员按 score 值递减(从大到小)的次序排列。
    /// 具有相同 score 值的成员按字典序的逆序(reverse lexicographical order )排列。
    /// 时间复杂度:O(log(N)+M)， N 为有序集的基数， M 为结果集的基数。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns>指定区间内，带有 score 值(可选)的有序集成员的列表。</returns>
    Task<Dictionary<T, double>> ZRevRangeByScoreAsync<T>(string key, double min, double max) where T : notnull;

    /// <summary>
    /// 返回有序集 key 中成员 member 的排名。其中有序集成员按 score 值递增(从小到大)顺序排列。
    /// 排名以 0 为底，也就是说， score 值最小的成员排名为 0 。
    /// 时间复杂度:O(log(N))
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="member"></param>
    /// <returns>如果 member 是有序集 key 的成员，返回 member 的排名。
    /// 如果 member 不是有序集 key 的成员，返回 nil 。</returns>
    long? ZRank<T>(string key, T member);

    /// <summary>
    /// 返回有序集 key 中成员 member 的排名。其中有序集成员按 score 值递增(从小到大)顺序排列。
    /// 排名以 0 为底，也就是说， score 值最小的成员排名为 0 。
    /// 时间复杂度:O(log(N))
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="member"></param>
    /// <returns>如果 member 是有序集 key 的成员，返回 member 的排名。
    /// 如果 member 不是有序集 key 的成员，返回 nil 。</returns>
    Task<long?> ZRankAsync<T>(string key, T member);

    /// <summary>
    /// 返回有序集 key 中成员 member 的排名。其中有序集成员按 score 值递减(从大到小)排序。
    /// 排名以 0 为底，也就是说， score 值最大的成员排名为 0 。
    /// 时间复杂度:O(log(N))
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="member"></param>
    /// <returns>如果 member 是有序集 key 的成员，返回 member 的排名。如果 member 不是有序集 key 的成员，返回 nil 。</returns>
    long? ZRevRank<T>(string key, T member);

    /// <summary>
    /// 返回有序集 key 中成员 member 的排名。其中有序集成员按 score 值递减(从大到小)排序。
    /// 排名以 0 为底，也就是说， score 值最大的成员排名为 0 。
    /// 时间复杂度:O(log(N))
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="member"></param>
    /// <returns>如果 member 是有序集 key 的成员，返回 member 的排名。如果 member 不是有序集 key 的成员，返回 nil 。</returns>
    Task<long?> ZRevRankAsync<T>(string key, T member);

    /// <summary>
    /// 返回有序集 key 中，成员 member 的 score 值。
    /// 如果 member 元素不是有序集 key 的成员，或 key 不存在，返回 nil 。
    /// 时间复杂度:O(1)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="member"></param>
    /// <returns>member 成员的 score 值</returns>
    double? ZScore<T>(string key, T member);

    /// <summary>
    /// 返回有序集 key 中，成员 member 的 score 值。
    /// 如果 member 元素不是有序集 key 的成员，或 key 不存在，返回 nil 。
    /// 时间复杂度:O(1)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="member"></param>
    /// <returns>member 成员的 score 值</returns>
    Task<double?> ZScoreAsync<T>(string key, T member);

    /// <summary>
    /// 移除有序集 key 中的一个或多个成员，不存在的成员将被忽略。
    /// 当 key 存在但不是有序集类型时，返回一个错误。
    /// 时间复杂度: O(M*log(N))， N 为有序集的基数， M 为被成功移除的成员的数量。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="members"></param>
    /// <returns>被成功移除的成员的数量，不包括被忽略的成员。</returns>
    long ZRem<T>(string key, params T[] members);

    /// <summary>
    /// 移除有序集 key 中的一个或多个成员，不存在的成员将被忽略。
    /// 当 key 存在但不是有序集类型时，返回一个错误。
    /// 时间复杂度: O(M*log(N))， N 为有序集的基数， M 为被成功移除的成员的数量。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="members"></param>
    /// <returns>被成功移除的成员的数量，不包括被忽略的成员。</returns>
    Task<long> ZRemAsync<T>(string key, params T[] members);

    /// <summary>
    /// 移除有序集 key 中，指定排名(rank)区间内的所有成员。
    /// 区间分别以下标参数 start 和 stop 指出，包含 start 和 stop 在内。
    /// 下标参数 start 和 stop 都以 0 为底，也就是说，以 0 表示有序集第一个成员，以 1 表示有序集第二个成员，以此类推。
    /// 你也可以使用负数下标，以 -1 表示最后一个成员， -2 表示倒数第二个成员，以此类推。
    /// 时间复杂度:O(log(N)+M)， N 为有序集的基数，而 M 为被移除成员的数量。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="start"></param>
    /// <param name="stop"></param>
    /// <returns>被移除成员的数量。</returns>
    long ZRemRangeByRank(string key, long start, long stop);

    /// <summary>
    /// 移除有序集 key 中，指定排名(rank)区间内的所有成员。
    /// 区间分别以下标参数 start 和 stop 指出，包含 start 和 stop 在内。
    /// 下标参数 start 和 stop 都以 0 为底，也就是说，以 0 表示有序集第一个成员，以 1 表示有序集第二个成员，以此类推。
    /// 你也可以使用负数下标，以 -1 表示最后一个成员， -2 表示倒数第二个成员，以此类推。
    /// 时间复杂度:O(log(N)+M)， N 为有序集的基数，而 M 为被移除成员的数量。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="start"></param>
    /// <param name="stop"></param>
    /// <returns>被移除成员的数量。</returns>
    Task<long> ZRemRangeByRankAsync(string key, long start, long stop);

    /// <summary>
    /// 移除有序集 key 中，所有 score 值介于 min 和 max 之间(包括等于 min 或 max )的成员。
    /// 时间复杂度:O(log(N)+M)， N 为有序集的基数，而 M 为被移除成员的数量。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    long ZRemRangeByScore(string key, double min, double max);

    /// <summary>
    /// 移除有序集 key 中，所有 score 值介于 min 和 max 之间(包括等于 min 或 max )的成员。
    /// 时间复杂度:O(log(N)+M)， N 为有序集的基数，而 M 为被移除成员的数量。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    Task<long> ZRemRangeByScoreAsync(string key, double min, double max);
}
