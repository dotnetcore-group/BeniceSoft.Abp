namespace BeniceSoft.Abp.Extensions.Redis;

public interface IListCommand
{
    /// <summary>
    /// 返回列表 key 中，下标为 index 的元素。
    /// 下标(index)参数 start 和 stop 都以 0 为底，也就是说，以 0 表示列表的第一个元素，以 1 表示列表的第二个元素，以此类推。
    /// 你也可以使用负数下标，以 -1 表示列表的最后一个元素， -2 表示列表的倒数第二个元素，以此类推。
    /// 如果 key 不是列表类型，返回一个错误。
    /// 时间复杂度：O(N)， N 为到达下标 index 过程中经过的元素数量。因此，对列表的头元素和尾元素执行 LINDEX 命令，复杂度为O(1)。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="index"></param>
    /// <returns>列表中下标为 index 的元素。
    /// 如果 index 参数的值不在列表的区间范围内(out of range)，返回 nil 。</returns>
    T? LIndex<T>(string key, long index);

    /// <summary>
    /// 返回列表 key 中，下标为 index 的元素。
    /// 下标(index)参数 start 和 stop 都以 0 为底，也就是说，以 0 表示列表的第一个元素，以 1 表示列表的第二个元素，以此类推。
    /// 你也可以使用负数下标，以 -1 表示列表的最后一个元素， -2 表示列表的倒数第二个元素，以此类推。
    /// 如果 key 不是列表类型，返回一个错误。
    /// 时间复杂度：O(N)， N 为到达下标 index 过程中经过的元素数量。因此，对列表的头元素和尾元素执行 LINDEX 命令，复杂度为O(1)。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="index"></param>
    /// <returns>列表中下标为 index 的元素。
    /// 如果 index 参数的值不在列表的区间范围内(out of range)，返回 nil 。</returns>
    Task<T?> LIndexAsync<T>(string key, long index);

    /// <summary>
    /// 返回列表 key 的长度。
    /// 如果 key 不存在，则 key 被解释为一个空列表，返回 0 .
    /// 如果 key 不是列表类型，返回一个错误。
    /// 时间复杂度：O(1)
    /// </summary>
    /// <param name="key"></param>
    /// <returns>列表 key 的长度。</returns>
    long LLen(string key);

    /// <summary>
    /// 返回列表 key 的长度。
    /// 如果 key 不存在，则 key 被解释为一个空列表，返回 0 .
    /// 如果 key 不是列表类型，返回一个错误。
    /// 时间复杂度：O(1)
    /// </summary>
    /// <param name="key"></param>
    /// <returns>列表 key 的长度。</returns>
    Task<long> LLenAsync(string key);

    /// <summary>
    /// 移除并返回列表 key 的头元素。
    /// 时间复杂度：O(1)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <returns>列表的头元素。
    /// 当 key 不存在时，返回 nil 。</returns>
    T? LPop<T>(string key);

    /// <summary>
    /// 移除并返回列表 key 的头元素。
    /// 时间复杂度：O(1)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <returns>列表的头元素。
    /// 当 key 不存在时，返回 nil 。</returns>
    Task<T?> LPopAsync<T>(string key);

    /// <summary>
    /// 移除并返回列表 key 的尾元素。
    /// 时间复杂度：O(1)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <returns>列表的头元素。
    /// 当 key 不存在时，返回 nil 。</returns>
    T? RPop<T>(string key);

    /// <summary>
    /// 移除并返回列表 key 的尾元素。
    /// 时间复杂度：O(1)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <returns>列表的头元素。
    /// 当 key 不存在时，返回 nil 。</returns>
    Task<T?> RPopAsync<T>(string key);

