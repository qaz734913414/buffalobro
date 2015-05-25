using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Reflection;
using System.Diagnostics;
using Buffalo.DB.CommBase;
using Buffalo.DB.CommBase.DataAccessBases;
using Buffalo.DB.DbCommon;
using Buffalo.Kernel.Defaults;
using Buffalo.DB.EntityInfos;
using Buffalo.Kernel.FastReflection;
using System.IO;
using Buffalo.Kernel;
using Buffalo.DB.CacheManager;

namespace Buffalo.DB.DataBaseAdapter
{
    /// <summary>
    /// 数据访问层的加载读取类
    /// </summary>
    public class DataAccessLoader
    {
        
        private static Dictionary<string, Type> _dicLoaderConfig =null;//配置记录集合
        private static Dictionary<string, Type> _dicEntityLoaderConfig = null;//配置实体记录集合
        private static Dictionary<string, DBInfo> _dicDBInfo = new Dictionary<string, DBInfo>();//配置实体记录集合
        private static Dictionary<string, Type> _dicBoLoader = null;//配置实体业务层集合
        /// <summary>
        /// 是否已经初始化
        /// </summary>
        internal static bool HasInit 
        {
            get 
            {
                return _dicLoaderConfig != null;
            }
        }

        /// <summary>
        /// 获取数据库信息
        /// </summary>
        /// <param name="dbName">数据库名</param>
        /// <returns></returns>
        public static DBInfo GetDBInfo(string dbName) 
        {
            if (_dicDBInfo == null)
            {
                return null;
            }
            DBInfo ret=null;
            _dicDBInfo.TryGetValue(dbName, out ret);
            return ret;
        }

        /// <summary>
        /// 获取随机一个数据库信息
        /// </summary>
        /// <returns></returns>
        internal static DBInfo GetFristDBInfo() 
        {
            if (_dicDBInfo == null)
            {
                return null;
            }
            foreach (KeyValuePair<string, DBInfo> kvp in _dicDBInfo) 
            {
                return kvp.Value;
            }
            return null;
        }

        #region 初始化配置
        /// <summary>
        /// 添加数据库信息
        /// </summary>
        /// <param name="dbinfo"></param>
        public static void AppendDBInfo(DBInfo dbinfo) 
        {
            _dicDBInfo[dbinfo.Name] = dbinfo;
        }
        

        /// <summary>
        /// 初始化配置
        /// </summary>
        internal static bool InitConfig()
        {
            if (HasInit) 
            {
                return true;
            }
            _dicLoaderConfig = new Dictionary<string, Type>();
            _dicEntityLoaderConfig = new Dictionary<string, Type>();
            _dicBoLoader = new Dictionary<string, Type>();
            Dictionary<string, XmlDocument> dicEntityConfig = new Dictionary<string, XmlDocument>();

            List<ConfigInfo> docs = ConfigXmlLoader.LoadXml("Buffalo.Config");
            if (docs.Count <= 0) 
            {
                docs = ConfigXmlLoader.LoadXml("DataAccessConfig");
            }
            if (docs.Count > 0)
            {
                
                DBInfo existsInfo = null;
                foreach (ConfigInfo doc in docs)
                {
                    XmlDocument docInfo = doc.Document;
                    DBInfo dbinfo = GetDBInfo(docInfo);
                    if (!_dicDBInfo.TryGetValue(dbinfo.Name, out existsInfo))
                    {
                        _dicDBInfo[dbinfo.Name] = dbinfo;
                    }
                    else 
                    {
                        if (!existsInfo.ConnectionString.Equals(dbinfo.ConnectionString)) 
                        {
                            throw new Exception("同名数据库:" + dbinfo.Name + "，的连接字符串不同");
                        }
                        if (!existsInfo.DbType.Equals(existsInfo.DbType)) 
                        {
                            throw new Exception("同名数据库:" + dbinfo.Name + "，的数据库类型不同");
                        }
                    }
                    
                }
                
            }
            if (_dicDBInfo.Count == 0) 
            {
                StringBuilder exMess = new StringBuilder();
                exMess.Append("没有配置数据库信息，请检查");
                if (CommonMethods.IsWebContext) 
                {
                    exMess.Append("web.config");
                }else
                {
                    exMess.Append("app.config");
                }
                exMess.Append("的appSettings中是否有 Buffalo.Config 或 DataAccessConfig 节点");
                throw new Exception(exMess.ToString());
            }

            LoadModel();
            return true;
        }

