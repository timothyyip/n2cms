using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Reflection;
using N2.Definitions;
using N2.Details;
using N2.Engine;
using N2.Persistence.NH;
using N2.Security;
using N2.Serialization;
using N2.Web;
using NHibernate;
using NHibernate.Driver;
using NHibernate.Tool.hbm2ddl;
using Environment=NHibernate.Cfg.Environment;
using N2.Persistence;

namespace N2.Installation
{
    /// <summary>
    /// Wraps functionality to request database status and generate n2's 
    /// database schema on multiple database flavours.
    /// </summary>
	public class InstallationManager
	{
        IConfigurationBuilder configurationBuilder;
        IDefinitionManager definitions;
        Importer importer;
        IPersister persister;
        ISessionProvider sessionProvider;
        IHost host;

        public InstallationManager(IHost host, IDefinitionManager definitions, Importer importer, IPersister persister, ISessionProvider sessionProvider, IConfigurationBuilder configurationBuilder)
		{
            this.host = host;
            this.definitions = definitions;
            this.importer = importer;
            this.persister = persister;
            this.sessionProvider = sessionProvider;
            this.configurationBuilder = configurationBuilder;
		}
        NHibernate.Cfg.Configuration cfg;
        protected NHibernate.Cfg.Configuration Cfg
        {
            get { return cfg ?? (cfg = configurationBuilder.BuildConfiguration()); }
        }

		public static string GetResourceString(string resourceKey)
		{
			Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceKey);
			StreamReader sr = new StreamReader(s);
			return sr.ReadToEnd();
		}

		#region Methods

        /// <summary>Upgrades to the latest version of N2</summary>
        public void Upgrade(Version fromFileVersion)
        {
            throw new N2Exception("Cannot upgrade.");
            //long oneWayToDoIt = fromFileVersion.Major * 1000000
            //                    + fromFileVersion.MajorRevision * 1000
            //                    + fromFileVersion.Minor;
            //if (oneWayToDoIt < 25000 || oneWayToDoIt > 1100000)
            //    throw new N2Exception("Can only upgrade from version 0.25 through 1.0.37");

            //ExecuteSqlResource(UpgradeSqlServer2005_1_1);
        }

		/// <summary>Executes sql create database scripts.</summary>
		public void Install()
		{
			SchemaExport exporter = new SchemaExport(Cfg);
			exporter.Create(true, true);
		}

		public void ExportSchema(TextWriter output)
		{
			SchemaExport exporter = new SchemaExport(Cfg);
			exporter.Execute(true, false, false, true, null, output);
		}

		public void DropDatabaseTables()
		{
			SchemaExport exporter = new SchemaExport(Cfg);
			exporter.Drop(true, true);
		}

		public DatabaseStatus GetStatus()
		{
			DatabaseStatus status = new DatabaseStatus();

			UpdateConnection(status);
			UpdateSchema(status);
			UpdateItems(status);

			return status;
		}

		private void UpdateItems(DatabaseStatus status)
		{
			try
			{
				status.StartPageID = host.DefaultSite.StartPageID;
                status.RootItemID = host.DefaultSite.RootItemID;
				status.StartPage = persister.Get(status.StartPageID);
				status.RootItem = persister.Get(status.RootItemID);
				status.IsInstalled = status.RootItem != null && status.StartPage != null;
			} 
			catch (Exception ex)
			{
				status.IsInstalled = false;
				status.ItemsError = ex.Message;
			}
		}

		private void UpdateSchema(DatabaseStatus status)
		{
			try
			{
				using (sessionProvider)
				{
                    ISession session = sessionProvider.GetOpenedSession();
					status.Items = session.CreateCriteria(typeof (ContentItem)).List().Count;
					status.Details = session.CreateCriteria(typeof(ContentDetail)).List().Count;
					status.DetailCollections = session.CreateCriteria(typeof(DetailCollection)).List().Count;
					status.AuthorizedRoles = session.CreateCriteria(typeof(AuthorizedRole)).List().Count;
				}
				status.HasSchema = true;
			}
			catch(Exception ex)
			{
				status.HasSchema = false;
				status.SchemaError = ex.Message;
				status.SchemaException = ex;
			}
		}

		private void UpdateConnection(DatabaseStatus status)
		{
			try
			{
				using (IDbConnection conn = GetConnection())
				{
					conn.Open();
					conn.Close();
					status.ConnectionType = conn.GetType().Name;
				}
				status.IsConnected = true;
				status.ConnectionError = null;
			}
			catch(Exception ex)
			{
				status.IsConnected = false;
				status.ConnectionError = ex.Message;
				status.ConnectionException = ex;
			}
		}

