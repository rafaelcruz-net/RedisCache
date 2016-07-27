using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Web.SessionState;

namespace RedisCache
{
    public class RedisSessionState : SessionStateStoreProviderBase
    {
        private SessionStateSection sessionStateConfig = null;

        #region Overrides

        public override void Initialize(string name, NameValueCollection config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            if (name == null || name.Length == 0)
            {
                name = "RedisSessionStateStore";
            }

            if (String.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "Redis Session State Provider");
            }

            base.Initialize(name, config);

            sessionStateConfig = (SessionStateSection)ConfigurationManager.GetSection("system.web/sessionState");

        }

        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(new SessionStateItemCollection(), SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            SessionItem sessionItem = new SessionItem();
            sessionItem.CreatedAt = DateTime.Now.ToUniversalTime();
            sessionItem.LockDate = DateTime.Now.ToUniversalTime();
            sessionItem.LockID = 0;
            sessionItem.Timeout = timeout;
            sessionItem.Locked = false;
            sessionItem.SessionItems = string.Empty;
            sessionItem.Flags = 0;

            CacheManager.Set<SessionItem>(id, sessionItem, DateTime.UtcNow.AddMinutes(timeout));
        }

        public override void EndRequest(HttpContext context)
        {
            this.Dispose();
        }

        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return GetSessionStoreItem(true, context, id, out locked, out lockAge, out lockId, out actions);
        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return GetSessionStoreItem(true, context, id, out locked, out lockAge, out lockId, out actions);
        }

        public override void InitializeRequest(HttpContext context)
        {
            //NÃO FAZ NADA
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            SessionItem currentSessionItem = CacheManager.Get<SessionItem>(id);

            if (currentSessionItem != null && (int?)lockId == currentSessionItem.LockID)
            {
                currentSessionItem.Locked = false;
                CacheManager.Set<SessionItem>(id, currentSessionItem, DateTime.UtcNow.AddMinutes(sessionStateConfig.Timeout.TotalMinutes));
            }

        }

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            CacheManager.Invalidate(id);
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            CacheManager.Expire(id, DateTime.UtcNow.AddMinutes(sessionStateConfig.Timeout.TotalMinutes));
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            string sessionItems = Serialize((SessionStateItemCollection)item.Items);

            try
            {
                if (newItem)
                {
                    SessionItem sessionItem = new SessionItem();
                    sessionItem.CreatedAt = DateTime.UtcNow;
                    sessionItem.LockDate = DateTime.UtcNow;
                    sessionItem.LockID = 0;
                    sessionItem.Timeout = item.Timeout;
                    sessionItem.Locked = false;
                    sessionItem.SessionItems = sessionItems;
                    sessionItem.Flags = 0;

                    CacheManager.Set<SessionItem>(id, sessionItem, DateTime.UtcNow.AddMinutes(item.Timeout));
                }
                else
                {
                    SessionItem currentSessionItem = CacheManager.Get<SessionItem>(id);
                    if (currentSessionItem != null && currentSessionItem.LockID == (int?)lockId)
                    {
                        currentSessionItem.Locked = false;
                        currentSessionItem.SessionItems = sessionItems;
                        CacheManager.Set<SessionItem>(id, currentSessionItem, DateTime.UtcNow.AddMinutes(item.Timeout));
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }

        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return true;
        }

        public override void Dispose()
        {

        }

        #endregion

        #region Private Methods

        private string Serialize(SessionStateItemCollection items)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                if (items != null)
                    items.Serialize(writer);

                writer.Close();

                return Convert.ToBase64String(ms.ToArray());
            }
        }

        private SessionStateStoreData Deserialize(HttpContext context, string serializedItems, int timeout)
        {
            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(serializedItems)))
            {
                SessionStateItemCollection sessionItems = new SessionStateItemCollection();

                if (ms.Length > 0)
                {
                    using (BinaryReader reader = new BinaryReader(ms))
                    {
                        sessionItems = SessionStateItemCollection.Deserialize(reader);
                    }
                }

                return new SessionStateStoreData(sessionItems,
                  SessionStateUtility.GetSessionStaticObjects(context),
                  timeout);
            }
        }

        private SessionStateStoreData GetSessionStoreItem(bool lockRecord, HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actionFlags)
        {
            SessionStateStoreData item = null;
            lockAge = TimeSpan.Zero;
            lockId = null;
            locked = false;
            actionFlags = 0;

            string serializedItems = "";

            // Timeout value from the data store.
            int timeout = 0;

            try
            {
                if (lockRecord)
                {
                    locked = false;
                    SessionItem currentItem = CacheManager.Get<SessionItem>(id);

                    if (currentItem != null)
                    {
                        if (!currentItem.Locked)
                        {
                            currentItem.Locked = true;
                            currentItem.LockDate = DateTime.UtcNow;
                            CacheManager.Set<SessionItem>(id, currentItem, DateTime.UtcNow.AddMinutes(sessionStateConfig.Timeout.TotalMinutes));
                        }
                        else
                        {
                            locked = true;
                        }
                    }
                }

                SessionItem currentSessionItem = CacheManager.Get<SessionItem>(id);

                if (currentSessionItem != null)
                {
                    serializedItems = currentSessionItem.SessionItems;
                    lockId = currentSessionItem.LockID;
                    lockAge = DateTime.UtcNow.Subtract(currentSessionItem.LockDate);
                    actionFlags = (SessionStateActions)currentSessionItem.Flags;
                    timeout = currentSessionItem.Timeout;
                }
                else
                {
                    locked = false;
                }

                if (currentSessionItem != null && !locked)
                {
                    CacheManager.Invalidate(id);

                    lockId = (int?)lockId + 1;
                    currentSessionItem.LockID = lockId != null ? (int)lockId : 0;
                    currentSessionItem.Flags = 0;

                    CacheManager.Set<SessionItem>(id, currentSessionItem, DateTime.UtcNow.AddMinutes(sessionStateConfig.Timeout.TotalMinutes));

                    if (actionFlags == SessionStateActions.InitializeItem)
                        item = CreateNewStoreData(context, 30);
                    else
                        item = Deserialize(context, serializedItems, timeout);
                }
            }

            catch (Exception e)
            {
                throw e;
            }
            return item;
        }
        #endregion

        #region Session Item Class
        public class SessionItem
        {
            public DateTime CreatedAt { get; set; }
            public DateTime LockDate { get; set; }
            public int LockID { get; set; }
            public int Timeout { get; set; }
            public bool Locked { get; set; }
            public string SessionItems { get; set; }
            public int Flags { get; set; }
        }

        #endregion
    }
}