        /// <summary>
        /// 获取当前配置文件的数据库信息
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        public static DBInfo GetDBInfo(XmlDocument doc) 
        {
            string dbType = null;
            string connectionString = null;
            string name = null;
            //string output = null;
            string[] attNames = null;
            ICacheAdaper ica=null;
            string cacheType = null;
            string cacheConn = null;
            LazyType lazy=LazyType.Enable;
            //Dictionary<string, string> extendDatabaseConnection = new Dictionary<string,string>();
            if (doc == null)
            {
                throw new Exception("找不到配置文件");
            }
            bool isAlltable = false ;
            XmlNodeList lstConfig = doc.GetElementsByTagName("config");
            if (lstConfig.Count > 0)
            {
                XmlNode node = lstConfig[0];
                foreach (XmlAttribute att in node.Attributes)
                {
                    if (att.Name.Equals("dbType",StringComparison.CurrentCultureIgnoreCase))
                    {
                        dbType = att.InnerText;
                    }
                    //else if (att.Name.Equals("output", StringComparison.CurrentCultureIgnoreCase))
                    //{
                    //    output = att.InnerText;
                    //}
                    else if (att.Name.Equals("name",StringComparison.CurrentCultureIgnoreCase))
                    {
                        name=att.InnerText;
                    }
                    else if (att.Name.Equals("connectionString",StringComparison.CurrentCultureIgnoreCase))
                    {
                        connectionString = att.InnerText;
                    }
                    else if (att.Name.Equals("appnamespace",StringComparison.CurrentCultureIgnoreCase)) 
                    {
                        string names=att.InnerText;

                        if (!string.IsNullOrEmpty(names))
                        {
                            attNames = names.Split(new char[] { '|' });
                        }
                        for (int i = 0; i < attNames.Length; i++) 
                        {
                            string attName = attNames[i];
                            if (!attName.EndsWith(".")) 
                            {
                                attNames[i] = attName + ".";
                            }
                        }
                    }
                    else if (att.Name.Equals("cache", StringComparison.CurrentCultureIgnoreCase)) 
                    {
                        cacheType = att.InnerText;
                    }
                    else if (att.Name.Equals("cacheConnString", StringComparison.CurrentCultureIgnoreCase))
                    {
                        cacheConn = att.InnerText;
                    }
                    else if (att.Name.Equals("allCache", StringComparison.CurrentCultureIgnoreCase))
                    {
                        isAlltable = att.InnerText=="1";
                    }
                    else if (att.Name.Equals("lazy", StringComparison.CurrentCultureIgnoreCase))
                    {
                        int ilazy=0;
                        if(int.TryParse(att.InnerText, out ilazy))
                        {
                            lazy=(LazyType)ilazy;
                        }
                    }
                }
            }
            else
            {
                throw new Exception("配置文件没有config节点");
            }
            
            DBInfo info = new DBInfo(name, connectionString, dbType,lazy);
            
            ica = QueryCache.GetCache(info,cacheType, cacheConn);

            info.SetQueryCache(ica,isAlltable);
            
            info.DataaccessNamespace=attNames;
            return info;
        }

        

        
        /// <summary>
        /// 加载模块信息
        /// </summary>
        private static void LoadModel()
        {
            List<Assembly> lstAss = GetAllAssembly();
            Dictionary<string, EntityConfigInfo> dicAllEntityInfo = new Dictionary<string, EntityConfigInfo>();//实体信息
            
            foreach (Assembly ass in lstAss) 
            {
                
                string[] resourceNames = ass.GetManifestResourceNames();
                foreach (string name in resourceNames)
                {
                    if (name.EndsWith(".BEM.xml", StringComparison.CurrentCultureIgnoreCase))
                    {
                        try
                        {
                            Stream stm = ass.GetManifestResourceStream(name);
                            XmlDocument doc = new XmlDocument();
                            doc.Load(stm);

                            //获取类名
                            XmlNodeList lstNode = doc.GetElementsByTagName("class");
                            if (lstNode.Count > 0)
                            {
                                XmlNode classNode = lstNode[0];
                                XmlAttribute att = classNode.Attributes["ClassName"];
                                if (att != null)
                                {
                                    string className = att.InnerText;
                                    if (!string.IsNullOrEmpty(className))
                                    {
                                        Type cType = ass.GetType(className);

                                        EntityConfigInfo info = new EntityConfigInfo();
                                        info.Type = cType;
                                        info.ConfigXML = doc;
                                        dicAllEntityInfo[className] = info;

                                    }
                                }
                                
                            }
                        }
                        catch
                        {

                        }
                    }
                    else if (name.EndsWith(".BDM.xml", StringComparison.CurrentCultureIgnoreCase))
                    {
                        Stream stm = ass.GetManifestResourceStream(name);
                        XmlDocument doc = new XmlDocument();
                        doc.Load(stm);
                        AppendDalLoader(ass, doc);
                        AppendBoLoader(ass, doc);
                    }
                }
            }
            EntityInfoManager.InitAllEntity(dicAllEntityInfo);

        }
        /// <summary>
        /// 添加到数据层
        /// </summary>
        /// <param name="doc"></param>
        private static void AppendDalLoader(Assembly ass, XmlDocument doc) 
        {
            XmlNodeList nodes = doc.GetElementsByTagName("dataaccess");
            if (nodes.Count <= 0) 
            {
                return;
            }
            XmlAttribute att = nodes[0].Attributes["name"];
            if (att == null) 
            {
                return;
            }
            string name = att.InnerText;
            DBInfo db = null;
            if (!_dicDBInfo.TryGetValue(name, out db)) 
            {
                return;
            }
            string[] namespaces = db.DataaccessNamespace;
            XmlNodeList dalNodes = nodes[0].ChildNodes;
            foreach (XmlNode dalNode in dalNodes) 
            {
                att = dalNode.Attributes["type"];
                if (att == null) 
                {
                    continue;
                }
                string typeName = att.InnerText;
                foreach (string allNameSpace in namespaces) 
                {
                    if (typeName.StartsWith(allNameSpace)) 
                    {
                        Type dalType = ass.GetType(typeName);
                        if (dalType != null) 
                        {
                            att = dalNode.Attributes["interface"];
                            if (att == null)
                            {
                                break;
                            }
                            _dicLoaderConfig[att.InnerText] = dalType;

                            Type[] gTypes = DefaultType.GetGenericType(dalType, true);
                            if (gTypes != null && gTypes.Length > 0)
                            {
                                Type gType = gTypes[0];
                                _dicEntityLoaderConfig[gType.FullName] = dalType;
                            }
                        }

                        break;
                    }
                }
            }
        }
        /// <summary>
        /// 添加到业务层
        /// </summary>
        /// <param name="doc"></param>
        private static void AppendBoLoader(Assembly ass, XmlDocument doc)
        {
            XmlNodeList nodes = doc.GetElementsByTagName("business");
            if (nodes.Count <= 0)
            {
                return;
            }
            XmlNodeList dalNodes = nodes[0].ChildNodes;
            foreach (XmlNode dalNode in dalNodes)
            {
                XmlAttribute att = dalNode.Attributes["type"];
                if (att == null)
                {
                    continue;
                }
                string typeName = att.InnerText;
                Type boType = ass.GetType(typeName);
                if (boType != null)
                {
                    att = dalNode.Attributes["class"];
                    if (att == null)
                    {
                        break;
                    }
                    _dicBoLoader[att.InnerText] = boType;
                }
            }
        }

