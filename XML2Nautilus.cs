using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AssutaContainersHelpers;
using System.Threading.Tasks;
using Patholab_DAL_V1;
using Patholab_Common;

namespace AssutaContainers
{
    public class XML2Nautilus
    {

        private DataLayer _dal;

        public XML2Nautilus(Patholab_DAL_V1.DataLayer _dal)
        {
            // TODO: Complete member initialization
            this._dal = _dal;
        }

        internal void Run()
        {


            Program.log("1st phase. Searching xml in directory " + Program.InputPath);

            string xmlPath = Program.InputPath;
            //Get XML from folder
            var files = Directory.GetFiles(xmlPath, "*.xml");
            Program.log(files.Count() + " XML were found.");
            foreach (var item in files)
            {
                try
                {
                    string xml = File.ReadAllText(item);

                    string filename = Path.GetFileName(item);
                    if (!filename.Contains("AS") || !filename.Contains("_"))
                    {
                        throw new System.ArgumentException("File does not contain 'AS' or\\and '_' in the name");
                    }
                    var xmlObj = xml.ParseXML<MAIN>();
                    Program.log("XML object created");

                    //Populates nautilus from XML
                    var NewIdNbr = InsertContainerMSG(xmlObj);

                    var newDest = MoveFile(Program.OuputPath, item);

                    UpdateAsSuccess(NewIdNbr, newDest);
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
                {
                    //Testing section
                    Exception raise = dbEx;
                    foreach (var validationErrors in dbEx.EntityValidationErrors)
                    {
                        foreach (var validationError in validationErrors.ValidationErrors)
                        {
                            string message = string.Format("{0}:{1}",
                                validationErrors.Entry.Entity.ToString(),
                                validationError.ErrorMessage);
                            Program.log("Exception is: " + message);
                            Program.log(dbEx);
                        }
                    }

                }
                catch (Exception ex)
                {
                    Program.log("Error working on file : " + item);
                    Program.log(ex);
                }
            }
        }
        #region Add to DB
        private long InsertContainerMSG(MAIN msg)
        {
            var id = _dal.GetNewId("SQ_U_CONTAINER_MSG");
            long NewId = Convert.ToInt64(id);
            U_CONTAINER_MSG req = new U_CONTAINER_MSG()
            {
                NAME = NewId.ToString(),
                U_CONTAINER_MSG_ID = NewId,
                VERSION = "1",
                VERSION_STATUS = "A"
            };
            decimal temp;
            // typecast either 'temp' or 'null'
            decimal? numericValue =
              decimal.TryParse(msg.TRCONTNUM, out temp) ? temp : (decimal?)null;
            string status = msg.TRCONTSTS.ToString();
            long? sender = GetSenderClinic(msg.TRHOSCODE);
            DateTime? date = GetDate(msg.XMLDATE, msg.XMLHR);
            DateTime? pckdate = GetDate(msg.TRDATEPICK, msg.TRTIMEPICK, "ddMMyyyyHmm");

            string driver = msg.TRDRIVER;


            string errors = "";
            U_CONTAINER_MSG_USER reqUser = new U_CONTAINER_MSG_USER()
            {
                U_CONTAINER_MSG_ID = NewId,
                U_MSG_DATE = date,//Rename to msg_date in db
                U_CONTAINER_NBR = temp,
                U_STATUS = status,
                //U_SENDER = sender,//todo delete from db
                U_CLINIC_ID = sender,
                U_MSG_NAME = msg.XMLNAME,
                U_PACKED_ON = pckdate,
                U_CREATED_ON = DateTime.Now,
                U_ERRORS = errors,
                U_DRIVER_NAME = driver


            };
            var msg_rows = msg.TRREQNUM;
            errors = GetErrors(sender, msg.TRHOSCODE, date, reqUser.U_MSG_NAME, msg_rows);
            reqUser.U_ERRORS = errors;
            if (!string.IsNullOrEmpty(reqUser.U_ERRORS))
            {
                reqUser.U_RECEIVING_STATUS = "H";
            }
            else
            {
                reqUser.U_RECEIVING_STATUS = "N";
            }
            Program.log("Change status to " + reqUser.U_RECEIVING_STATUS);
            req.U_CONTAINER_MSG_USER = reqUser;
            _dal.Add(req);
            //_dal.Add(reqUser);
            Program.log("U_CONTAINER_MSG_USER added with id " + NewId + " and Reception number " + reqUser.U_MSG_NAME);

            foreach (var item in msg.TRREQNUM)
            {
                InsertContainerMSG_Entry(item, NewId);
            }
            Program.log("Commit Changes");

            _dal.SaveChanges();

            return NewId;
        }
        private void InsertContainerMSG_Entry(MAINTRANSPORT xmlRow, long headerId)
        {
            var rowId = _dal.GetNewId("SQ_U_CONTAINER_MSG_ROW");
            long NewrowId = Convert.ToInt64(rowId);
            U_CONTAINER_MSG_ROW NautRow = new U_CONTAINER_MSG_ROW()
            {
                NAME = NewrowId.ToString(),
                U_CONTAINER_MSG_ROW_ID = NewrowId,
                VERSION = "1",
                VERSION_STATUS = "A"
            };
            DateTime? date = GetDate(xmlRow.TRXMLDATE, xmlRow.TRXMLTIME);

            U_CONTAINER_MSG_ROW_USER NautRowUser = new U_CONTAINER_MSG_ROW_USER()
            {
                U_CONTAINER_MSG_ROW_ID = NewrowId,
                U_REQUEST = xmlRow.TRREQUEST.ToString(),
                U_MSG_ID = headerId,
                U_PACKED_ON = date

            };

            Program.log("U_CONTAINER_MSG_ROW_USER added with id " + NewrowId + " and barcode " + NautRowUser.U_REQUEST);
            NautRow.U_CONTAINER_MSG_ROW_USER = NautRowUser;
            _dal.Add(NautRow);
            //_dal.Add(NautRowUser);
        }
        #endregion


        private string GetErrors(long? sender, string clinicID, DateTime? date, string containerNbr, AssutaContainers.MAINTRANSPORT[] msg_rows)
        {
            string errors = "";
            if (!sender.HasValue)
            {
                errors += "No Clinic number:" + clinicID + "; ";
            }
            if (!date.HasValue)
            {
                errors += "No Date; ";
            }
            //var decimalNbr = Convert.ToDecimal(containerNbr);
            var msg_row = _dal.FindBy<U_CONTAINER_MSG_USER>(x => x.U_MSG_NAME == containerNbr).FirstOrDefault();
            if (msg_row != null)
            {
                errors += "Container Msg number " + msg_row.U_CONTAINER_NBR + " Already exsists; ";
            }
            var conUser = _dal.FindBy<U_CONTAINER_USER>(x => x.U_RECEIVE_NUMBER == containerNbr).FirstOrDefault();
            if (conUser != null)
            {
                errors += "Container number " + conUser.U_RECEIVE_NUMBER + " already exsists; ";
            }
            string msgError = "";
            string sampleError = "";
            foreach (var msg in msg_rows)
            {
                var s = msg.TRREQUEST.ToString();
                var row = _dal.FindBy<U_CONTAINER_MSG_ROW_USER>(x => x.U_REQUEST == s).FirstOrDefault();
                if (row != null)
                {
                    if (msgError == "")
                    {
                        msgError += "Msg row: " + msg.TRREQUEST.ToString();
                    }
                    else
                    {
                        msgError += ", " + msg.TRREQUEST.ToString();
                    }

                }


                //var sample = _dal.FindBy<SAMPLE_USER>(x => x.U_ASSUTA_NUMBER == s && x.SAMPLE.STATUS != "X" && x.SAMPLE.STATUS != "U" && x.SAMPLE.STATUS != "V").FirstOrDefault();
                //if (sample != null)
                //{
                //    if (sampleError == "")
                //    {
                //        sampleError += "Assuta number: " + msg.TRREQUEST.ToString() + " Sample Name - " + sample.U_PATHOLAB_SAMPLE_NAME;
                //    }
                //    else
                //    {
                //        sampleError += ", " + msg.TRREQUEST.ToString() +" Sample Name - " + sample.U_PATHOLAB_SAMPLE_NAME;
                //    }
                //}
            }
            if (msgError != "")
            {
                msgError += " already exsist";
            }
            if (sampleError != "")
            {
                sampleError += " already exsist";
            }
            errors += msgError;
            errors += sampleError;
            return errors;
        }

        private void UpdateAsSuccess(long NewIdNbr, string newDest)
        {

            U_CONTAINER_MSG_USER msg = _dal.FindBy<U_CONTAINER_MSG_USER>(x => x.U_CONTAINER_MSG_ID == NewIdNbr).FirstOrDefault();
            if (msg != null)
            {
                msg.U_PATH = newDest;
            }
            _dal.SaveChanges();
        }

        public DirectoryInfo GetCreateMyFolder(string baseFolder)
        {
            var now = DateTime.Now;
            var yearName = now.ToString("yyyy");
            var monthName = now.ToString("MM");
            var dayName = now.ToString("dd-MM-yyyy");

            var folder = Path.Combine(baseFolder, Path.Combine(yearName, monthName));

            return Directory.CreateDirectory(folder);
        }

        private string MoveFile(string xmlDir, string file)
        {

            string NameWithoutExtension = Path.GetFileNameWithoutExtension(file);
            FileInfo f = new FileInfo(file);
            var bb = GetCreateMyFolder(xmlDir);
            var newDest = Path.Combine(bb.FullName, NameWithoutExtension + "-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".xml");
            File.Move(file, newDest);
            Program.log(string.Format("File moved from {0} to {1} ", file, newDest));
            return newDest;
        }

        #region Conversoins
        DateTime? GetDate(string date, string time, string format = "ddMMyyyyHmmss")
        {
            try
            {
                var dttimefull = DateTime.ParseExact(date + time, format, null);
                return dttimefull;
            }
            catch (Exception)
            {
                return null;
            }
        }

        long? GetSenderClinic(string msgCode)
        {


            var clinic = _dal.GetAll<U_CLINIC_USER>().FirstOrDefault(x => x.U_ASSUTA_DIVISION_CODE == msgCode && x.U_SENDER == "T");
            if (clinic != null)
            {
                return clinic.U_CLINIC_ID;

            }
            return null;
        }
        #endregion

    }
}
