using Patholab_DAL_V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssutaContainers
{
    public class Nautilus2Container
    {
        private DataLayer _dal;

        public Nautilus2Container(DataLayer _dal)
        {
            // TODO: Complete member initialization
            this._dal = _dal;
        }

        internal void Run()
        {

            Program.log("2nd phase. Get New Messages");

            var msg4Nautilus = _dal.GetAll<U_CONTAINER_MSG_USER>().Where(x => x.U_RECEIVING_STATUS == "N");
            Program.log(msg4Nautilus.Count() + " Messages Found.");

            foreach (var item in msg4Nautilus)
            {


                //Option a (insert)
                InsertContainer(item);

            }
        }
        private void InsertContainer(U_CONTAINER_MSG_USER item)
        {
            // U_CONTAINER newContainer = null;
            var Assuta_Prototype = _dal.FindBy<U_CONTAINER>
                (c => c.NAME == "Assuta Prototype").FirstOrDefault();
            if (Assuta_Prototype != null)
            {
                var id = _dal.GetNewId("sq_u_container");
                var NEWNam = _dal.GetDynamicStr("  SELECT LIMS.CONTAINER_EXT_SYNTAX FROM DUAL");
                var YEAR = DateTime.Now.Year - 2000;
                long NewId = Convert.ToInt64(id);
                var newContainer = new U_CONTAINER()
                {
                    U_CONTAINER_ID = NewId
                    ,
                    NAME = NEWNam,

                    VERSION = Assuta_Prototype.VERSION,
                    VERSION_STATUS = Assuta_Prototype.VERSION_STATUS,
                    TEMPLATE_ID = Assuta_Prototype.TEMPLATE_ID,
                    WORKFLOW_NODE_ID = Assuta_Prototype.WORKFLOW_NODE_ID,
                    EVENTS = Assuta_Prototype.EVENTS,
                    DESCRIPTION = item.U_CONTAINER_MSG_ID.ToString() //Assuta_Prototype.NAME //item.U_MSG_NAME

                };

                U_CONTAINER_USER newContainerUser = new U_CONTAINER_USER()
                {
                    U_CONTAINER_ID = NewId,
                    U_CREATE_BY = 1,
                    U_RECEIVE_NUMBER = item.U_MSG_NAME,
                    U_CLINIC = item.U_CLINIC_ID,
                    U_NUMBER_OF_SAMPLES = item.U_CONTAINER_MSG.U_CONTAINER_MSG_ROW_USER.Count,
                    //U_DRIVER_ID = item.U_DRIVER_ID,
                    U_DRIVER_NAME = item.U_DRIVER_NAME,
                    U_SEND_ON = item.U_PACKED_ON,
                    U_RECEIVED_ON = DateTime.Now,
                    U_REQUESTS = GetRequests(item),
                    U_STATUS = Assuta_Prototype.U_CONTAINER_USER.U_STATUS //Ashi 1/8/21 Take initial status from propotype

                };
                item.U_RECEIVING_STATUS = "R";
                _dal.Add(newContainer);
                newContainer.U_CONTAINER_USER = newContainerUser;
                _dal.SaveChanges();
                Program.log(newContainer.NAME + " Added , Receive Number " + newContainerUser.U_RECEIVE_NUMBER);

            }
        }
        private string GetRequests(U_CONTAINER_MSG_USER parentMsg)
        {
            string seq = "";
            var children = parentMsg.U_CONTAINER_MSG.U_CONTAINER_MSG_ROW_USER;
            if (children != null && children.Count > 0)
            {
                foreach (var item in children)
                {
                    seq += item.U_REQUEST + ";";
                }


            }
            return seq;
        }

    }
}