		/// <summary>Method that will checks the database. If something goes wrong an exception is thrown.</summary>
		/// <returns>A string with diagnostic information about the database.</returns>
		public string CheckDatabase()
		{
            using (sessionProvider)
			{
                ISession session = sessionProvider.GetOpenedSession();
				int itemCount = session.CreateCriteria(typeof (ContentItem)).List().Count;
				int detailCount = session.CreateCriteria(typeof (ContentDetail)).List().Count;
				int allowedRoleCount = session.CreateCriteria(typeof (AuthorizedRole)).List().Count;
				int detailCollectionCount = session.CreateCriteria(typeof (DetailCollection)).List().Count;
				return string.Format("Database OK, items: {0}, details: {1}, allowed roles: {2}, detail collections: {3}",
				                     itemCount, detailCount, allowedRoleCount, detailCollectionCount);
			}
		}

		/// <summary>Checks the root node in the database. Throws an exception if there is something really wrong with it.</summary>
		/// <returns>A diagnostic string about the root node.</returns>
		public string CheckRootItem()
		{
			int rootID = host.DefaultSite.RootItemID;
			ContentItem rootItem = persister.Get(rootID);
			if (rootItem != null)
				return String.Format("Root node OK, id: {0}, name: {1}, type: {2}, discriminator: {3}, published: {4} - {5}",
									 rootItem.ID, rootItem.Name, rootItem.GetType(),
									 definitions.GetDefinition(rootItem.GetType()), rootItem.Published, rootItem.Expires);
			else
				return "No root item found with the id: " + rootID;
		}

		/// <summary>Checks the root node in the database. Throws an exception if there is something really wrong with it.</summary>
		/// <returns>A diagnostic string about the root node.</returns>
		public string CheckStartPage()
		{
			int startID = host.DefaultSite.StartPageID;
			ContentItem startPage = persister.Get(startID);
			if(startPage != null)
				return String.Format("Root node OK, id: {0}, name: {1}, type: {2}, discriminator: {3}, published: {4} - {5}",
									 startPage.ID, startPage.Name, startPage.GetType(),
									 definitions.GetDefinition(startPage.GetType()), startPage.Published, startPage.Expires);
			else
				return "No start page found with the id: " + startID;
		}

		public ContentItem InsertRootNode(Type type, string name, string title)
		{
			ContentItem item = definitions.CreateInstance(type, null);
			item.Name = name;
			item.Title = title;
			persister.Save(item);
			return item;
		}

		public ContentItem InsertStartPage(Type type, ContentItem root, string name, string title)
		{
			ContentItem item = definitions.CreateInstance(type, root);
			item.Name = name;
			item.Title = title;
			persister.Save(item);
			return item;
		}

		#region Helper Methods

		private void ExecuteCommandWithGoSplitter(string sql)
		{
			using (IDbConnection conn = GetConnection())
			{
				conn.Open();
				IDbCommand cmd = conn.CreateCommand();
				cmd.CommandType = CommandType.Text;
				int startPos = 0;

				do
				{
					int lastPos = sql.IndexOf("GO", startPos);
					int len = (lastPos > startPos ? lastPos : sql.Length) - startPos;
					string sqlPart = sql.Substring(startPos, len);

					if (sqlPart.Trim().Length > 0)
					{
						cmd.CommandText = sqlPart;
						cmd.ExecuteNonQuery();
					}

					if (lastPos == -1)
						break;
					else
						startPos = lastPos + 2;
				} while (startPos < sql.Length);
			}
		}

		private void ExecuteSqlResource(string resourceKey)
		{
			string sql = GetResourceString(resourceKey);
			ExecuteCommandWithGoSplitter(sql);
		}

		public IDbConnection GetConnection()
		{
			string driverName = (string) Cfg.Properties[Environment.ConnectionDriver];
			Type driverType = NHibernate.Util.ReflectHelper.ClassForName(driverName);
			IDriver driver = (IDriver) Activator.CreateInstance(driverType);

			IDbConnection conn = driver.CreateConnection();
			if (Cfg.Properties.ContainsKey(Environment.ConnectionString))
				conn.ConnectionString = (string)Cfg.Properties[Environment.ConnectionString];
			else if (Cfg.Properties.ContainsKey(Environment.ConnectionStringName))
				conn.ConnectionString = ConfigurationManager.ConnectionStrings[(string)Cfg.Properties[Environment.ConnectionStringName]].ConnectionString;
			else
				throw new Exception("Didn't find a confgiured connection string or connection string name in the nhibernate configuration.");
			return conn;
		}

		#endregion

		public ContentItem InsertExportFile(Stream stream, string filename)
		{
			IImportRecord record = importer.Read(stream, filename);
            importer.Import(record, null, ImportOption.All);

			return record.RootItem;
		}
		#endregion
	}
}