        private static List<Assembly> _modelAssembly = new List<Assembly>();

        /// <summary>
        /// 添加要处理的程序集
        /// </summary>
        /// <param name="ass">程序集</param>
        public static void AppendModelAssembly(Assembly ass) 
        {
            
            string key=ass.FullName;
            foreach (Assembly curAss in _modelAssembly) 
            {
                string curKey = curAss.FullName;
                if (key == curKey) 
                {
                    return;
                }
            }
            _modelAssembly.Add(ass);
        }
        /// <summary>
        /// 获取本模块下所有程序集
        /// </summary>
        /// <returns></returns>
        private static List<Assembly> GetAllAssembly() 
        {
            return _modelAssembly;
        }

        #endregion

        /// <summary>
        /// 根据实体类型获取对应的数据访问层的实例
        /// </summary>
        /// <param name="entityType">实体类型</param>
        /// <returns></returns>
        public static object GetInstanceByEntity(Type entityType, DataBaseOperate oper)
        {
            //InitConfig();
            Type objType = null;
            if (!_dicEntityLoaderConfig.TryGetValue(entityType.FullName, out objType))
            {
                throw new Exception("找不到对应的类型" + entityType.FullName+"的配置，请检查配置文件");
            }
            return Activator.CreateInstance(objType, oper);
        }

