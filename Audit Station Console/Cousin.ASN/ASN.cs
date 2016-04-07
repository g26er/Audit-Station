//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Text;
//using System.IO;
using MySql.Data.MySqlClient;

namespace Cousin.ASN
{
    public class ASN
    {
        /* 
         * order_header status key
         * 1 = new
         * 2 = auditing
         * 3 = finished
         */

        public enum Customer
        {
            MichaelsCA,
            MichaelsUS,
            WalMart,
            WalmartCA,
            Joann,
            Hancock,
            Amazon,
            AmazonCanada,//Added by Jason 4-22-13
            Meijer,
            Acmoore,
        }

        //MySqlConnection asnConn = new MySqlConnection();
        //MySqlCommand asnCommand = new MySqlCommand();
        //MySqlDataReader asnReader;

        public ASN()
        {
            //asnCommand.Connection = asnConn;
            //asnConn.ConnectionString = "server=192.168.123.24;user id=audit;password=audit;database=audit";
        }

        public int GetNumBoxes_Order(Customer myCust, string orderNumber)
        {
            using (MySqlConnection connGNBO = new MySqlConnection("server=192.168.123.24;user id=audit;password=audit;database=audit"))
            {
                using (MySqlCommand commandGNBO = connGNBO.CreateCommand())
                {
                    int numBoxes;

                    commandGNBO.CommandText = "select max(box_number) from audit_box where order_number = ?ORDERNUMBER";
                    commandGNBO.Parameters.Clear();
                    commandGNBO.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                    //commandGNBO.Parameters.Add("?ORDERNUMBER", orderNumber);
                    connGNBO.Open();
                    MySqlDataReader readerGNBO = commandGNBO.ExecuteReader();
                    readerGNBO.Read();
                    numBoxes = readerGNBO.GetInt32(0);

                    return numBoxes;
                }
            }
        }

