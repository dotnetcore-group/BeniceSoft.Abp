-- 分布式锁续期脚本
-- KEYS[1]: 锁的 key
-- ARGV[1]: 锁的 value (唯一标识)
-- ARGV[2]: 新的过期时间(毫秒)
-- 返回值: 1=成功(创建或续期), 0=创建失败, -1=冲突(被其他客户端持有)

local currentVal = redis.call('get', KEYS[1])
if (currentVal == false) then
	-- 锁不存在，尝试创建
	return redis.call('set', KEYS[1], ARGV[1], 'PX', ARGV[2]) and 1 or 0
elseif (currentVal == ARGV[1]) then
	-- 锁存在且值匹配，续期成功
	return redis.call('pexpire', KEYS[1], ARGV[2])
else
	-- 锁被其他客户端持有
	return -1
end