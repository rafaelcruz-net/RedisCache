using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

using ServiceStack.Redis;

namespace RedisCache
{
    public class CacheManager
    {
        private static IRedisClient cache;
        private static string host = "aspnet-cache.redis.cache.windows.net";
        private static int port = 6380;
        private static string password = "gGkQSTQOtQwpt2cnylK74UzQOfrt2nxEUtBWx3LSCvY=";
        
        private static IRedisClient Cache
        {
            get
            {
                if (cache == null)
                    cache = GetConnection();
                
                return cache;

            }
        }

        private static IRedisClient GetConnection()
        {
            RedisEndpoint redisConfig = new RedisEndpoint();
            redisConfig.Host = host;
            redisConfig.Ssl = true;
            redisConfig.Port = port;
            redisConfig.Password = password;

            RedisClient client = new RedisClient(redisConfig);

            return client;
        }

        public static bool Set<T>(string key, T value, DateTime? expireAt = null)
        {
            if (expireAt.HasValue)
                return Cache.Set<T>(key, value, expireAt.Value);

            return Cache.Set<T>(key, value);
        }

        public static T Get<T>(string key)
        {
             return Cache.Get<T>(key);
        }

        public static bool Contains(string key)
        {
            try
            {
                return Cache.ContainsKey(key);
            }
            catch
            {
                return false;
            }
        }

        public static bool Invalidate(string key)
        {
            try
            {
                return Cache.Remove(key);
            }
            catch
            {
                return false;
            }
        }

        public static void Expire(string key, DateTime expireAt)
        {
            Cache.ExpireEntryAt(key, expireAt);
        }
    }
}