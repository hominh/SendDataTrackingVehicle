using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Threading;
using System.IO;
using System.Threading;
using MySql.Data.MySqlClient;
using System.Text.RegularExpressions;
using ProtoBuf;
using RabbitMQ.Client;
using ufms;
using log4net;

namespace ServiceSendDataGSHT
{
    public partial class ServiceSendData : ServiceBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceSendData));
        private static ConnectionFactory factory;
        private static IConnection connection;
        private static IModel channel;
        public ServiceSendData()
        {
            InitializeComponent();
        }

        static void Main(string[] args)
        {
            ServiceSendData service = new ServiceSendData();
            if (Environment.UserInteractive)
            {
                try
                {
                    TextWriter file = new StreamWriter("D:\\startsend.txt", true);
                    file.WriteLine("Service Started");
                    service.OnStart(args);

                }
                catch (Exception ex)
                {
                    TextWriter file2 = new StreamWriter("D:\\startsenderror.txt", true);
                    file2.WriteLine(ex.Message);
                }
                Console.WriteLine("Press any key to stop program");
                Console.Read();
                service.OnStop();
            }
            else
            {
                ServiceBase[] servicesToRun = new ServiceBase[]
                {
                    new ServiceSendData()
                };
                try
                {
                    ServiceBase.Run(servicesToRun);

                }
                catch (Exception ex)
                {
                    service.WriteLog("ex5: " + ex.Message);
                }
            }
        }

        public void SendData()
        {

            DateTime dateTime = DateTime.UtcNow.Date;
            String now = dateTime.ToString("yyyy-MM-dd") + " 00:00:00";
            WriteLog("Start SendData");


            

            try
            {
                WayPoint wp = new WayPoint();
                using (MySqlConnection connMysql = DBUtils.GetDBConnection())
                {
                    WriteLog("Try connect db");
                    connMysql.Open();
                    Reconnect();
                    while (true)
                    {
                        WriteLog("try factory.CreateConnection to DRVN");

                        //Mat mang thi chet tu day
                        //IConnection conn = CreateConnection();
                        /////////////////////////////////////

                        //IModel channel = conn.CreateModel();
                        //bool status = conn.IsOpen;
                        //WriteLog("status connect drvn: " + status.ToString());
                        
                        WriteLog("Start connect db");
                        try
                        {

                            string sql = "SELECT DISTINCT(carlog_id), YEAR(carlog_thoigian) as year,MONTH(carlog_thoigian) as month,DAY(carlog_thoigian) as day, HOUR(carlog_thoigian) as hour,MINUTE(carlog_thoigian) as minute, SECOND(carlog_thoigian) as second, carlog_bienso,carlog_kinhdo,carlog_vido,carlog_vantoc_tb,carlog_laixe_uid FROM tbl_carlogsentdrvn LEFT JOIN tbl_laixe ON carlog_laixe_uid = laixe_uid   WHERE carlog_thoigian != '0000-00-00 00:00:00' AND carlog_sentdrvn  = 0  AND  carlog_thoigian >=  '" + now + "'    ORDER BY carlog_thoigian ASC LIMIT 0,1000";

                            WriteLog(sql);

                            MySqlCommand cmd = new MySqlCommand(sql, connMysql);
                            string carlogid = "";
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {

                                string carlogbienso = "";
                                string carlogbiensoorigin = "";
                                double carlogx;
                                double carlogy;
                                int year, month, day, hour, minute, second;
                                if (reader.HasRows == true)
                                {
                                    WriteLog("HasRows");
                                    while (reader.Read())
                                    {

                                        carlogid += reader.GetString("carlog_id");
                                        carlogid += ",";

                                        carlogbiensoorigin = reader.GetString("carlog_bienso").ToString();
                                        carlogbienso = string.Concat(reader.GetString("carlog_bienso").Where(char.IsLetterOrDigit));
                                        carlogx = reader.GetDouble("carlog_kinhdo");
                                        carlogy = reader.GetDouble("carlog_vido");


                                        year = reader.GetInt32("year");
                                        month = reader.GetInt32("month");
                                        day = reader.GetInt32("day");
                                        hour = reader.GetInt32("hour");
                                        minute = reader.GetInt32("minute");
                                        second = reader.GetInt32("second");
                                        wp.datetime = DateTimeToUnixTimestamp(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local));
                                        wp.door = false; // Dong mo cua xe ,Chua co trong db

                                        //wp.driver = "123451234512345";
                                        if (reader["carlog_laixe_uid"] == DBNull.Value || reader.GetString("carlog_laixe_uid") == "")
                                        {
                                            wp.driver = "123451234512345";
                                        }
                                        else
                                        {
                                            wp.driver = reader.GetString("carlog_laixe_uid");
                                        }
                                        wp.ignition = true; // So may,Chua co trong db
                                        wp.speed = reader.GetFloat("carlog_vantoc_tb");
                                        wp.vehicle = carlogbienso;
                                        wp.x = carlogx;
                                        wp.y = carlogy;
                                        BaseMessage msg = new BaseMessage();
                                        msg.msgType = BaseMessage.MsgType.WayPoint;
                                        Extensible.AppendValue<WayPoint>(msg, ufms.BaseMessage.MsgType.WayPoint.GetHashCode(), wp);
                                        byte[] b = this.Serialize(msg);

                                        channel.BasicPublish("tracking.ctchaulong", "track1", null, b);
                                        WriteLog("-Sent data: BS:  " + wp.vehicle.ToString() + "-Id: " + reader.GetString("carlog_id") + "- datetime: " + wp.datetime + "- wp.door: " + wp.door + "- wp.driver: " + wp.driver + "-wp.ignition: " + wp.ignition + "-wp.speed:" + wp.speed + "-wp.vehicle: " + wp.vehicle + "-wp.x: " + carlogx + "-wp.y: " + wp.y);
                                    }
                                }
                            }
                            Console.WriteLine("carlogid.Length.ToString(): " + carlogid.Length.ToString());
                            if (carlogid.Length > 0)
                            {
                                carlogid = carlogid.Substring(0, carlogid.Length - 1);
                                Console.WriteLine("carlogid : " + carlogid);
                                string sqlDelete = "DELETE FROM tbl_carlogsentdrvn WHERE carlog_id IN (" + carlogid + ") ";
                                WriteLog(sqlDelete);
                                MySqlCommand commandDelete = connMysql.CreateCommand();
                                commandDelete.CommandText = sqlDelete;
                                commandDelete.ExecuteNonQuery();

                                string sqlUpdate = "UPDATE tbl_carlog SET carlog_sentdrvn = 1 WHERE carlog_id IN (" + carlogid + ")";
                                WriteLog(sqlUpdate);
                                MySqlCommand commandUpdate = connMysql.CreateCommand();
                                commandUpdate.CommandText = sqlUpdate;
                                commandUpdate.ExecuteNonQuery();
                                
                            }
                            Thread.Sleep(10000);
                            //Reconnect();


                        }
                        catch (Exception ex)
                        {
                            WriteLog("ex connect db: " + ex.Message);
                            connMysql.Close();
                            connMysql.Open();
                            //RestartService();
                            Thread.Sleep(1000);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog("ex connect drvn: " + ex.ToString());
                //Cleanup();
                Reconnect();
                //RestartService();
            }
        }


        public void RestartService()
        {
            WriteLog("Jump Restart service");
            ServiceController service = new ServiceController("ServiceSendData");

            //WriteLog("Restart service");
            WriteLog("Service status: " + service.Status.ToString());

            if ((service.Status.Equals(ServiceControllerStatus.Running)) || (service.Status.Equals(ServiceControllerStatus.StartPending)))
            {
                service.Stop();
                WriteLog("Stoped service");
            }
            service.WaitForStatus(ServiceControllerStatus.Stopped);
            service.Start();
            service.WaitForStatus(ServiceControllerStatus.Running);
            service.Start();
            WriteLog("Service status now: " + service.Status.ToString());
            WriteLog("Restart service done");
        }

        private IConnection CreateConnection()
        {
            var inifle = new IniFile(@"C:\Release\config.ini");
            int port;
            string username = inifle.Read("DRVN_USERNAME").ToString();
            string password = inifle.Read("DRVN_PASSWORD").ToString();
            string virtualhost = inifle.Read("DRVN_VIRTUALHOST").ToString();
            string hostname = inifle.Read("DRVN_HOSTNAME").ToString();
            var strport = inifle.Read("DRVN_PORT");
            bool convertportoint = Int32.TryParse(strport, out port);

            WriteLog("username: " + username + "-password: " + password + "-virtualhost: " + virtualhost + "-hostname:" + hostname + "-port" + port.ToString());
            ConnectionFactory factory = new ConnectionFactory()
            {
                HostName = hostname,
                UserName = username,
                Password = password,
                VirtualHost = "/",
                Port = port,
            };

            factory.RequestedHeartbeat = 30;

            IConnection connection = factory.CreateConnection();
            return connection;
        }

        private void connect()
        {
            connection = CreateConnection();
            connection.ConnectionShutdown += Connection_ConnectionShutdown;
            channel = connection.CreateModel();
        }

        private void Connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            WriteLog("Connection broke!");
            Reconnect();
        }

        private void Reconnect()
        {
            Cleanup();
            var mres = new ManualResetEventSlim(false);
            while (!mres.Wait(10000))
            {
                try
                {
                    connect();
                    WriteLog("connected");
                    mres.Set();
                }
                catch(Exception ex)
                {
                    WriteLog("ex Reconnect: " + ex.ToString());
                }
            }
        }

        private void Cleanup()
        {
            try
            {
                if (channel != null && channel.IsOpen)
                {
                    channel.Close();
                    channel = null;
                }

                if (connection != null && connection.IsOpen)
                {
                    connection.Close();
                    connection = null;
                }
            }
            catch (IOException ex)
            {
                WriteLog("ex clean up: "+ex.ToString());
            }
        }

        public void WriteLog(string strLog)
        {
            StreamWriter log;
            FileStream fileStream = null;
            DirectoryInfo logDirInfo = null;
            FileInfo logFileInfo;

            string logFilePath = "C:\\Logs\\";
            logFilePath = logFilePath + System.DateTime.Today.ToString("MM-dd-yyyy") + "." + "log";
            logFileInfo = new FileInfo(logFilePath);
            logDirInfo = new DirectoryInfo(logFileInfo.DirectoryName);
            if (!logDirInfo.Exists) logDirInfo.Create();
            if (!logFileInfo.Exists)
            {
                fileStream = logFileInfo.Create();
            }
            else
            {
                fileStream = new FileStream(logFilePath, FileMode.Append);
            }
            log = new StreamWriter(fileStream);
            DateTime now = DateTime.Now;
            strLog = now.ToString("yyyy-MM-dd HH:mm:ss.fff: ") + strLog;
            log.WriteLine(strLog);
            log.Close();
        }


        public byte[] Serialize(BaseMessage wp)
        {
            byte[] b = null;
            using (var ms = new MemoryStream())
            {
                Serializer.Serialize<BaseMessage>(ms, wp);
                b = new byte[ms.Position];
                var fullB = ms.GetBuffer();
                Array.Copy(fullB, b, b.Length);
            }
            return b;
        }
        public BaseMessage DeSerialize(byte[] bytes)
        {
            BaseMessage msg;
            using (var ms = new MemoryStream(bytes))
            {
                msg = Serializer.Deserialize<BaseMessage>(ms);
            }

            return msg;
        }
        public int DateTimeToUnixTimestamp(DateTime dateTime)
        {
            TimeSpan span = (dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));
            double unixTime = span.TotalSeconds;
            return (int)unixTime; // 1371802570000
        }

        public DateTime UnixTimeStampToDateTime(int unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp);
            return dtDateTime.ToLocalTime();
        }

        protected override void OnStart(string[] args)
        {
            WriteLog("Service is started");
            try
            {
                Thread t = new Thread(new ThreadStart(SendData)); // e.g.
                t.Start();

            }
            catch (Exception ex)
            {
                WriteLog("ex Onstart: " + ex.Message);
            }
        }

        protected override void OnStop()
        {
            WriteLog("Service is stopped");

        }
    }
}