    /// <summary>
    /// 将一个或多个值 value 插入到列表 key 的表头
    /// 如果有多个 value 值，那么各个 value 值按从左到右的顺序依次插入到表头： 比如说，对空列表 mylist 执行命令 LPUSH mylist a b c ，列表的值将是 c b a ，这等同于原子性地执行 LPUSH mylist a 、 LPUSH mylist b 和 LPUSH mylist c 三个命令。
    /// 如果 key 不存在，一个空列表会被创建并执行 LPUSH 操作。
    /// 当 key 存在但不是列表类型时，返回一个错误。
    /// 时间复杂度：O(1)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="values"></param>
    /// <returns>执行 LPUSH 命令后，列表的长度。</returns>
    long LPush<T>(string key, params T[] values);

    /// <summary>
    /// 将一个或多个值 value 插入到列表 key 的表头
    /// 如果有多个 value 值，那么各个 value 值按从左到右的顺序依次插入到表头： 比如说，对空列表 mylist 执行命令 LPUSH mylist a b c ，列表的值将是 c b a ，这等同于原子性地执行 LPUSH mylist a 、 LPUSH mylist b 和 LPUSH mylist c 三个命令。
    /// 如果 key 不存在，一个空列表会被创建并执行 LPUSH 操作。
    /// 当 key 存在但不是列表类型时，返回一个错误。
    /// 时间复杂度：O(1)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="values"></param>
    /// <returns>执行 LPUSH 命令后，列表的长度。</returns>
    Task<long> LPushAsync<T>(string key, params T[] values);

    /// <summary>
    /// 将一个或多个值 value 插入到列表 key 的表尾(最右边)。
    /// 如果有多个 value 值，那么各个 value 值按从左到右的顺序依次插入到表尾：比如对一个空列表 mylist 执行 RPUSH mylist a b c ，得出的结果列表为 a b c ，等同于执行命令 RPUSH mylist a 、 RPUSH mylist b 、 RPUSH mylist c 。
    /// 如果 key 不存在，一个空列表会被创建并执行 RPUSH 操作。
    /// 当 key 存在但不是列表类型时，返回一个错误。
    /// 时间复杂度：O(1)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="values"></param>
    /// <returns>执行 RPUSH 操作后，表的长度。</returns>
    long RPush<T>(string key, params T[] values);

    /// <summary>
    /// 将一个或多个值 value 插入到列表 key 的表尾(最右边)。
    /// 如果有多个 value 值，那么各个 value 值按从左到右的顺序依次插入到表尾：比如对一个空列表 mylist 执行 RPUSH mylist a b c ，得出的结果列表为 a b c ，等同于执行命令 RPUSH mylist a 、 RPUSH mylist b 、 RPUSH mylist c 。
    /// 如果 key 不存在，一个空列表会被创建并执行 RPUSH 操作。
    /// 当 key 存在但不是列表类型时，返回一个错误。
    /// 时间复杂度：O(1)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="values"></param>
    /// <returns>执行 RPUSH 操作后，表的长度。</returns>
    Task<long> RPushAsync<T>(string key, params T[] values);

    /// <summary>
    /// 对一个列表进行修剪(trim)，就是说，让列表只保留指定区间内的元素，不在指定区间之内的元素都将被删除。
    /// 举个例子，执行命令 LTRIM list 0 2 ，表示只保留列表 list 的前三个元素，其余元素全部删除。
    /// 下标(index)参数 start 和 stop 都以 0 为底，也就是说，以 0 表示列表的第一个元素，以 1 表示列表的第二个元素，以此类推。
    /// 你也可以使用负数下标，以 -1 表示列表的最后一个元素， -2 表示列表的倒数第二个元素，以此类推。
    /// 当 key 不是列表类型时，返回一个错误。
    /// LTRIM 命令通常和 LPUSH 命令或 RPUSH 命令配合使用，举个例子：
    /// LPUSH log newest_log
    /// LTRIM log 0 99
    /// 这个例子模拟了一个日志程序，每次将最新日志 newest_log 放到 log 列表中，并且只保留最新的 100 项。注意当这样使用 LTRIM 命令时，时间复杂度是O(1)，因为平均情况下，每次只有一个元素被移除。
    /// 注意LTRIM命令和编程语言区间函数的区别
    /// 假如你有一个包含一百个元素的列表 list ，对该列表执行 LTRIM list 0 10 ，结果是一个包含11个元素的列表，这表明 stop 下标也在 LTRIM 命令的取值范围之内(闭区间)，这和某些语言的区间函数可能不一致，比如Ruby的 Range.new 、 Array#slice 和Python的 range() 函数。
    /// 超出范围的下标
    /// 超出范围的下标值不会引起错误。
    /// 如果 start 下标比列表的最大下标 end(LLEN list 减去 1 )还要大，或者 start > stop ， LTRIM 返回一个空列表(因为 LTRIM 已经将整个列表清空)。
    /// 如果 stop 下标比 end 下标还要大，Redis将 stop 的值设置为 end 。
    /// 时间复杂度:O(N)， N 为被移除的元素的数量。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="start"></param>
    /// <param name="stop"></param>
    void LTrim(string key, long start, long stop);