        /// <summary>
        /// 根据实体类型获取对应的数据访问层的实例
        /// </summary>
        /// <param name="entityType">实体类型</param>
        /// <returns></returns>
        public static IDataAccessModel<T> GetInstanceByEntity<T>(DataBaseOperate oper)
                    where T : EntityBase, new()
        {

            return GetInstanceByEntity(typeof(T),oper) as IDataAccessModel<T>;
        }
        /// <summary>
        /// 根据实体类型获取对应的数据访问层的实例
        /// </summary>
        /// <param name="entityType">实体类型</param>
        /// <returns></returns>
        public static IViewDataAccess<T> GetViewInstanceByEntity<T>(DataBaseOperate oper)
                    where T : EntityBase, new()
        {
            return GetInstanceByEntity(typeof(T), oper) as IViewDataAccess<T>;
        }
        /// <summary>
        /// 根据实体类型获取对应的数据访问层的实例
        /// </summary>
        /// <param name="interfaceType">实体类型</param>
        /// <returns></returns>
        public static object GetInstance(Type interfaceType, DataBaseOperate oper) 
        {
            //InitConfig();
            Type objType = null;
            if (!_dicLoaderConfig.TryGetValue(interfaceType.FullName, out objType)) 
            {
                throw new Exception("找不到接口" + interfaceType.FullName + "的对应配置，请检查配置文件");
            }
            return Activator.CreateInstance(objType, oper);
        }
       
        /// <summary>
        /// 根据实体类型获取对应的数据访问层的实例
        /// </summary>
        /// <param name="DataBaseOperate">链接</param>
        /// <returns></returns>
        public static T GetInstance<T>(DataBaseOperate oper)
        {
            return (T)GetInstance(typeof(T),oper);
        }



        /// <summary>
        /// 根据实体类型获取对应的数据访问层的实例
        /// </summary>
        /// <param name="entityType">实体类型</param>
        /// <returns></returns>
        public static object GetInstanceByEntity(Type entityType)
        {
            //InitConfig();
            Type objType = null;
            if (!_dicEntityLoaderConfig.TryGetValue(entityType.FullName, out objType))
            {
                throw new Exception("找不到对应的类型");
            }
            return Activator.CreateInstance(objType);
        }

        /// <summary>
        /// 根据实体类型获取对应的数据访问层的实例
        /// </summary>
        /// <param name="entityType">实体类型</param>
        /// <returns></returns>
        public static IDataAccessModel<T> GetInstanceByEntity<T>()
                    where T : EntityBase, new()
        {

            return GetInstanceByEntity(typeof(T)) as IDataAccessModel<T>;
        }
        /// <summary>
        /// 根据实体类型获取对应的数据访问层的实例
        /// </summary>
        /// <param name="entityType">实体类型</param>
        /// <returns></returns>
        public static IViewDataAccess<T> GetViewInstanceByEntity<T>()
                    where T : EntityBase, new()
        {
            return GetInstanceByEntity(typeof(T)) as IViewDataAccess<T>;
        }
        /// <summary>
        /// 根据实体类型获取对应的数据访问层的实例
        /// </summary>
        /// <param name="interfaceType">实体类型</param>
        /// <returns></returns>
        public static object GetInstance(Type interfaceType)
        {
            //InitConfig();
            Type objType = null;
            if (!_dicLoaderConfig.TryGetValue(interfaceType.FullName, out objType))
            {
                throw new Exception("找不到对应的类型");
            }
            return Activator.CreateInstance(objType);
        }

        /// <summary>
        /// 获取业务层的类型
        /// </summary>
        /// <param name="classTypeName">实体类型名</param>
        /// <returns></returns>
        public static Type GetBoType(string classTypeName) 
        {
            Type objType = null;
            if (!_dicBoLoader.TryGetValue(classTypeName, out objType))
            {
                throw new Exception("找不到对应的类型");
            }
            return objType;
        }
        /// <summary>
        ///  获取业务层的实例
        /// </summary>
        /// <param name="classTypeName">实体类型名</param>
        /// <returns></returns>
        public static object GetBoInstance(string classTypeName) 
        {
            Type type = GetBoType(classTypeName);
            if (type == null) 
            {
                return null;
            }
            return Activator.CreateInstance(type);
        }

        /// <summary>
        /// 根据实体类型获取对应的数据访问层的实例
        /// </summary>
        /// <param name="DataBaseOperate">链接</param>
        /// <returns></returns>
        public static T GetInstance<T>()
        {
            return (T)GetInstance(typeof(T));
        }
    }
}