        private bool CheckSSCCExists(Customer myCust, string orderNumber, int boxNumber)
        {
            int ssccCount = 0;

            MySqlConnection checkSSCCConn = new MySqlConnection("server=192.168.123.24;user id=audit;password=audit;database=audit");
            MySqlCommand checkSSCCCommand = checkSSCCConn.CreateCommand();
            MySqlDataReader checkSSCCReader;

            checkSSCCCommand.CommandText = "select count(ucc_128) from sscc_shipping_number where order_number = ?ORDERNUMBER and box_number = ?BOXNUMBER";
            checkSSCCCommand.Parameters.Clear();
            checkSSCCCommand.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
            checkSSCCCommand.Parameters.AddWithValue("?BOXNUMBER", boxNumber);

            if (checkSSCCConn.State != System.Data.ConnectionState.Closed)
            {
                checkSSCCConn.Close();
            }
            checkSSCCConn.Open();
            checkSSCCReader = checkSSCCCommand.ExecuteReader();
            checkSSCCReader.Read();
            ssccCount = checkSSCCReader.GetInt32(0);
            checkSSCCConn.Close();

            if (ssccCount == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
 
        private void SSCCNumber(Customer myCust, string orderNumber, int boxNumber)
        {
            string ssccNumber;
            string barcode;
            string ssccNineDigit;
            //int ssccCount;
            int shippingNumber;
            int oddNum;
            int evenNum;
            int checkDigit;

            MySqlConnection ssccConn = new MySqlConnection("server=192.168.123.24;user id=audit;password=audit;database=audit");
            MySqlCommand ssccCommand = ssccConn.CreateCommand();
            MySqlDataReader ssccReader;
            if (CheckSSCCExists(myCust, orderNumber, boxNumber))
            {
                ssccCommand.CommandText = "select ucc_128 from sscc_shipping_number where order_number = ?ORDERNUMBER and box_number = ?BOXNUMBER";
                ssccCommand.Parameters.Clear();
                ssccCommand.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                ssccCommand.Parameters.AddWithValue("?BOXNUMBER", boxNumber);

                if (ssccConn.State != System.Data.ConnectionState.Closed)
                {
                    ssccConn.Close();
                }
                ssccConn.Open();
                ssccReader = ssccCommand.ExecuteReader();
                ssccReader.Read();
                ssccNumber = ssccReader.GetString(ssccReader.GetOrdinal("ucc_128"));
                ssccConn.Close();
            }
            else
            {
                //insert for new shipping_number (auto increment) to make ucc_128
                ssccCommand.CommandText = "INSERT INTO sscc_shipping_number (order_number,box_number) VALUES (?ORDERNUMBER,?BOXNUMBER)";
                ssccCommand.Parameters.Clear();
                ssccCommand.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                ssccCommand.Parameters.AddWithValue("?BOXNUMBER", boxNumber);

                if (ssccConn.State != System.Data.ConnectionState.Closed)
                {
                    ssccConn.Close();
                }
                ssccConn.Open();
                ssccCommand.ExecuteNonQuery();
                ssccConn.Close();

                //get shipping_number that was just made
                ssccCommand.CommandText = "select shipping_number from sscc_shipping_number where order_number = ?ORDERNUMBER and box_number = ?BOXNUMBER";
                ssccCommand.Parameters.Clear();
                ssccCommand.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                ssccCommand.Parameters.AddWithValue("?BOXNUMBER", boxNumber);

                if (ssccConn.State != System.Data.ConnectionState.Closed)
                {
                    ssccConn.Close();
                }
                ssccConn.Open();
                ssccReader = ssccCommand.ExecuteReader();
                ssccReader.Read();
                shippingNumber = ssccReader.GetInt32(ssccReader.GetOrdinal("shipping_number"));
                ssccConn.Close();

                //create barcode with check digit
                ssccNineDigit = shippingNumber.ToString().PadLeft(9, '0');
                barcode = "000016321" + ssccNineDigit;

                oddNum = (int)barcode[1] + (int)barcode[3] + (int)barcode[5] + (int)barcode[7] + (int)barcode[9] + (int)barcode[11] + (int)barcode[13] + (int)barcode[15] + (int)barcode[17];
                evenNum = (int)barcode[2] + (int)barcode[4] + (int)barcode[6] + (int)barcode[8] + (int)barcode[10] + (int)barcode[12] + (int)barcode[14] + (int)barcode[16];
                checkDigit = (10 - ((oddNum * 3) + evenNum) % 10);
                if (checkDigit == 10)
                {
                    checkDigit = 0;
                }
                ssccNumber = "0" + barcode + checkDigit;

                //add SSCCNumber to DB
                ssccCommand.CommandText = "UPDATE sscc_shipping_number SET ucc_128 = ?SSCCNUMBER WHERE shipping_number = ?SHIPPINGNUMBER";
                ssccCommand.Parameters.Clear();
                ssccCommand.Parameters.AddWithValue("?SSCCNUMBER", ssccNumber);
                ssccCommand.Parameters.AddWithValue("?SHIPPINGNUMBER", shippingNumber);
                if (ssccConn.State != System.Data.ConnectionState.Closed)
                {
                    ssccConn.Close();
                }
                ssccConn.Open();
                ssccCommand.ExecuteNonQuery();
                ssccConn.Close();
            }
            //return ssccNumber.Substring(0, 19);
        }

        public void PrintBarcodeLabels(Customer myCust, string orderNumber, int boxNumber)
        {
            PrintBarcodeLabels(myCust, orderNumber, boxNumber, "no");
        }

        public void PrintBarcodeLabels(Customer myCust, string orderNumber, int boxNumber, string upcNumber)
        {

            using (MySqlConnection connPrintLabel = new MySqlConnection("server=192.168.123.24;user id=audit;password=audit;database=audit"))
            {
                using (MySqlCommand commandPrintLabel = connPrintLabel.CreateCommand())
                {
                    MySqlDataReader readerPrintLabel;

                    BarTender.ApplicationClass btApp;
                    BarTender.Format btFormat;

                    btApp = new BarTender.ApplicationClass();

                    string shipmentID = "";
                    int myNumBoxes = boxNumber;

                    string cartonQty = "";
                    string vendorItem = "";
                    string skuNum = "";
                    string numCartons = "";

                    switch (myCust)
                    {
                        case Customer.Acmoore:
                            //Acmoore
                            boxNumber = 0;

                            for (int i = 1; i <= myNumBoxes; i++)
                            {
                                SSCCNumber(myCust, orderNumber, i);
                            }

                            commandPrintLabel.CommandText = @"SELECT audit_shipment.*, audit_header.*, audit_box_header.*, sscc_shipping_number.*
                            FROM ((audit_shipment INNER JOIN audit_header ON audit_shipment.shipment_id = audit_header.shipment_id) INNER JOIN audit_box_header ON audit_header.order_number = audit_box_header.order_number) INNER JOIN sscc_shipping_number ON (audit_box_header.order_number = sscc_shipping_number.order_number) AND (audit_box_header.box_number = sscc_shipping_number.box_number)
                            WHERE sscc_shipping_number.order_number=?ORDERNUMBER
                            ORDER BY sscc_shipping_number.box_number";

                            commandPrintLabel.Parameters.Clear();
                            commandPrintLabel.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                            connPrintLabel.Open();
                            readerPrintLabel = commandPrintLabel.ExecuteReader();

                            btFormat = btApp.Formats.Open(@"\\cca\apps\labels\acmoore.btw", false, "");
                            btFormat.Printer = "METO";

                            while (readerPrintLabel.Read())
                            {
                                string addressLine3 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_city")) + ", " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_state")) + " " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_zip"));
                                string cityState = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_city")) + ", " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_state"));
                                string ucc128 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ucc_128")).Substring(0, 19);
                                shipmentID = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("shipment_id"));
                                boxNumber = readerPrintLabel.GetInt32(readerPrintLabel.GetOrdinal("box_number"));

                                btFormat.SetNamedSubStringValue("ship_line1", (string)readerPrintLabel["ship_to_name"]);
                                //btFormat.SetNamedSubStringValue("ship_line1", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_name")));

                                btFormat.SetNamedSubStringValue("ship_line2", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address1")));
                                btFormat.SetNamedSubStringValue("ship_line3", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address2")));
                                btFormat.SetNamedSubStringValue("ship_line4", addressLine3);

                                btFormat.SetNamedSubStringValue("po_num", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("po_number")));
                                //btFormat.SetNamedSubStringValue("event_code", " ");
                                //btFormat.SetNamedSubStringValue("box_num", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("box_number")));
                                //btFormat.SetNamedSubStringValue("box_total", myNumBoxes.ToString());
                                btFormat.SetNamedSubStringValue("dept", readerPrintLabel.GetValue(readerPrintLabel.GetOrdinal("department_number")).ToString());
                                btFormat.SetNamedSubStringValue("desc", readerPrintLabel.GetValue(readerPrintLabel.GetOrdinal("product_group")).ToString());
                                //btFormat.SetNamedSubStringValue("city_state", cityState.ToString());
                                btFormat.SetNamedSubStringValue("city_state", readerPrintLabel.GetValue(readerPrintLabel.GetOrdinal("ship_to_location_id")).ToString());
                                btFormat.SetNamedSubStringValue("store_num", readerPrintLabel.GetValue(readerPrintLabel.GetOrdinal("store_number")).ToString());
                                btFormat.SetNamedSubStringValue("sscc", ucc128);

                                btFormat.PrintOut(false, false);
                            }
                            btFormat.Close(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            btApp.Quit(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            break;



                        case Customer.Joann:
                            //Joann
                            boxNumber = 0;

                            for (int i = 1; i <= myNumBoxes; i++)
                            {
                                SSCCNumber(myCust, orderNumber, i);
                            }

                            commandPrintLabel.CommandText = @"SELECT audit_shipment.*, audit_header.*, audit_box_header.*, sscc_shipping_number.*
                            FROM ((audit_shipment INNER JOIN audit_header ON audit_shipment.shipment_id = audit_header.shipment_id) INNER JOIN audit_box_header ON audit_header.order_number = audit_box_header.order_number) INNER JOIN sscc_shipping_number ON (audit_box_header.order_number = sscc_shipping_number.order_number) AND (audit_box_header.box_number = sscc_shipping_number.box_number)
                            WHERE sscc_shipping_number.order_number=?ORDERNUMBER
                            ORDER BY sscc_shipping_number.box_number";

                            commandPrintLabel.Parameters.Clear();
                            commandPrintLabel.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                            connPrintLabel.Open();
                            readerPrintLabel = commandPrintLabel.ExecuteReader();

                            btFormat = btApp.Formats.Open(@"\\cca\apps\labels\joann.btw", false, "");
                            btFormat.Printer = "METO";

                            while (readerPrintLabel.Read())
                            {
                                string addressLine3 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_city")) + ", " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_state")) + " " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_zip"));
                                string ucc128 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ucc_128")).Substring(0, 19);
                                shipmentID = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("shipment_id"));
                                boxNumber = readerPrintLabel.GetInt32(readerPrintLabel.GetOrdinal("box_number"));

                                btFormat.SetNamedSubStringValue("ship_line1", (string)readerPrintLabel["ship_to_name"]);
                                //btFormat.SetNamedSubStringValue("ship_line1", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_name")));

                                btFormat.SetNamedSubStringValue("ship_line2", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address1")));
                                btFormat.SetNamedSubStringValue("ship_line3", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address2")));
                                btFormat.SetNamedSubStringValue("ship_line4", addressLine3);

                                btFormat.SetNamedSubStringValue("po_num", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("po_number")));
                                //btFormat.SetNamedSubStringValue("event_code", " ");
                                btFormat.SetNamedSubStringValue("box_num", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("box_number")));
                                btFormat.SetNamedSubStringValue("box_total", myNumBoxes.ToString());
                                //btFormat.SetNamedSubStringValue("store_num", readerPrintLabel.GetValue(readerPrintLabel.GetOrdinal("store_number")).ToString());
                                btFormat.SetNamedSubStringValue("sscc", ucc128);

                                btFormat.PrintOut(false, false);
                            }
                            btFormat.Close(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            btApp.Quit(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            break;

//                        case Customer.WalmartCA:
//                            //Wal-Mart Canada
//                            SSCCNumber(myCust, orderNumber, boxNumber);

//                            commandPrintLabel.CommandText = @"SELECT audit_shipment.*, audit_header.*, sscc_shipping_number.*
//                                FROM (audit_shipment INNER JOIN audit_header ON audit_shipment.shipment_id = audit_header.shipment_id) 
//                                INNER JOIN sscc_shipping_number ON (audit_header.order_number = sscc_shipping_number.order_number) 
//                                WHERE sscc_shipping_number.order_number=?ORDERNUMBER and sscc_shipping_number.box_number=?BOXNUMBER";

//                            commandPrintLabel.Parameters.Clear();
//                            commandPrintLabel.Parameters.Add("?ORDERNUMBER", orderNumber);
//                            commandPrintLabel.Parameters.Add("?BOXNUMBER", boxNumber);
//                            connPrintLabel.Open();
//                            readerPrintLabel = commandPrintLabel.ExecuteReader();

//                            btFormat = btApp.Formats.Open(@"\\cca\apps\labels\walmartcanada.btw", false, "");
//                            btFormat.Printer = "METO";

//                            while (readerPrintLabel.Read())
//                            {
//                                string shipToAddressCode = (string)readerPrintLabel["ship_to_address_code"];

//                                string addressLine3 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_city")) + ", " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_state")) + " " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_zip"));
//                                string ucc128 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ucc_128")).Substring(0, 19);
//                                shipmentID = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("shipment_id"));
//                                boxNumber = readerPrintLabel.GetInt32(readerPrintLabel.GetOrdinal("box_number"));

//                                btFormat.SetNamedSubStringValue("ship_line1", (string)readerPrintLabel["ship_to_name"]);

//                                btFormat.SetNamedSubStringValue("ship_line2", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address1")));
//                                btFormat.SetNamedSubStringValue("ship_line3", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address2")));
//                                btFormat.SetNamedSubStringValue("ship_line4", addressLine3);
//                                btFormat.SetNamedSubStringValue("sequence1", shipmentID);
//                                btFormat.SetNamedSubStringValue("ordernumber", (string)readerPrintLabel["order_number"].ToString());
//                                btFormat.SetNamedSubStringValue("dc_num", shipToAddressCode);
//                                btFormat.SetNamedSubStringValue("type", (string)readerPrintLabel["type"]);
//                                btFormat.SetNamedSubStringValue("dept1", (string)readerPrintLabel["dept"]);
//                                btFormat.SetNamedSubStringValue("zip", (string)readerPrintLabel["ship_to_zip"]);
//                                btFormat.SetNamedSubStringValue("store_num", (string)readerPrintLabel["store_number"]);
//                                btFormat.SetNamedSubStringValue("vendor", (string)readerPrintLabel["vendor_number"]);
//                                btFormat.SetNamedSubStringValue("sscc", ucc128.ToString());
//                                btFormat.PrintOut(false, false);
//                            }
//                            btFormat.Close(BarTender.BtSaveOptions.btDoNotSaveChanges);
//                            btApp.Quit(BarTender.BtSaveOptions.btDoNotSaveChanges);
//                            break;

                        case Customer.MichaelsCA:
                            //Michaels Canada
                            boxNumber = 0;

                            for (int i = 1; i <= myNumBoxes; i++)
                            {
                                SSCCNumber(myCust, orderNumber, i);
                            }

                            commandPrintLabel.CommandText = @"SELECT audit_shipment.*, audit_header.*, audit_box_header.*, sscc_shipping_number.*
                            FROM ((audit_shipment INNER JOIN audit_header ON audit_shipment.shipment_id = audit_header.shipment_id) INNER JOIN audit_box_header ON audit_header.order_number = audit_box_header.order_number) INNER JOIN sscc_shipping_number ON (audit_box_header.order_number = sscc_shipping_number.order_number) AND (audit_box_header.box_number = sscc_shipping_number.box_number)
                            WHERE sscc_shipping_number.order_number=?ORDERNUMBER
                            ORDER BY sscc_shipping_number.box_number";

                            commandPrintLabel.Parameters.Clear();
                            commandPrintLabel.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                            connPrintLabel.Open();
                            readerPrintLabel = commandPrintLabel.ExecuteReader();

                            btFormat = btApp.Formats.Open(@"\\cca\apps\labels\michaels.btw", false, "");
                            btFormat.Printer = "METO";

                            while (readerPrintLabel.Read())
                            {
                                string addressLine3 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_city")) + ", " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_state")) + " " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_zip"));
                                string ucc128 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ucc_128")).Substring(0, 19);
                                shipmentID = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("shipment_id"));
                                boxNumber = readerPrintLabel.GetInt32(readerPrintLabel.GetOrdinal("box_number"));

                                btFormat.SetNamedSubStringValue("ship_line1", (string)readerPrintLabel["ship_to_name"]);
                                //btFormat.SetNamedSubStringValue("ship_line1", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_name")));

                                btFormat.SetNamedSubStringValue("ship_line2", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address1")));
                                btFormat.SetNamedSubStringValue("ship_line3", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address2")));
                                btFormat.SetNamedSubStringValue("ship_line4", addressLine3);

                                btFormat.SetNamedSubStringValue("po_num", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("po_number")));
                                btFormat.SetNamedSubStringValue("event_code", " ");
                                btFormat.SetNamedSubStringValue("box_num", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("box_number")));
                                btFormat.SetNamedSubStringValue("box_total", myNumBoxes.ToString());
                                btFormat.SetNamedSubStringValue("store_num", readerPrintLabel.GetValue(readerPrintLabel.GetOrdinal("store_number")).ToString());
                                btFormat.SetNamedSubStringValue("sscc", ucc128);

                                btFormat.PrintOut(false, false);
                            }
                            btFormat.Close(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            btApp.Quit(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            break;

                        case Customer.WalMart:
                        //Wal-Mart
                            SSCCNumber(myCust, orderNumber, boxNumber);
                            /*
                             * commandPrintLabel.CommandText = @"SELECT audit_shipment.*, audit_header.*, audit_box_header.*, sscc_shipping_number.*
                             * FROM ((audit_shipment INNER JOIN audit_header ON audit_shipment.shipment_id = audit_header.shipment_id) INNER JOIN audit_box_header ON audit_header.order_number = audit_box_header.order_number) INNER JOIN sscc_shipping_number ON (audit_box_header.order_number = sscc_shipping_number.order_number) AND (audit_box_header.box_number = sscc_shipping_number.box_number)
                             * WHERE sscc_shipping_number.order_number=?ORDERNUMBER and sscc_shipping_number.box_number=?BOXNUMBER";
                             */
                            commandPrintLabel.CommandText = @"SELECT audit_shipment.*, audit_header.*, sscc_shipping_number.*
                                FROM (audit_shipment INNER JOIN audit_header ON audit_shipment.shipment_id = audit_header.shipment_id) 
                                INNER JOIN sscc_shipping_number ON (audit_header.order_number = sscc_shipping_number.order_number) 
                                WHERE sscc_shipping_number.order_number=?ORDERNUMBER and sscc_shipping_number.box_number=?BOXNUMBER";


                            commandPrintLabel.Parameters.Clear();
                            commandPrintLabel.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                            commandPrintLabel.Parameters.AddWithValue("?BOXNUMBER", boxNumber);
                            connPrintLabel.Open();
                            readerPrintLabel = commandPrintLabel.ExecuteReader();

                            btFormat = btApp.Formats.Open(@"\\cca\apps\labels\walmart.btw", false, "");
                            btFormat.Printer = "METO";

                            while (readerPrintLabel.Read())
                            {
                                //string shipToAddressCode = (string)readerPrintLabel["ship_to_address_code"];
                                //string shipToDC = (string)readerPrintLabel["dc_number"];

                                string addressLine3 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_city")) + ", " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_state")) + " " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_zip"));
                                string ucc128 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ucc_128")).Substring(0, 19);
                                shipmentID = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("shipment_id"));
                                boxNumber = readerPrintLabel.GetInt32(readerPrintLabel.GetOrdinal("box_number"));