    /// <summary>
    /// 对一个列表进行修剪(trim)，就是说，让列表只保留指定区间内的元素，不在指定区间之内的元素都将被删除。
    /// 举个例子，执行命令 LTRIM list 0 2 ，表示只保留列表 list 的前三个元素，其余元素全部删除。
    /// 下标(index)参数 start 和 stop 都以 0 为底，也就是说，以 0 表示列表的第一个元素，以 1 表示列表的第二个元素，以此类推。
    /// 你也可以使用负数下标，以 -1 表示列表的最后一个元素， -2 表示列表的倒数第二个元素，以此类推。
    /// 当 key 不是列表类型时，返回一个错误。
    /// LTRIM 命令通常和 LPUSH 命令或 RPUSH 命令配合使用，举个例子：
    /// LPUSH log newest_log
    /// LTRIM log 0 99
    /// 这个例子模拟了一个日志程序，每次将最新日志 newest_log 放到 log 列表中，并且只保留最新的 100 项。注意当这样使用 LTRIM 命令时，时间复杂度是O(1)，因为平均情况下，每次只有一个元素被移除。
    /// 注意LTRIM命令和编程语言区间函数的区别
    /// 假如你有一个包含一百个元素的列表 list ，对该列表执行 LTRIM list 0 10 ，结果是一个包含11个元素的列表，这表明 stop 下标也在 LTRIM 命令的取值范围之内(闭区间)，这和某些语言的区间函数可能不一致，比如Ruby的 Range.new 、 Array#slice 和Python的 range() 函数。
    /// 超出范围的下标
    /// 超出范围的下标值不会引起错误。
    /// 如果 start 下标比列表的最大下标 end(LLEN list 减去 1 )还要大，或者 start > stop ， LTRIM 返回一个空列表(因为 LTRIM 已经将整个列表清空)。
    /// 如果 stop 下标比 end 下标还要大，Redis将 stop 的值设置为 end 。
    /// 时间复杂度:O(N)， N 为被移除的元素的数量。
    /// </summary>
    /// <param name="key"></param>
    /// <param name="start"></param>
    /// <param name="stop"></param>
    Task LTrimAsync(string key, long start, long stop);

    /// <summary>
    /// 返回列表 key 中指定区间内的元素，区间以偏移量 start 和 stop 指定。
    /// 下标(index)参数 start 和 stop 都以 0 为底，也就是说，以 0 表示列表的第一个元素，以 1 表示列表的第二个元素，以此类推。
    /// 你也可以使用负数下标，以 -1 表示列表的最后一个元素， -2 表示列表的倒数第二个元素，以此类推。
    /// 注意LRANGE命令和编程语言区间函数的区别
    /// 假如你有一个包含一百个元素的列表，对该列表执行 LRANGE list 0 10 ，结果是一个包含11个元素的列表，这表明 stop 下标也在 LRANGE 命令的取值范围之内(闭区间)，这和某些语言的区间函数可能不一致，比如Ruby的 Range.new 、 Array#slice 和Python的 range() 函数。
    /// 超出范围的下标
    /// 超出范围的下标值不会引起错误。
    /// 如果 start 下标比列表的最大下标 end(LLEN list 减去 1 )还要大，那么 LRANGE 返回一个空列表。
    /// 如果 stop 下标比 end 下标还要大，Redis将 stop 的值设置为 end 。
    /// 时间复杂度:O(S+N)， S 为偏移量 start ， N 为指定区间内元素的数量。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="start"></param>
    /// <param name="stop"></param>
    /// <returns>一个列表，包含指定区间内的元素。</returns>
    T?[] LRange<T>(string key, long start, long stop);

