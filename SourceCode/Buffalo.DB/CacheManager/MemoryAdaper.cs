using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Caching;
using System.Web;
using System.Data;
using System.Collections;
using Buffalo.Kernel;
using Buffalo.DB.DataBaseAdapter;
using Buffalo.DB.MessageOutPuters;
using Buffalo.DB.DbCommon;

namespace Buffalo.DB.CacheManager
{
    /// <summary>
    /// 系统内存的缓存适配器
    /// </summary>
    public class MemoryAdaper : ICacheAdaper 
    {
        public MemoryAdaper(DBInfo info) 
        {
            _info = info;
        }

        private Hashtable _cache = Hashtable.Synchronized(new Hashtable());
        private Hashtable _hsToKey = Hashtable.Synchronized(new Hashtable());
        private DBInfo _info;
        /// <summary>
        /// 数据库信息
        /// </summary>
        public DBInfo Info
        {
            get { return _info; }
        }
        /// <summary>
        /// 设置数据
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="sql">SQL名</param>
        /// <param name="dt">数据</param>
        /// <returns></returns>
        public bool SetData(IDictionary<string, bool> tableNames, string sql, DataSet ds, DataBaseOperate oper) 
        {
            string key = GetKey(sql);
            ArrayList sqlItems = null;
            //添加表对应的SQL语句值
            foreach (KeyValuePair<string, bool> kvptableName in tableNames)
            {
                string tableName = kvptableName.Key;
                sqlItems = _hsToKey[tableName] as ArrayList;
                if (sqlItems == null)
                {

                    sqlItems = ArrayList.Synchronized(new ArrayList());
                    _hsToKey[tableName] = sqlItems;

                }

                sqlItems.Add(key);

            }
            if (_info.SqlOutputer.HasOutput)
            {
                OutPutMessage(QueryCache.CommandSetDataSet, sql,oper);
            }
            _cache[key] = ds;
            
            return true;
        }
        /// <summary>
        /// 获取表名
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private string GetTableName(string tableName)
        {
            StringBuilder sbInfo = new StringBuilder(tableName.Length + 10);
            sbInfo.Append(_info.Name);
            sbInfo.Append(".");
            sbInfo.Append(tableName);
            return sbInfo.ToString();
        }

        /// <summary>
        /// 生成表名对应的Key
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns></returns>
        private  string GetTableKeyName(string tableName)
        {
            StringBuilder sbRet = new StringBuilder(tableName.Length + 20);
            sbRet.Append("___Table:");
            sbRet.Append(GetTableName(tableName));
            return sbRet.ToString();
        }
        /// <summary>
        /// 通过SQL获取键
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        private string GetKey(string sql) 
        {
            StringBuilder sbKey = new StringBuilder(100);
            sbKey.Append("BuffaloCache:");
            sbKey.Append(sql.Length );
            sbKey.Append(":");
            sbKey.Append(PasswordHash.ToMD5String(_info.Name+":"+sql));
            return sbKey.ToString();
        }

        /// <summary>
        /// 根据SQL语句从缓存中找出数据
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="tableNames">表名集合</param>
        /// <returns></returns>
        public  DataSet GetData(IDictionary<string, bool> tableNames, string sql, DataBaseOperate oper)
        {
            string key = GetKey(sql);
            if (_info.SqlOutputer.HasOutput)
            {
                OutPutMessage(QueryCache.CommandGetDataSet, sql,oper);
            }
            return _cache[key] as DataSet;
        }

        /// <summary>
        /// 通过SQL删除某项
        /// </summary>
        /// <param name="sql"></param>
        public void RemoveBySQL(IDictionary<string, bool> tableNames, string sql, DataBaseOperate oper) 
        {
            string key = GetKey(sql);
            
            _cache.Remove(key);
            if (_info.SqlOutputer.HasOutput)
            {
                OutPutMessage(QueryCache.CommandDeleteSQL, sql,oper);
            }

        }
        /// <summary>
        /// 通过表名删除关联项
        /// </summary>
        /// <param name="sql"></param>
        public void RemoveByTableName(string tableName, DataBaseOperate oper)
        {
            ArrayList sqlItems = _hsToKey[tableName] as ArrayList;

            if (sqlItems != null)
            {


                foreach (object okey in sqlItems)
                {
                    string key = okey as string;
                    if (!string.IsNullOrEmpty(key))
                    {
                        _cache.Remove(key);
                    }
                }
                sqlItems.Clear();

            }
            if (_info.SqlOutputer.HasOutput)
            {
                OutPutMessage(QueryCache.CommandDeleteTable, tableName,oper);
            }
        }

        private void OutPutMessage(string type, string message, DataBaseOperate oper)
        {
            oper.OutMessage(MessageType.QueryCache, "SystemMemory", type, message);
        }

        #region ICacheAdaper 成员


        public IDictionary<string, object> GetValues(string[] keys, DataBaseOperate oper)
        {
            Dictionary<string, object> dic = new Dictionary<string, object>(keys.Length);
            foreach (string str in keys) 
            {
                object val = _cache[str];
                dic[str] = val;
            }
            return dic;
        }

        public void SetValue<E>(string key, E value, DataBaseOperate oper)
        {
            _cache[key] = value;
        }

        public void DeleteValue(string key, DataBaseOperate oper)
        {
            _cache.Remove(key);
        }

        public void DoIncrement(string key, DataBaseOperate oper)
        {
            lock (_cache)
            {
                object oval = _cache[key];
                if (oval == null)
                {
                    oval = 1;
                }
                long value = Convert.ToInt64(oval);
                value++;
                _cache[key] = value;
            }
        }

        #endregion
    }

    
}