                                btFormat.SetNamedSubStringValue("ship_line1", (string)readerPrintLabel["ship_to_name"]);

                                btFormat.SetNamedSubStringValue("ship_line2", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address1")));
                                btFormat.SetNamedSubStringValue("ship_line3", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address2")));
                                btFormat.SetNamedSubStringValue("ship_line4", addressLine3);
                                btFormat.SetNamedSubStringValue("sequence1", shipmentID);
                                btFormat.SetNamedSubStringValue("ordernumber", (string)readerPrintLabel["order_number"].ToString());
                                btFormat.SetNamedSubStringValue("dc_num", (string)readerPrintLabel["dc_number"]);
                                btFormat.SetNamedSubStringValue("type", (string)readerPrintLabel["type"]);
                                btFormat.SetNamedSubStringValue("dept1", (string)readerPrintLabel["dept"]);
                                btFormat.SetNamedSubStringValue("zip", (string)readerPrintLabel["ship_to_zip"]);
                                btFormat.SetNamedSubStringValue("store_num", (string)readerPrintLabel["store_number"]);
                                btFormat.SetNamedSubStringValue("vendor", (string)readerPrintLabel["vendor_number"]);
                                btFormat.SetNamedSubStringValue("sscc", ucc128.ToString());
                                btFormat.PrintOut(false, false);
                            }
                            btFormat.Close(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            btApp.Quit(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            break;




                        case Customer.Meijer:
                            //Meijer
                            SSCCNumber(myCust, orderNumber, boxNumber);
                            /*
                             * commandPrintLabel.CommandText = @"SELECT audit_shipment.*, audit_header.*, audit_box_header.*, sscc_shipping_number.*
                             * FROM ((audit_shipment INNER JOIN audit_header ON audit_shipment.shipment_id = audit_header.shipment_id) INNER JOIN audit_box_header ON audit_header.order_number = audit_box_header.order_number) INNER JOIN sscc_shipping_number ON (audit_box_header.order_number = sscc_shipping_number.order_number) AND (audit_box_header.box_number = sscc_shipping_number.box_number)
                             * WHERE sscc_shipping_number.order_number=?ORDERNUMBER and sscc_shipping_number.box_number=?BOXNUMBER";
                             */

                            //new item select 07-05-11
                            commandPrintLabel.CommandText = @"
                                SELECT *
                                FROM audit_detail
                                WHERE order_number=?ORDERNUMBER and upc_number=?UPCNUMBER
                                LIMIT 1";


                            commandPrintLabel.Parameters.Clear();
                            commandPrintLabel.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                            commandPrintLabel.Parameters.AddWithValue("?UPCNUMBER", upcNumber);
                            connPrintLabel.Open();
                            readerPrintLabel = commandPrintLabel.ExecuteReader();
                            if (readerPrintLabel.Read())
                            {
                                cartonQty = readerPrintLabel["conversion_factor"].ToString();
                                //cartonQty = ((int)readerPrintLabel["conversion_factor"] * (int)readerPrintLabel["scanned_qty"]).ToString();
                                vendorItem = (string)readerPrintLabel["trading_partner_item_id"];
                                skuNum = (string)readerPrintLabel["cousin_sku_number"];
                                //numCartons = ("1").ToString(); // ((int)readerPrintLabel["ordered_qty"] / (int)readerPrintLabel["conversion_factor"]).ToString();
                                numCartons = (readerPrintLabel.GetInt32(readerPrintLabel.GetOrdinal("ordered_qty")) / readerPrintLabel.GetInt32(readerPrintLabel.GetOrdinal("conversion_factor"))).ToString();
                            }
                            //end new item select
                            connPrintLabel.Close();

                            commandPrintLabel.CommandText = @"SELECT audit_shipment.*, audit_header.*, sscc_shipping_number.*
                                FROM (audit_shipment INNER JOIN audit_header ON audit_shipment.shipment_id = audit_header.shipment_id) 
                                INNER JOIN sscc_shipping_number ON (audit_header.order_number = sscc_shipping_number.order_number) 
                                WHERE sscc_shipping_number.order_number=?ORDERNUMBER and sscc_shipping_number.box_number=?BOXNUMBER";


                            commandPrintLabel.Parameters.Clear();
                            commandPrintLabel.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                            commandPrintLabel.Parameters.AddWithValue("?BOXNUMBER", boxNumber);
                            connPrintLabel.Open();
                            readerPrintLabel = commandPrintLabel.ExecuteReader();

                            btFormat = btApp.Formats.Open(@"\\cca\apps\labels\meijer.btw", false, "");
                            btFormat.Printer = "METO";

                            while (readerPrintLabel.Read())
                            {
                                //string shipToAddressCode = (string)readerPrintLabel["ship_to_address_code"];
                                //string shipToDC = (string)readerPrintLabel["dc_number"];

                                string addressLine3 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_city")) + ", " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_state")) + " " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_zip"));
                                string ucc128 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ucc_128")).Substring(0, 19);
                                shipmentID = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("po_number"));
                                boxNumber = readerPrintLabel.GetInt32(readerPrintLabel.GetOrdinal("box_number"));

                                btFormat.SetNamedSubStringValue("ship_line1", (string)readerPrintLabel["ship_to_name"]);
                                btFormat.SetNamedSubStringValue("ship_line2", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address1")));
                                btFormat.SetNamedSubStringValue("ship_line3", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address2")));
                                btFormat.SetNamedSubStringValue("ship_line4", addressLine3);
                                btFormat.SetNamedSubStringValue("po_number", shipmentID);
                                btFormat.SetNamedSubStringValue("zip", (string)readerPrintLabel["ship_to_zip"].ToString().Substring(0,5));
                                btFormat.SetNamedSubStringValue("dept_num", (string)readerPrintLabel["dept"]);
                                btFormat.SetNamedSubStringValue("for", (string)readerPrintLabel["store_number"]);
                                //btFormat.SetNamedSubStringValue("num_cartons", numCartons);
                                btFormat.SetNamedSubStringValue("sscc", ucc128.ToString());

                                //btFormat.SetNamedSubStringValue("ordernumber", (string)readerPrintLabel["order_number"].ToString());
                                //btFormat.SetNamedSubStringValue("type", (string)readerPrintLabel["type"]);
                                btFormat.PrintOut(false, false);
                            }
                            btFormat.Close(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            btApp.Quit(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            break;




                        case Customer.Amazon:
                        case Customer.AmazonCanada: //added by Jason 4-22-13
                            //Amazon
                            boxNumber = 0;

                            for (int i = 1; i <= myNumBoxes; i++)
                            {
                                SSCCNumber(myCust, orderNumber, i);
                            }

                            commandPrintLabel.CommandText = @"SELECT audit_shipment.*, audit_header.*, audit_box_header.*, sscc_shipping_number.*
                            FROM ((audit_shipment INNER JOIN audit_header ON audit_shipment.shipment_id = audit_header.shipment_id) INNER JOIN audit_box_header ON audit_header.order_number = audit_box_header.order_number) INNER JOIN sscc_shipping_number ON (audit_box_header.order_number = sscc_shipping_number.order_number) AND (audit_box_header.box_number = sscc_shipping_number.box_number)
                            WHERE sscc_shipping_number.order_number=?ORDERNUMBER
                            ORDER BY sscc_shipping_number.box_number";

                            commandPrintLabel.Parameters.Clear();
                            commandPrintLabel.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                            connPrintLabel.Open();
                            readerPrintLabel = commandPrintLabel.ExecuteReader();

                            btFormat = btApp.Formats.Open(@"\\cca\apps\labels\amazon.btw", false, "");
                            btFormat.Printer = "METO";

                            while (readerPrintLabel.Read())
                            {
                                string addressLine3 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_city")) + ", " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_state")) + " " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_zip"));
                                string ucc128 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ucc_128")).Substring(0, 19);
                                shipmentID = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("shipment_id"));
                                boxNumber = readerPrintLabel.GetInt32(readerPrintLabel.GetOrdinal("box_number"));

                                btFormat.SetNamedSubStringValue("ship_line1", (string)readerPrintLabel["ship_to_name"]);
                                //btFormat.SetNamedSubStringValue("ship_line1", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_name")));

                                btFormat.SetNamedSubStringValue("ship_line2", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address1")));
                                btFormat.SetNamedSubStringValue("ship_line3", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address2")));
                                btFormat.SetNamedSubStringValue("ship_line4", addressLine3);

                                btFormat.SetNamedSubStringValue("po_num", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("po_number")));
                                //btFormat.SetNamedSubStringValue("event_code", " ");
                                btFormat.SetNamedSubStringValue("box_num", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("box_number")));
                                btFormat.SetNamedSubStringValue("box_total", myNumBoxes.ToString());
                                //btFormat.SetNamedSubStringValue("store_num", readerPrintLabel.GetValue(readerPrintLabel.GetOrdinal("store_number")).ToString());
                                btFormat.SetNamedSubStringValue("sscc", ucc128);

                                btFormat.PrintOut(false, false);
                            }
                            btFormat.Close(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            btApp.Quit(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            break;
                        
                        
                        
                        
                        case Customer.Hancock:
                            //Hancock
                            SSCCNumber(myCust, orderNumber, boxNumber);
                            /*
                             * commandPrintLabel.CommandText = @"SELECT audit_shipment.*, audit_header.*, audit_box_header.*, sscc_shipping_number.*
                             * FROM ((audit_shipment INNER JOIN audit_header ON audit_shipment.shipment_id = audit_header.shipment_id) INNER JOIN audit_box_header ON audit_header.order_number = audit_box_header.order_number) INNER JOIN sscc_shipping_number ON (audit_box_header.order_number = sscc_shipping_number.order_number) AND (audit_box_header.box_number = sscc_shipping_number.box_number)
                             * WHERE sscc_shipping_number.order_number=?ORDERNUMBER and sscc_shipping_number.box_number=?BOXNUMBER";
                             */
                            
                            //new item select 07-05-11
                            commandPrintLabel.CommandText = @"
                                SELECT *
                                FROM audit_detail
                                WHERE order_number=?ORDERNUMBER and upc_number=?UPCNUMBER
                                LIMIT 1";


                            commandPrintLabel.Parameters.Clear();
                            commandPrintLabel.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                            commandPrintLabel.Parameters.AddWithValue("?UPCNUMBER", upcNumber);
                            connPrintLabel.Open();
                            readerPrintLabel = commandPrintLabel.ExecuteReader();
                            if (readerPrintLabel.Read())
                            {
                                cartonQty = readerPrintLabel["conversion_factor"].ToString();
                                //cartonQty = ((int)readerPrintLabel["conversion_factor"] * (int)readerPrintLabel["scanned_qty"]).ToString();
                                vendorItem = (string)readerPrintLabel["trading_partner_item_id"];
                                skuNum = (string)readerPrintLabel["cousin_sku_number"];
                                //numCartons = ("1").ToString(); // ((int)readerPrintLabel["ordered_qty"] / (int)readerPrintLabel["conversion_factor"]).ToString();
                                numCartons = (readerPrintLabel.GetInt32(readerPrintLabel.GetOrdinal("ordered_qty")) / readerPrintLabel.GetInt32(readerPrintLabel.GetOrdinal("conversion_factor"))).ToString();
                            }
                            //end new item select
                            connPrintLabel.Close();

                            commandPrintLabel.CommandText = @"SELECT audit_shipment.*, audit_header.*, sscc_shipping_number.*
                                FROM (audit_shipment INNER JOIN audit_header ON audit_shipment.shipment_id = audit_header.shipment_id) 
                                INNER JOIN sscc_shipping_number ON (audit_header.order_number = sscc_shipping_number.order_number) 
                                WHERE sscc_shipping_number.order_number=?ORDERNUMBER and sscc_shipping_number.box_number=?BOXNUMBER";


                            commandPrintLabel.Parameters.Clear();
                            commandPrintLabel.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                            commandPrintLabel.Parameters.AddWithValue("?BOXNUMBER", boxNumber);
                            connPrintLabel.Open();
                            readerPrintLabel = commandPrintLabel.ExecuteReader();

                            btFormat = btApp.Formats.Open(@"\\cca\apps\labels\hancock.btw", false, "");
                            btFormat.Printer = "METO";

                            while (readerPrintLabel.Read())
                            {
                                //string shipToAddressCode = (string)readerPrintLabel["ship_to_address_code"];
                                //string shipToDC = (string)readerPrintLabel["dc_number"];

                                string addressLine3 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_city")) + ", " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_state")) + " " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_zip"));
                                string ucc128 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ucc_128")).Substring(0, 19);
                                shipmentID = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("po_number"));
                                boxNumber = readerPrintLabel.GetInt32(readerPrintLabel.GetOrdinal("box_number"));

                                btFormat.SetNamedSubStringValue("ship_line1", (string)readerPrintLabel["ship_to_name"]);
                                btFormat.SetNamedSubStringValue("ship_line2", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address1")));
                                btFormat.SetNamedSubStringValue("ship_line3", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address2")));
                                btFormat.SetNamedSubStringValue("ship_line4", addressLine3);
                                btFormat.SetNamedSubStringValue("po_number", shipmentID);
                                btFormat.SetNamedSubStringValue("zip", (string)readerPrintLabel["ship_to_zip"]);
                                btFormat.SetNamedSubStringValue("dept_num", (string)readerPrintLabel["dept"]);
                                btFormat.SetNamedSubStringValue("bl_num", (string)readerPrintLabel["bill_of_lading"]);
                                btFormat.SetNamedSubStringValue("carton_qty", cartonQty);
                                btFormat.SetNamedSubStringValue("ven_item_num", vendorItem);
                                btFormat.SetNamedSubStringValue("ven_num", (string)readerPrintLabel["vendor_number"]);
                                btFormat.SetNamedSubStringValue("sku_num", skuNum);
                                btFormat.SetNamedSubStringValue("upc_num", upcNumber);
                                btFormat.SetNamedSubStringValue("num_cartons", numCartons);
                                btFormat.SetNamedSubStringValue("sscc", ucc128.ToString());
                                btFormat.SetNamedSubStringValue("carrier_pro_num", (string)readerPrintLabel["carrier_pro_number"]);
                                btFormat.SetNamedSubStringValue("carrier", "Averitt Express");

                                //btFormat.SetNamedSubStringValue("ordernumber", (string)readerPrintLabel["order_number"].ToString());
                                //btFormat.SetNamedSubStringValue("type", (string)readerPrintLabel["type"]);
                                 btFormat.PrintOut(false, false);
                            }
                            btFormat.Close(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            btApp.Quit(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            break;



                        case Customer.WalmartCA:
                            //Wal-Mart
                            SSCCNumber(myCust, orderNumber, boxNumber);
                            /*
                             * commandPrintLabel.CommandText = @"SELECT audit_shipment.*, audit_header.*, audit_box_header.*, sscc_shipping_number.*
                             * FROM ((audit_shipment INNER JOIN audit_header ON audit_shipment.shipment_id = audit_header.shipment_id) INNER JOIN audit_box_header ON audit_header.order_number = audit_box_header.order_number) INNER JOIN sscc_shipping_number ON (audit_box_header.order_number = sscc_shipping_number.order_number) AND (audit_box_header.box_number = sscc_shipping_number.box_number)
                             * WHERE sscc_shipping_number.order_number=?ORDERNUMBER and sscc_shipping_number.box_number=?BOXNUMBER";
                             */
                            commandPrintLabel.CommandText = @"SELECT audit_shipment.*, audit_header.*, sscc_shipping_number.*
                                FROM (audit_shipment INNER JOIN audit_header ON audit_shipment.shipment_id = audit_header.shipment_id) 
                                INNER JOIN sscc_shipping_number ON (audit_header.order_number = sscc_shipping_number.order_number) 
                                WHERE sscc_shipping_number.order_number=?ORDERNUMBER and sscc_shipping_number.box_number=?BOXNUMBER";


                            commandPrintLabel.Parameters.Clear();
                            commandPrintLabel.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                            commandPrintLabel.Parameters.AddWithValue("?BOXNUMBER", boxNumber);
                            connPrintLabel.Open();
                            readerPrintLabel = commandPrintLabel.ExecuteReader();

                            btFormat = btApp.Formats.Open(@"\\cca\apps\labels\walmartcanada.btw", false, "");
                            btFormat.Printer = "METO";

                            while (readerPrintLabel.Read())
                            {
                                //string shipToAddressCode = (string)readerPrintLabel["ship_to_address_code"];
                                //string shipToDC = (string)readerPrintLabel["dc_number"];

                                string addressLine3 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_city")) + ", " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_state")) + " " + readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_zip"));
                                string ucc128 = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ucc_128")).Substring(0, 19);
                                shipmentID = readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("shipment_id"));
                                boxNumber = readerPrintLabel.GetInt32(readerPrintLabel.GetOrdinal("box_number"));

                                btFormat.SetNamedSubStringValue("ship_line1", (string)readerPrintLabel["ship_to_name"]);

                                btFormat.SetNamedSubStringValue("ship_line2", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address1")));
                                btFormat.SetNamedSubStringValue("ship_line3", readerPrintLabel.GetString(readerPrintLabel.GetOrdinal("ship_to_address2")));
                                btFormat.SetNamedSubStringValue("ship_line4", addressLine3);
                                btFormat.SetNamedSubStringValue("sequence1", shipmentID);
                                btFormat.SetNamedSubStringValue("ordernumber", (string)readerPrintLabel["order_number"].ToString());
                                btFormat.SetNamedSubStringValue("dc_num", (string)readerPrintLabel["dc_number"]);
                                btFormat.SetNamedSubStringValue("type", (string)readerPrintLabel["type"]);
                                btFormat.SetNamedSubStringValue("dept1", (string)readerPrintLabel["dept"]);
                                //btFormat.SetNamedSubStringValue("zip", (string)readerPrintLabel["ship_to_zip"]);
                                btFormat.SetNamedSubStringValue("store_num", (string)readerPrintLabel["store_number"]);
                                btFormat.SetNamedSubStringValue("vendor", (string)readerPrintLabel["vendor_number"]);
                                btFormat.SetNamedSubStringValue("sscc", ucc128.ToString());
                                btFormat.PrintOut(false, false);
                            }
                            btFormat.Close(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            btApp.Quit(BarTender.BtSaveOptions.btDoNotSaveChanges);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        //private void UpdateShipDate(string shipmentID)
        //{
        //    using (MySqlConnection connUpdateShipDate = new MySqlConnection("server=192.168.123.24;user id=audit;password=audit;database=audit"))
        //    {
        //        using (MySqlCommand commandUpdateShipDate = connUpdateShipDate.CreateCommand())
        //        {
        //            string shipDate = DateTime.Now.Year.ToString().PadLeft(4, '0') + "-" + DateTime.Now.Month.ToString().PadLeft(2, '0') + "-" + DateTime.Now.Day.ToString().PadLeft(2, '0');

        //            commandUpdateShipDate.CommandText = "UPDATE audit_shipment SET ship_date = ?SHIPDATE WHERE shipment_id = ?SHIPMENTID";
        //            commandUpdateShipDate.Parameters.Clear();
        //            commandUpdateShipDate.Parameters.Add("?SHIPDATE", shipDate);
        //            commandUpdateShipDate.Parameters.Add("?SHIPMENTID", shipmentID);
        //            connUpdateShipDate.Open();
        //            commandUpdateShipDate.ExecuteNonQuery();
        //        }
        //    }
        //}

    }
}
