using Microsoft.Win32;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using AssutaContainers;
using AssutaContainersHelpers;
using Patholab_DAL_V1;

namespace AssutaContainers
{
    public class Program
    {

        private static DataLayer _dal;
        public static string NautConStr, InputPath, OuputPath;
        private static string LogPath;

        static void Main(string[] args)
        {


            try
            {




                //Setup
                SetAppSettings();

                //Create Connection
                _dal = new DataLayer();
                _dal.MockConnect(NautConStr);
                XML2Nautilus xml2nautilus = new XML2Nautilus(_dal);
                xml2nautilus.Run();

                _dal = new DataLayer();
                _dal.MockConnect(NautConStr);
                Nautilus2Container na2con = new Nautilus2Container(_dal);
                na2con.Run();

                _dal.Close();
                _dal = null;
           


            }
            catch (Exception ex)
            {
                Program.log("Program Failed");
                Program.log(ex);

            }
            finally
            {
                if (_dal != null)
                {


                    _dal.Close();
                    _dal = null;
                }    
                Program.log("Finished on " + DateTime.Now);
                Program.log("******************************************/n******************************************/n/n/");
            }


        }
        private static void SetAppSettings()
        {
            NautConStr = ConfigurationManager.ConnectionStrings["NautConnectionString"].ConnectionString;
            InputPath = ConfigurationManager.AppSettings["InputPath"];
            OuputPath = ConfigurationManager.AppSettings["OuputPath"];
            LogPath = ConfigurationManager.AppSettings["LogPath"];
            Program.log("Started on " + DateTime.Now);

            Program.log(string.Format("Connection string = {0} \nxml Input = {1} \nxml Output = {2}"
    , NautConStr, InputPath, OuputPath));

        }
        public static void log(string s)
        {
            try
            {
                using (FileStream file = new FileStream(LogPath + DateTime.Now.ToString("dd-MM-yyyy") + ".log", FileMode.Append, FileAccess.Write))
                {
                    var streamWriter = new StreamWriter(file);
                    streamWriter.WriteLine(s);
                    streamWriter.Close();
                }
            }
            catch
            {
            }
        }

        public static void log(Exception ex)
        {

            Program.log(ex.Message);
            if (ex.InnerException != null)
            {
                Program.log("StackTrace " + ex.InnerException.StackTrace + "Data " + ex.InnerException.Data + "Source " + ex.InnerException.Source);
            }
        }
        private static void Exit(string p)
        {
        }


    }

}
