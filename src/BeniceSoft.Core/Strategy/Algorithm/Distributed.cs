namespace BeniceSoft.Core.Strategy;

/// <summary>
/// 分组算法
/// </summary>
public sealed class Distributed
{
    #region Assigns
    /// <summary>
    /// 平均分派分组（根据数量，将相同tag分到同一组）
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="total">总分组数量</param>
    /// <returns>可能会达不到分组的总数量，数据</returns>
    public static List<List<(string Tag, int Count)>> Assign(List<(string Tag, int Count)> source, int total)
    {
        if (source.Count <= total)
        {
            var org = new List<List<(string, int)>>(source.Count);
            source.OrderByDescending(t => t.Count).ForEach(t => org.Add([t]));

            return org;
        }

        source = [.. source.OrderByDescending(t => t.Count)];

        var start = 0;
        var list = new List<List<(string, int)>>();
        var avg = Math.Round((decimal)source.Sum(t => t.Count) / total);
        var greater = source.FindAll(t => t.Count >= avg);
        while (greater.IsNotNull())
        {
            greater.ForEach(t =>
            {
                list.Add([t]);
                start++;
            });

            source = source.FindAll(t => t.Count < avg);
            avg = Math.Round((decimal)source.Sum(t => t.Count) / (total - start));
            if (avg <= 0)
            {
                break;
            }

            greater = source.FindAll(t => t.Count >= avg);
        }

        if (start >= total || avg <= 0)
        {
            source.ForEach(t => list[start - 1].Add(t));
            source.Clear();
        }
        else
        {
            foreach (var i in start..total)
            {
                var data = BackTracking(source, i, total, avg.ToInt32());
                if (data != null)
                {
                    list.Add(data);
                }
                else
                {
                    break;
                }
            }
        }

        return list;
    }

    /// <summary>
    /// 回溯法查找最佳数量
    /// </summary>
    /// <param name="source"></param>
    /// <param name="cur"></param>
    /// <param name="total"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    private static List<(string Tag, int Count)>? BackTracking(List<(string Tag, int Count)> source, int cur, int total, int target)
    {
        if (source.IsNull())
        {
            return null;
        }

        var start = source[0];
        source.RemoveAt(0);
        var list = new List<(string Tag, int Count)> { start };

        if (cur == total - 1)
        {
            source.ForEach(list.Add);
            source.Clear();
            return list;
        }

        if (start.Count >= target)
        {
            return list;
        }

        var diff = target - start.Count;
        var item = FindNearest(source, diff);
        while (item.HasValue)
        {
            list.Add(item.Value);
            var sum = list.Sum(t => t.Count);
            if (sum < target)
            {
                var exp = target - sum;
                item = FindNearest(source, exp);
            }
            else
            {
                break;
            }
        }

        return list;
    }

    /// <summary>
    /// 找出最接近目标的数字(二分法)
    /// </summary>
    /// <param name="source">数组，降序排列</param>
    /// <param name="target"></param>
    /// <returns></returns>
    private static (string Tag, int Count)? FindNearest(List<(string Tag, int Count)> source, int target)
    {
        if (source.IsNull())
        {
            return null;
        }

        var index = -1;
        var count = source.Count;
        if (count == 1)
        {
            index = 0;
        }

        var fIndex = source.FindIndex(t => t.Count <= target);
        if (fIndex == -1)
        {
            index = count - 1;
        }
        else if (fIndex == 0)
        {
            index = 0;
        }
        else
        {
            var min = source[fIndex].Count;
            var max = source[fIndex - 1].Count;
            index = Math.Abs(target - min) <= Math.Abs(max - target) ? fIndex : fIndex - 1;
        }

        var aim = source[index].Count;
        if (aim > target * 2)
        {
            return null;
        }

        (string, int)? result = null;

        if (index >= 0)
        {
            result = source[index];
            source.RemoveAt(index);
        }

        return result;
    }
    #endregion

    #region Similar
    /// <summary>
    /// 相似度分组
    /// </summary>
    /// <param name="source"></param>
    /// <param name="fixCount">每组固定个数</param>
    /// <param name="total">总分组数量</param>
    /// <returns></returns>
    public static List<IGrouping<int, (string Id, List<string> Tags, byte Priority)>> Similar(List<(string Id, List<string> Tags, byte Priority)> source, int fixCount, int total)
    {
        var list = new List<IGrouping<int, (string Id, List<string> Tags, byte Priority)>>();
        source = [.. source.OrderBy(t => t.Priority).ThenBy(t => t.Tags.Count)];

        foreach (var g in total)
        {
            var maxSource = source[0];
            var group = new Grouping<int, (string Id, List<string> Tags, byte Priority)>(g)
                {
                    maxSource
                };
            source.Remove(maxSource);

            var tags = maxSource.Tags;
            var result = FindNearest(source, tags, fixCount - 1);
            result.ForEach(group.Add);
            list.Add(group);
            if (source.IsNull())
            {
                break;
            }
        }

        return list;
    }

    /// <summary>
    /// 查找最佳匹配的数据
    /// </summary>
    /// <param name="source"></param>
    /// <param name="tags"></param>
    /// <param name="fixCount"></param>
    /// <returns></returns>
    private static List<(string Id, List<string> Tags, byte Priority)> FindNearest(List<(string Id, List<string> Tags, byte Priority)> source, List<string> tags, int fixCount)
    {
        if (fixCount == 0)
        {
            return [];
        }

        var aim = source.OrderBy(t => t.Tags.Union(tags).Count()).ThenBy(t => t.Priority).ThenByDescending(t => t.Tags.Count).Take(fixCount);

        var list = new List<(string Id, List<string> Tags, byte Priority)>();
        foreach (var item in aim)
        {
            var nTags = tags.Union(item.Tags).ToList();
            if (list.IsNull() || nTags.Count == tags.Count)
            {
                list.Add(item);
                source.Remove(item);
            }

            if (list.Count == fixCount || source.Count == 0)
            {
                break;
            }

            if (nTags.Count > tags.Count)
            {
                var result = FindNearest(source, nTags, fixCount - list.Count);
                list.AddRange(result);
            }
        }

        return list;
    }
    #endregion
}
