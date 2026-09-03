-- 分布式锁解锁脚本
-- KEYS[1]: 锁的 key
-- ARGV[1]: 锁的 value (唯一标识)
-- 返回值: 1=成功释放, 0=未持有锁

if redis.call('get', KEYS[1]) == ARGV[1] then
	return redis.call('del', KEYS[1])
else
	return 0
end