    /// <summary>
    /// 返回列表 key 中指定区间内的元素，区间以偏移量 start 和 stop 指定。
    /// 下标(index)参数 start 和 stop 都以 0 为底，也就是说，以 0 表示列表的第一个元素，以 1 表示列表的第二个元素，以此类推。
    /// 你也可以使用负数下标，以 -1 表示列表的最后一个元素， -2 表示列表的倒数第二个元素，以此类推。
    /// 注意LRANGE命令和编程语言区间函数的区别
    /// 假如你有一个包含一百个元素的列表，对该列表执行 LRANGE list 0 10 ，结果是一个包含11个元素的列表，这表明 stop 下标也在 LRANGE 命令的取值范围之内(闭区间)，这和某些语言的区间函数可能不一致，比如Ruby的 Range.new 、 Array#slice 和Python的 range() 函数。
    /// 超出范围的下标
    /// 超出范围的下标值不会引起错误。
    /// 如果 start 下标比列表的最大下标 end(LLEN list 减去 1 )还要大，那么 LRANGE 返回一个空列表。
    /// 如果 stop 下标比 end 下标还要大，Redis将 stop 的值设置为 end 。
    /// 时间复杂度:O(S+N)， S 为偏移量 start ， N 为指定区间内元素的数量。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="start"></param>
    /// <param name="stop"></param>
    /// <returns>一个列表，包含指定区间内的元素。</returns>
    Task<T?[]> LRangeAsync<T>(string key, long start, long stop);

    /// <summary>
    /// 根据参数 count 的值，移除列表中与参数 value 相等的元素。
    /// count 的值可以是以下几种：
    /// count > 0 : 从表头开始向表尾搜索，移除与 value 相等的元素，数量为 count 。
    /// count小于 0 : 从表尾开始向表头搜索，移除与 value 相等的元素，数量为 count 的绝对值。
    /// count = 0 : 移除表中所有与 value 相等的值。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    long LRem<T>(string key, T value, long count = 0);

    /// <summary>
    /// 根据参数 count 的值，移除列表中与参数 value 相等的元素。
    /// count 的值可以是以下几种：
    /// count > 0 : 从表头开始向表尾搜索，移除与 value 相等的元素，数量为 count 。
    /// count小于 0 : 从表尾开始向表头搜索，移除与 value 相等的元素，数量为 count 的绝对值。
    /// count = 0 : 移除表中所有与 value 相等的值。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    Task<long> LRemAsync<T>(string key, T value, long count = 0);

    /// <summary>
    /// 将列表 key 下标为 index 的元素的值设置为 value 。当 index 参数超出范围，或对一个空列表(key 不存在)进行 LSET 时，返回一个错误。
    /// 时间复杂度：对头元素或尾元素进行 LSET 操作，复杂度为 O(1)。其他情况下，为 O(N)， N 为列表的长度。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="index"></param>
    void LSet<T>(string key, T value, long index);

    /// <summary>
    /// 将列表 key 下标为 index 的元素的值设置为 value 。当 index 参数超出范围，或对一个空列表(key 不存在)进行 LSET 时，返回一个错误。
    /// 时间复杂度：对头元素或尾元素进行 LSET 操作，复杂度为 O(1)。其他情况下，为 O(N)， N 为列表的长度。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="index"></param>
    Task LSetAsync<T>(string key, T value, long index);
}
