using System;
using System.Collections.Generic;
//using System.Data.SqlClient;
using System.Text;
using MySql.Data.MySqlClient;

namespace Cousin.Audit
{
    public class Order
    {
        #region Delegates and Events

        public delegate void OrderFinishedHandler(object sender, EventArgs e);
        public event OrderFinishedHandler OnOrderFinished;

        public delegate void OrderKilledHandler(object sender, EventArgs e);
        public event OrderKilledHandler OnOrderKilled;

        public delegate void UPCScannedHandler(object sender, UPCScannedEventArgs e);
        public event UPCScannedHandler OnUPCScanned;

        public delegate void NewBoxHandler(object sender, int boxNumber);
        public event NewBoxHandler OnNewBox;

        public delegate void OrderChangedHandler(object sender, EventArgs e);
        public event OrderChangedHandler OnOrderChanged;

        public delegate void ErrorHandler(object sender, ErrorEventArgs e);
        public static event ErrorHandler OnError;

        public delegate void MySQLErrorHandler(object sender, ErrorEventArgs e);
        public static event MySQLErrorHandler OnMySqlError;

        #endregion

        public enum Customer
        {
            MichaelsUS,
            MichaelsCA,
            WalmartUS,
            WalmartCA,
            Joann,
            Acmoore,
            Hancock,
            Amazon,
            AmazonCanada,
            Meijer,
            Generic,
            Unknown,
        }
        public Dictionary<string, ItemOrdered> ItemsOrdered;
        public Dictionary<int, Dictionary<string, ItemScanned>> Box;
        public string orderNumber { get; private set; }//public readonly string orderNumber;

        private bool myLoggedMissing = false; //When print missing items, log it, but not more than once
        private const string ConnectionString = "server=192.168.123.24;user id=audit;password=audit;database=audit";
        private MistakeLogger myLogger;

        public Order(string orderNumber)
        {
            this.orderNumber = orderNumber;
            this.myLogger = new MistakeLogger(orderNumber);
            CreateOrder();
        }


        #region Private methods

        void CreateOrder()
        {
            using(MySqlConnection myConn = new MySqlConnection(ConnectionString))
            {
                using(MySqlCommand myCommand = myConn.CreateCommand())
                {
                    // Creat dictionaries to hold ordered and scanned items
                    ItemsOrdered = new Dictionary<string, ItemOrdered>();
                    Box = new Dictionary<int, Dictionary<string, ItemScanned>>();

                    // Get items ordered
                    string sql = "SELECT cousin_sku_number, cousin_sku_description, upc_number, ordered_qty, weight, conversion_factor, bin_loc FROM audit_detail WHERE order_number = ?ORDERNUMBER";
                    myCommand.CommandText = sql;
                    myCommand.Parameters.AddWithValue("?ORDERNUMBER", this.orderNumber);
                    try
                    {
                        myConn.Open();
                        MySqlDataReader myReader = myCommand.ExecuteReader(System.Data.CommandBehavior.CloseConnection);

                        // If there are no rows then order number doesn't exist
                        if(!myReader.HasRows)
                        {
                            string msg = this.orderNumber + " doesn't exist";
                            throw new ApplicationException(msg);
                        }

                        // Fill itemsOrdered dictionary
                        while(myReader.Read())
                        {
                            string upc = myReader.GetString(myReader.GetOrdinal("upc_number"));
                            if(ItemsOrdered.ContainsKey(upc))
                                ItemsOrdered[upc].OrderedQTY += myReader.GetInt32(myReader.GetOrdinal("ordered_qty"));
                            else
                            {
                                ItemOrdered myItem = new ItemOrdered(myReader.GetString(myReader.GetOrdinal("cousin_sku_number")), myReader.GetString(myReader.GetOrdinal("cousin_sku_description")), myReader.GetString(myReader.GetOrdinal("upc_number")), myReader.GetInt32(myReader.GetOrdinal("ordered_qty")), myReader.GetFloat(myReader.GetOrdinal("weight")), myReader.GetInt32(myReader.GetOrdinal("conversion_factor")), myReader.GetString(myReader.GetOrdinal("bin_loc")));
                                ItemsOrdered.Add(myReader.GetString(myReader.GetOrdinal("upc_number")), myItem);
                            }
                        }
                    }
                    catch(Exception err)
                    {
                        if(OnMySqlError != null)
                            OnMySqlError(this, new ErrorEventArgs(err.Message.ToString()));
                        else
                            throw new ApplicationException(err.Message.ToString());
                    }
                    
                }
            }
        }

        #endregion


        #region Static Methods

        // this is only used for michaels CA and will go away shortly
        public static void SetBoxWeight(string orderNumber, int boxNumber, float weight)
        {
            if(GetCustomer(orderNumber) == Customer.Joann || GetCustomer(orderNumber) == Customer.Acmoore)
            {
                using(MySqlConnection myConn = new MySqlConnection(ConnectionString))
                {
                    using(MySqlCommand myCommand = myConn.CreateCommand())
                    {
                        myCommand.CommandText = "UPDATE audit_box_header SET box_weight = ?WEIGHT" +
                            " WHERE order_number = ?ORDERNUMBER and box_number = ?BOXNUMBER";
                        myCommand.Parameters.AddWithValue("?WEIGHT", weight);
                        myCommand.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                        myCommand.Parameters.AddWithValue("?BOXNUMBER", boxNumber);
                        try
                        {
                            myConn.Open();
                            myCommand.ExecuteNonQuery();
                        }
                        catch(Exception err)
                        {
                            if(OnMySqlError != null)
                                OnMySqlError(null, new ErrorEventArgs(err.Message.ToString()));
                            else
                                throw new ApplicationException(err.Message.ToString());
                        }
                        finally
                        {
                            if(myConn.State != System.Data.ConnectionState.Closed)
                                myConn.Close();
                        }
                    }
                }
            }
            else
            {
                if(OnError != null)
                    OnError(null, new ErrorEventArgs("This is used for Joann only"));
                else
                    throw new ApplicationException("This is used for Joann only");
            }
        }

        public static int GetStatus(string orderNumber)
        {
            int status = 0;
            using(MySqlConnection myConn = new MySqlConnection(ConnectionString))
            {
                using(MySqlCommand myCommand = myConn.CreateCommand())
                {
                    myCommand.CommandText = "SELECT status FROM audit_header WHERE order_number = ?ORDERNUMBER";
                    myCommand.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                    try
                    {
                        myConn.Open();
                        object crap = myCommand.ExecuteScalar();
                        if(crap != null)
                            int.TryParse(crap.ToString(), out status);
                    }
                    catch(Exception err)
                    {
                        if(OnMySqlError != null)
                            OnMySqlError(null, new ErrorEventArgs(err.Message.ToString()));
                        else
                            throw new ApplicationException(err.Message.ToString());
                    }
                    finally
                    {
                        if(myConn.State != System.Data.ConnectionState.Closed)
                            myConn.Close();
                    }
                }
            }
            return status;
        }

        public static void SetStatus2(string orderNumber)
        {
            /*
             * Status 1 = untouched
             * Status 2 = audit started
             * Status 3 = audit finished
             * Status 4 = started releasing (but does not mean finished releasing for sure)
            */
            using (MySqlConnection myConn = new MySqlConnection(ConnectionString))
            {
                using (MySqlCommand myCommand = myConn.CreateCommand())
                {
                    myCommand.CommandText = "UPDATE audit_header SET status = '2' WHERE order_number = ?ORDERNUMBER";
                    myCommand.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                    try
                    {
                        myConn.Open();
                        myCommand.ExecuteNonQuery();
                    }
                    catch (Exception err)
                    {
                        if (OnMySqlError != null)
                            OnMySqlError(null, new ErrorEventArgs(err.Message.ToString()));
                        else
                            throw new ApplicationException(err.Message.ToString());
                    }
                    finally
                    {
                        if (myConn.State != System.Data.ConnectionState.Closed)
                            myConn.Close();
                    }
                }
            }
        }

        public static int[] GetBoxNumbers(string orderNumber)
        {
            if(!Exists(orderNumber))
            {
                if(OnError != null)
                {
                    OnError(null, new ErrorEventArgs("Order " + orderNumber + " doesn't exist"));
                    return new int[0];
                }
                else
                    throw new ApplicationException("Order " + orderNumber + " doesn't exist");
            }

            List<int> boxNumbers = new List<int>();
            int currBoxNumber;

            if(GetStatus(orderNumber) == 3)
            {
                using(MySqlConnection myConn = new MySqlConnection(ConnectionString))
                {
                    using(MySqlCommand myCommand = myConn.CreateCommand())
                    {
                        myCommand.CommandText = "SELECT DISTINCT box_number FROM audit_box_header WHERE order_number = ?ORDERNUMBER";
                        myCommand.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                        try
                        {
                            myConn.Open();
                            MySqlDataReader myReader = myCommand.ExecuteReader();
                            while(myReader.Read())
                            {
                                if(int.TryParse(myReader.GetString(0), out currBoxNumber))
                                    boxNumbers.Add(currBoxNumber);
                            }
                        }
                        catch(Exception err)
                        {
                            if(OnMySqlError != null)
                                OnMySqlError(null, new ErrorEventArgs(err.Message.ToString()));
                            else
                                throw new ApplicationException(err.Message.ToString());
                        }
                        finally
                        {
                            if(myConn.State != System.Data.ConnectionState.Closed)
                                myConn.Close();
                        }
                    }
                }
            }
            else
            {
                if(OnError != null)
                    OnError(null, new ErrorEventArgs("Order " + orderNumber + " does not have a status of FINISHED"));
                else
                    throw new ApplicationException("Order " + orderNumber + " does not have a status of FINISHED");
            }
            return boxNumbers.ToArray();
        }

        public static bool Exists(string orderNumber)
        {
            bool exists = false;

            using(MySqlConnection myConn = new MySqlConnection(ConnectionString))
            {
                using(MySqlCommand myCommand = myConn.CreateCommand())
                {
                    myCommand.CommandText = "Select Count(*) From audit_detail Where order_number = ?ORDERNUMBER";
                    myCommand.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                    try
                    {
                        myConn.Open();
                        object result = myCommand.ExecuteScalar();
                        if(int.Parse(result.ToString()) > 0)
                        {
                            exists = true;
                        }
                    }
                    catch(Exception err)
                    {
                        if(OnMySqlError != null)
                        {
                            OnMySqlError(null, new ErrorEventArgs(err.Message.ToString()));
                        }
                        else
                        {
                            throw new ApplicationException(err.Message.ToString());
                        }
                    }
                }
            }
            return exists;
        }

        public static Customer GetCustomer(string orderNumber)
        {
            string shipToName = string.Empty;
            string customerNumber = string.Empty;
            Customer currentCustomer;

            using(MySqlConnection myConn = new MySqlConnection(ConnectionString))
            {
                using(MySqlCommand myCommand = myConn.CreateCommand())
                {
                    MySqlDataReader myReader;
                    myCommand.CommandText = @"SELECT audit_shipment.ship_to_name, audit_header.customer_number
                        FROM audit_header LEFT JOIN audit_shipment ON audit_header.shipment_id = audit_shipment.shipment_id
                        WHERE audit_header.order_number = ?ORDERNUMBER
                        LIMIT 1";
                    myCommand.Parameters.Clear();
                    myCommand.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);

                    try
                    {
                        myConn.Open();
                        myReader = myCommand.ExecuteReader();
                        if(myReader.HasRows)
                        {
                            while(myReader.Read())
                            {
                                shipToName = myReader.GetString(myReader.GetOrdinal("ship_to_name"));
                                customerNumber = myReader.GetString(myReader.GetOrdinal("customer_number"));
                                break;
                            }
                        }
                        else
                        {
                            if(OnError != null)
                            {
                                OnError(null, new ErrorEventArgs("Order " + orderNumber + " doesn't exist"));
                            }
                            else
                            {
                                throw new ApplicationException("Order " + orderNumber + " doesn't exist");
                            }
                        }
                    }
                    catch(ApplicationException)
                    {
                        throw;
                    }
                    catch(Exception err)
                    {
                        if(OnMySqlError != null)
                        {
                            OnMySqlError(null, new ErrorEventArgs(err.Message.ToString()));
                        }
                        else
                        {
                            throw new ApplicationException(err.Message.ToString());
                        }
                    }
                    finally
                    {
                        if(myConn.State != System.Data.ConnectionState.Closed)
                        {
                            myConn.Close();
                        }
                    }
                }
            }
            switch(customerNumber)
            {
                case "136873":
                    if(shipToName.ToLower().Contains("canada"))
                    {
                        currentCustomer = Customer.MichaelsCA;
                    }
                    else
                    {
                        currentCustomer = Customer.MichaelsUS;
                    }
                    break;
                case "160454":
                    currentCustomer = Customer.WalmartUS;
                    break;
                case "51173":
                    currentCustomer = Customer.Hancock;
                    break;
                case "231058":
                    currentCustomer = Customer.WalmartCA;
                    break;
                case "224590":
                    currentCustomer = Customer.Joann;
                    break;
                case "7731":
                    currentCustomer = Customer.Acmoore;
                    break;
                case "235890":
                    currentCustomer = Customer.Amazon;
                    break;
                case "235891": // added by Jason 4-22-13
                    currentCustomer = Customer.AmazonCanada;
                    break;
                case "235942":
                    currentCustomer = Customer.Meijer;
                    break;
                default:
                    //if(OnError != null)
                    //{
                    //    OnError(null, new ErrorEventArgs("Unknown customer number: " + customerNumber));
                    //}
                    //else
                    //{
                    //    throw new ApplicationException("Unknown customer number: " + customerNumber);
                    //}
                    //currentCustomer = Customer.Unknown;
                    currentCustomer = Customer.Generic;
                    break;
            }
            return currentCustomer;
        }

        public static DateTime GetAuditDate(string orderNumber)
        {
            DateTime auditDate = DateTime.Parse("2000-01-01");

            using(MySqlConnection myConn = new MySqlConnection(ConnectionString))
            {
                using(MySqlCommand myCommand = myConn.CreateCommand())
                {
                    myCommand.CommandText = "SELECT audit_date FROM audit_header WHERE audit_date <> '0000-00-00' AND order_number = ?ORDERNUMBER";
                    myCommand.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                    try
                    {
                        myConn.Open();
                        object result = myCommand.ExecuteScalar();
                        if(result != null)
                        {
                            if(!DateTime.TryParse(result.ToString(), out auditDate))
                            {
                                auditDate = DateTime.Parse("2000-01-01");
                            }
                        }
                    }
                    catch(Exception err)
                    {
                        if(OnMySqlError != null)
                        {
                            OnMySqlError(null, new ErrorEventArgs(err.Message.ToString()));
                        }
                        else
                        {
                            throw new ApplicationException(err.Message.ToString());
                        }
                    }
                }
            }
            return auditDate;
        }

        public static bool ASNSent(string orderNumber)
        {
            string shipmentID = "";
            string asnStatus = "";

            // get shipment id of order
            using (MySqlConnection myConn = new MySqlConnection(ConnectionString))
            {
                using (MySqlCommand myCommand = myConn.CreateCommand())
                {
                    myCommand.CommandText = "SELECT shipment_id FROM audit_header WHERE order_number = ?ORDERNUMBER";
                    myCommand.Parameters.AddWithValue("?ORDERNUMBER", orderNumber);
                    try
                    {
                        myConn.Open();
                        object crap = myCommand.ExecuteScalar();
                        if (crap != null)
                        {
                            shipmentID = crap.ToString();
                        }
                    }
                    catch (Exception err)
                    {
                        if (OnMySqlError != null)
                        {
                            OnMySqlError(null, new ErrorEventArgs(err.Message.ToString()));
                        }
                        else
                        {
                            throw new ApplicationException(err.Message.ToString());
                        }
                    }
                    finally
                    {
                        if (myConn.State != System.Data.ConnectionState.Closed)
                        {
                            myConn.Close();
                        }
                    }
                }
            }

            // get asn status using shipment id from above
            using (MySqlConnection myConn = new MySqlConnection(ConnectionString))
            {
                using (MySqlCommand myCommand = myConn.CreateCommand())
                {
                    myCommand.CommandText = "SELECT asn_sent FROM audit_shipment WHERE shipment_id = ?SHIPID";
                    myCommand.Parameters.AddWithValue("?SHIPID", shipmentID);
                    try
                    {
                        myConn.Open();
                        object crap = myCommand.ExecuteScalar();
                        if (crap != null)
                        {
                            asnStatus = crap.ToString();
                        }
                    }
                    catch (Exception err)
                    {
                        if (OnMySqlError != null)
                        {
                            OnMySqlError(null, new ErrorEventArgs(err.Message.ToString()));
                        }
                        else
                        {
                            throw new ApplicationException(err.Message.ToString());
                        }
                    }
                    finally
                    {
                        if (myConn.State != System.Data.ConnectionState.Closed)
                        {
                            myConn.Close();
                        }
                    }
                }
            }

            if (asnStatus.ToLower().Equals("y"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        private void logMissingItems()
        {
            List<string> itemsNotInStock;

            using (MySqlConnection myConn = new MySqlConnection(ConnectionString))
            {
                using (MySqlCommand myCommand = myConn.CreateCommand())
                {
                    // Jason was renamed from upcNotInStock to itemNotInStock on 11/17/2017
                    itemsNotInStock = new List<string>();

                    // Get items not in stock
                    string sql = "SELECT upc, item_num FROM items_not_on_pick_line";
                    myCommand.CommandText = sql;
                    myCommand.Parameters.Clear();
                    try
                    {
                        myConn.Open();
                        MySqlDataReader myReader = myCommand.ExecuteReader(System.Data.CommandBehavior.CloseConnection);

                        // If HasRows then items are out of stock
                        if (myReader.HasRows)
                        {
                            // Insert out of stock items into list
                            while (myReader.Read())
                            {
                                // Jason was upc, changed to itemNumber on 11/17/2017
                                // string upc = myReader.GetString(myReader.GetOrdinal("upc"));
                                string itemNumber = myReader.GetString(myReader.GetOrdinal("item_num"));
                                if (!itemsNotInStock.Contains(itemNumber))
                                {
                                    itemsNotInStock.Add(itemNumber);
                                }
                            }
                        } 
                    }
                    catch (Exception err)
                    {
                        if (OnMySqlError != null)
                            OnMySqlError(this, new ErrorEventArgs(err.Message.ToString()));
                        else
                            throw new ApplicationException(err.Message.ToString());
                    }

                }
            }

            if (!myLoggedMissing)
            {
                foreach (ItemOrdered itOrd in ItemsOrdered.Values)
                {
                    // Jason was UPCNumber, changed to ItemNumber on 11/17/2017
                    // if (itemsNotInStock.Contains(itOrd.UPCNumber))
                    if (itemsNotInStock.Contains(itOrd.ItemNumber))
                    {
                        continue;
                    }
                    if (itOrd.ScannedQTY < itOrd.OrderedQTY)
                    {
                        myLogger.LogItem(itOrd.ItemNumber, itOrd.ItemDescription, itOrd.UPCNumber, itOrd.BinLOC, itOrd.ConversionFactor, MistakeLogger.Item.Reason.Missing);
                    }
                }
            }
            myLoggedMissing = true;
        }

        #region Public Methods

        public bool IsFinished()
        {
            // Compares ordered vs scanned qty of items ordered
            // to see if order is finished
            bool finished = true;
            foreach(ItemOrdered itm in ItemsOrdered.Values)
            {
                if(itm.OrderedQTY > itm.ScannedQTY)
                {
                    finished = false;
                    break;
                }
            }
            return finished;
        }

        public void ReleaseCompleteOneBox()
        {
            foreach(ItemOrdered itmOrd in ItemsOrdered.Values)
            {
                do
                {
                    ScanItemForCompleteOneBox(itmOrd.UPCNumber, 1);
                } while(itmOrd.ScannedQTY < itmOrd.OrderedQTY);
            }
        }

        public bool OrderHasScans()
        {
            bool hasScans = false;
            foreach(ItemOrdered itOrd in ItemsOrdered.Values)
            {
                if(itOrd.ScannedQTY > 0)
                {
                    hasScans = true;
                    break;
                }
            }
            return hasScans;
        }

        public bool BoxHasScans(int boxNumber)
        {
            bool hasScans = false;
            if(Box.ContainsKey(boxNumber))
            {
                hasScans = true;
            }
            return hasScans;
        }

        public float GetBoxWeight(int boxNumber)
        {
            // Loops through Box[boxNumber] and adds all weight
            // to get total weight of box
            float weight = 0F;
            foreach (ItemScanned itm in Box[boxNumber].Values)
            {
                weight += itm.ScannedWeight;
            }
            return weight;
        }

        public void ScanItemForCompleteOneBox(string upc, int boxNumber)
        {
            // Checks ItemsOrdered dictionary to see if upc
            // belongs on current order
            if(ItemsOrdered.ContainsKey(upc))
            {
                // Checks to see if this item is complete
                if(ItemsOrdered[upc].OrderedQTY > ItemsOrdered[upc].ScannedQTY)
                {
                    if(boxNumber > GetNextBoxNumber())
                    {
                        string msg = "Next available box# is: " + GetNextBoxNumber().ToString();
                        if(OnError != null)
                        {
                            OnError(this, new ErrorEventArgs(msg));
                            return;
                        }
                        else
                            throw new ApplicationException(msg);
                    }
                    if(!Box.ContainsKey(boxNumber))
                    {
                        Dictionary<string, ItemScanned> tmp = new Dictionary<string, ItemScanned>();
                        Box.Add(boxNumber, tmp);
                        if(OnNewBox != null)
                        {
                            OnNewBox(this, boxNumber);
                        }
                    }

                    if(Box[boxNumber].ContainsKey(upc))
                    {
                        Box[boxNumber][upc].ScannedQTY += ItemsOrdered[upc].ConversionFactor;
                        Box[boxNumber][upc].ScannedWeight += ItemsOrdered[upc].Weight;
                    }
                    else
                    {
                        Box[boxNumber].Add(upc, new ItemScanned(upc, ItemsOrdered[upc].ItemNumber, ItemsOrdered[upc].ItemDescription,ItemsOrdered[upc].OrderedQTY, ItemsOrdered[upc].ConversionFactor, ItemsOrdered[upc].Weight));
                    }
                    ItemsOrdered[upc].ScannedQTY += ItemsOrdered[upc].ConversionFactor;

                    // Check to see if OrderIsFinished
                    // and call the FinishOrder method if true
                    if(IsFinished())
                        FinishOrder();
                }
                else
                {
                    string msg = upc + " already scanned and complete for this order.";
                    if(OnError != null)
                    {
                        OnError(this, new ErrorEventArgs(msg));
                        return;
                    }
                    else
                        throw new ApplicationException(msg);
                }
            }
            else
            {
                string msg = upc + " is not on this order.";
                if(OnError != null)
                {
                    OnError(this, new ErrorEventArgs(msg));
                    return;
                }
                else
                    throw new ApplicationException(msg);
            }
        }

        public void ScanItem(string upc, int boxNumber)
        {
            bool orderIsFinished;
            // Checks ItemsOrdered dictionary to see if upc
            // belongs on current order
            if(ItemsOrdered.ContainsKey(upc))
            {
                // Checks to see if this item is complete
                if(ItemsOrdered[upc].OrderedQTY > ItemsOrdered[upc].ScannedQTY)
                {
                    if (boxNumber > GetNextBoxNumber())
                    {
                        string msg = "Next available box# is: " + GetNextBoxNumber().ToString();
                        if (OnError != null)
                        {
                            OnError(this, new ErrorEventArgs(msg));
                            return;
                        }
                        else
                            throw new ApplicationException(msg);
                    }

                    if (!Box.ContainsKey(boxNumber))
                    {
                        Dictionary<string,ItemScanned> tmp = new Dictionary<string,ItemScanned>();
                        Box.Add(boxNumber, tmp);
                        if(OnNewBox != null)
                        {
                            OnNewBox(this, boxNumber);
                        }
                    }

                    if (Box[boxNumber].ContainsKey(upc))
                    {
                        Box[boxNumber][upc].ScannedQTY += ItemsOrdered[upc].ConversionFactor;
                        Box[boxNumber][upc].ScannedWeight += ItemsOrdered[upc].Weight;
                    }
                    else
                    {
                        Box[boxNumber].Add(upc, new ItemScanned(upc, ItemsOrdered[upc].ItemNumber, ItemsOrdered[upc].ItemDescription, ItemsOrdered[upc].OrderedQTY, ItemsOrdered[upc].ConversionFactor, ItemsOrdered[upc].Weight));
                    }
                    ItemsOrdered[upc].ScannedQTY += ItemsOrdered[upc].ConversionFactor;

                    orderIsFinished = IsFinished();

                    // call the UPCScanned EVENT
                    if (OnUPCScanned != null)
                    {
                        OnUPCScanned(this, new UPCScannedEventArgs(upc, boxNumber, ItemsOrdered[upc].ConversionFactor, ItemsOrdered[upc].ScannedQTY, ItemsOrdered[upc].OrderedQTY, orderIsFinished));
                    }

                    // Check to see if OrderIsFinished
                    // and call the FinishOrder method if true
                    if (orderIsFinished)
                    {
                        FinishOrder();
                    }
                }
                else
                {
                    string msg = upc + ": The ordered qty of " + ItemsOrdered[upc].OrderedQTY.ToString() + " has already been scanned";
                    //Add code here to log a upc that is complete but still scanned (overscan)
                    //LogOverScan added March 18th, 2015 - Jason
                    myLogger.LogItem(ItemsOrdered[upc].ItemNumber, ItemsOrdered[upc].ItemDescription, upc, ItemsOrdered[upc].BinLOC, ItemsOrdered[upc].ConversionFactor, MistakeLogger.Item.Reason.Overscan);

                    if (OnError != null)
                    {
                        OnError(this, new ErrorEventArgs(msg));
                        return;
                    }
                    else
                        throw new ApplicationException(msg);
                }
            }
            else
            {
                string msg = upc + " was not ordered on order number:" + this.ToString();
                //Add code here to log a upc that was not on this order
                //LogWrongItem() added March 18th, 2015 - Jason
                myLogger.LogItem("9999999999", "Unordered Item", upc, "00A00", 1, MistakeLogger.Item.Reason.Wrong);

                if (OnError != null)
                {
                    OnError(this, new ErrorEventArgs(msg));
                    return;
                }
                else
                    throw new ApplicationException(msg);
            }
        }

        public void FinishOrder()
        {
            //UPDATE INFO TO DATABASE
            using (MySqlConnection myConn = new MySqlConnection(ConnectionString))
            {
                using (MySqlCommand myCommand = myConn.CreateCommand())
                {
                    #region audit_box
                    // audit_box
                    myCommand.CommandText = "DELETE FROM audit_box WHERE order_number = ?ORDERNUMBER; " +
                        "DELETE FROM log WHERE order_number = ?ORDERNUMBER; " +
                        "DELETE FROM log_scanned_order_info WHERE order_number = ?ORDERNUMBER;";
                    myCommand.Parameters.Clear();
                    myCommand.Parameters.AddWithValue("?ORDERNUMBER", this.orderNumber);
                    try
                    {
                        myConn.Open();
                        myCommand.ExecuteNonQuery();
                    }
                    catch(Exception err)
                    {
                        if(OnError != null)
                        {
                            OnError(this, new ErrorEventArgs(err.Message.ToString()));
                        }
                        else
                        {
                            throw new ApplicationException(err.Message.ToString());
                        }
                    }
                    finally
                    {
                        if(myConn.State != System.Data.ConnectionState.Closed)
                            myConn.Close();
                    }
                    foreach (int i in Box.Keys)
                    {
                        foreach (ItemScanned item in Box[i].Values)
                        {
                            myCommand.CommandText = "INSERT INTO audit_box (order_number, upc_number, box_number, box_qty, box_weight)" +
                                " VALUES (?ORDERNUMBER, ?UPCNUMBER, ?BOXNUMBER, ?BOXQTY, ?BOXWEIGHT)";
                            myCommand.Parameters.Clear();
                            myCommand.Parameters.AddWithValue("?ORDERNUMBER", this.orderNumber);
                            myCommand.Parameters.AddWithValue("?UPCNUMBER", item.UPCNumber);
                            myCommand.Parameters.AddWithValue("?BOXNUMBER", i);
                            myCommand.Parameters.AddWithValue("?BOXQTY", item.ScannedQTY);
                            myCommand.Parameters.AddWithValue("?BOXWEIGHT", item.ScannedWeight);
                            try
                            {
                                myConn.Open();
                                myCommand.ExecuteNonQuery();
                            }
                            catch (Exception err)
                            {
                                if(OnError != null)
                                {
                                    OnError(this, new ErrorEventArgs(err.Message.ToString()));
                                }
                                else
                                {
                                    throw new ApplicationException(err.Message.ToString());
                                }
                            }
                            finally
                            {
                                if (myConn.State != System.Data.ConnectionState.Closed)
                                    myConn.Close();
                            }
                        }
                    }
                    #endregion
                    #region audit_box_header
                    // audit_box_header
                    myCommand.CommandText = "DELETE FROM audit_box_header WHERE order_number = ?ORDERNUMBER and box_number <> '1'";
                    myCommand.Parameters.Clear();
                    myCommand.Parameters.AddWithValue("?ORDERNUMBER", this.orderNumber);
                    try
                    {
                        myConn.Open();
                        myCommand.ExecuteNonQuery();
                    }
                    catch(Exception err)
                    {
                        if(OnError != null)
                        {
                            OnError(this, new ErrorEventArgs(err.Message.ToString()));
                        }
                        else
                        {
                            throw new ApplicationException(err.Message.ToString());
                        }
                    }
                    finally
                    {
                        if(myConn.State != System.Data.ConnectionState.Closed)
                            myConn.Close();
                    }

                    myCommand.CommandText = "UPDATE audit_box_header" +
                        " SET box_weight = ?BOXWEIGHT WHERE order_number = ?ORDERNUMBER and box_number = '1'";
                    myCommand.Parameters.Clear();
                    myCommand.Parameters.AddWithValue("?ORDERNUMBER", this.orderNumber);
                    myCommand.Parameters.AddWithValue("?BOXWEIGHT", GetBoxWeight(1));
                    try
                    {
                        myConn.Open();
                        myCommand.ExecuteNonQuery();
                    }
                    catch(Exception err)
                    {
                        if(OnError != null)
                        {
                            OnError(this, new ErrorEventArgs(err.Message.ToString()));
                        }
                        else
                        {
                            throw new ApplicationException(err.Message.ToString());
                        }
                    }
                    finally
                    {
                        if(myConn.State != System.Data.ConnectionState.Closed)
                            myConn.Close();
                    }

                    foreach(int i in Box.Keys)
                    {
                        if(i > 1)
                        {
                            myCommand.CommandText = "INSERT INTO audit_box_header" +
                                " (order_number, box_weight, box_number)" +
                                " VALUES (?ORDERNUMBER, ?BOXWEIGHT, ?BOXNUMBER)";
                            myCommand.Parameters.Clear();
                            myCommand.Parameters.AddWithValue("?ORDERNUMBER", this.orderNumber);
                            myCommand.Parameters.AddWithValue("?BOXWEIGHT", GetBoxWeight(i));
                            myCommand.Parameters.AddWithValue("?BOXNUMBER", i);
                            try
                            {
                                myConn.Open();
                                myCommand.ExecuteNonQuery();
                            }
                            catch(Exception err)
                            {
                                if(OnError != null)
                                {
                                    OnError(this, new ErrorEventArgs(err.Message.ToString()));
                                }
                                else
                                {
                                    throw new ApplicationException(err.Message.ToString());
                                }
                            }
                            finally
                            {
                                if(myConn.State != System.Data.ConnectionState.Closed)
                                    myConn.Close();
                            }
                        }
                    }
                    #endregion

                    #region audit_header
                    //audit_header
                    string myToday = DateTime.Today.Year.ToString() + "-" + DateTime.Today.Month.ToString().PadLeft(2, '0') + "-" + DateTime.Today.Day.ToString().PadLeft(2, '0');
                    myCommand.CommandText = "UPDATE audit_header SET status = 3, audit_date = ?TODAYSDT WHERE order_number = ?ORDERNUMBER";
                    myCommand.Parameters.Clear();
                    myCommand.Parameters.AddWithValue("?ORDERNUMBER", this.orderNumber);
                    myCommand.Parameters.AddWithValue("?TODAYSDT", myToday);
                    try
                    {
                        myConn.Open();
                        myCommand.ExecuteNonQuery();
                    }
                    catch(Exception err)
                    {
                        if(OnError != null)
                        {
                            OnError(this, new ErrorEventArgs(err.Message.ToString()));
                        }
                        else
                        {
                            throw new ApplicationException(err.Message.ToString());
                        }
                    }
                    finally
                    {
                        if(myConn.State != System.Data.ConnectionState.Closed)
                            myConn.Close();
                    }

                    #endregion

                    #region audit_detail
                    // audit_detail
                    foreach(ItemOrdered itmOrdrd in ItemsOrdered.Values)
                    {
                        myCommand.CommandText = "UPDATE audit_detail" +
                            " SET scanned_qty = ?SCANNEDQTY" +
                            " WHERE order_number = ?ORDERNUMBER AND upc_number = ?UPCNUMBER";
                        myCommand.Parameters.Clear();
                        myCommand.Parameters.AddWithValue("?ORDERNUMBER", this.orderNumber);
                        myCommand.Parameters.AddWithValue("?SCANNEDQTY", itmOrdrd.ScannedQTY);
                        myCommand.Parameters.AddWithValue("?UPCNUMBER", itmOrdrd.UPCNumber);
                        try
                        {
                            myConn.Open();
                            myCommand.ExecuteNonQuery();
                        }
                        catch(Exception err)
                        {
                            if(OnError != null)
                            {
                                OnError(this, new ErrorEventArgs(err.Message.ToString()));
                            }
                            else
                            {
                                throw new ApplicationException(err.Message.ToString());
                            }
                        }
                        finally
                        {
                            if(myConn.State != System.Data.ConnectionState.Closed)
                                myConn.Close();
                        }
                    }

                    #endregion

                    #region sscc_shipping_number

                    myCommand.CommandText = "DELETE FROM sscc_shipping_number WHERE order_number = ?ORDERNUMBER and box_number >= ?NUMBOXS";
                    myCommand.Parameters.Clear();
                    myCommand.Parameters.AddWithValue("?ORDERNUMBER", this.orderNumber);
                    myCommand.Parameters.AddWithValue("?NUMBOXS", this.GetNextBoxNumber());
                    try
                    {
                        myConn.Open();
                        myCommand.ExecuteNonQuery();
                    }
                    catch(Exception err)
                    {
                        if(OnError != null)
                        {
                            OnError(this, new ErrorEventArgs(err.Message.ToString()));
                        }
                        else
                        {
                            throw new ApplicationException(err.Message.ToString());
                        }
                    }
                    finally
                    {
                        if(myConn.State != System.Data.ConnectionState.Closed)
                            myConn.Close();
                    }

                    #endregion
                }
            }


            //Added 3/30/2015 by Jason
            //If order is finished, write logged errors to database
            if (GetCustomer(this.orderNumber) == Customer.WalmartUS)
            //if (1 == 2)
            {
                logMissingItems();
                int needed = 0;
                int converted = 0;
                foreach (ItemOrdered item in ItemsOrdered.Values)
                {
                    needed += item.OrderedQTY;
                    converted += item.OrderedQTY * item.ConversionFactor;
                }
                myLogger.LogWrite(needed, converted);
            }


            // Delete items from order marked out of stock in mysql/webpage
            string webpage = "http://192.168.123.21/stockfix.php?ordernumber=" + orderNumber;
            Console.WriteLine(webpage);
            System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(webpage);
            System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse();
            Console.WriteLine(response.StatusCode);
            request.Abort();
            response.Close();

            // OrderFinished EVENT
            if (OnOrderFinished != null)
            {
                OnOrderFinished(this, new EventArgs());
            }

        }

        public int GetNextBoxNumber()
        {
            int nextBoxNumber = 0;
            foreach (int i in Box.Keys)
            {
                if (i > nextBoxNumber)
                {
                    nextBoxNumber = i;
                }
            }
            return ++nextBoxNumber;
        }

        public string[] GetItemsOrdered()
        {
            List<string> items = new List<string>();
            foreach(ItemOrdered itOrd in ItemsOrdered.Values)
            {
                items.Add(itOrd.ToString());
            }
            items.Sort();
            return items.ToArray();
        }

        public string[] GetMissingItems()
        {
            List<string> items = new List<string>();
            foreach(ItemOrdered itOrd in ItemsOrdered.Values)
            {
                if(itOrd.ScannedQTY < itOrd.OrderedQTY)
                {
                    items.Add(itOrd.ToString());
                    //if bool myLoggedMissing is true then missing items already logged
                    //if not, log them
                    //if (!myLoggedMissing)
                    //    myLogger.LogItem(itOrd.ItemNumber, itOrd.ItemDescription, itOrd.UPCNumber, itOrd.BinLOC, itOrd.ConversionFactor, MistakeLogger.Item.Reason.Missing);
                }
            }
            //Set bool myLoggedMissing to true so will not log missing items if sheet printed again
            //myLoggedMissing = true;
            logMissingItems();

            items.Sort();

            return items.ToArray();
        }

        public string[] GetMissingItemPrintSheet()
        {
            int currLine = 0;
            List<string> missingItemsPages = new List<string>();
            StringBuilder sb = null;

            foreach(ItemOrdered itOrd in ItemsOrdered.Values)
            {
                if(sb == null)
                {
                    sb = new StringBuilder();
                    sb.AppendLine("Page " + ( missingItemsPages.Count + 1 ));
                    sb.AppendLine();
                    sb.AppendLine("Order Number: " + this.orderNumber);
                    sb.AppendLine("Bin Loc".PadRight(13) + "Item".PadRight(12) + "Description".PadRight(31) + "Ordered".PadRight(10) + "Scanned".PadRight(10));
                }

                if(itOrd.ScannedQTY < itOrd.OrderedQTY)
                {
                    sb.Append(itOrd.BinLOC.PadRight(13));
                    sb.Append(itOrd.ItemNumber.PadRight(12));
                    sb.Append(itOrd.ItemDescription.PadRight(31));
                    sb.Append(itOrd.OrderedQTY.ToString().PadRight(10));
                    sb.AppendLine(itOrd.ScannedQTY.ToString().PadRight(10));

                    if(currLine > 58)
                    {
                        currLine = 0;
                        missingItemsPages.Add(sb.ToString());
                        sb = null;
                    }
                    else
                    {
                        currLine++;
                    }
                    //if bool myLoggedMissing is true then missing items already logged
                    //if not, log them
                    //if (!myLoggedMissing)
                    //    myLogger.LogItem(itOrd.ItemNumber, itOrd.ItemDescription, itOrd.UPCNumber, itOrd.BinLOC, itOrd.ConversionFactor, MistakeLogger.Item.Reason.Missing);
                }
            }
            //Set bool myLoggedMissing to true so will not log missing items if sheet printed again
            //myLoggedMissing = true;
            logMissingItems();

            if(currLine > 0)
            {
                missingItemsPages.Add(sb.ToString());
            }

            return missingItemsPages.ToArray();
        }

        public string[] GetBoxItems(int boxNumber)
        {
            List<string> items = new List<string>();
            foreach(ItemScanned itScn in Box[boxNumber].Values)
            {
                items.Add(itScn.ToString());
            }
            return items.ToArray();
        }

        public void EmptyBox(int boxNumber)
        {
            foreach (string upc in Box[boxNumber].Keys)
            {
                if (ItemsOrdered.ContainsKey(upc) && Box[boxNumber].ContainsKey(upc))
                {
                    ItemsOrdered[upc].ScannedQTY -= Box[boxNumber][upc].ScannedQTY;
                    //Box[boxNumber].Remove(upc);
                }
            }
            Box[boxNumber].Clear();

            if (OnOrderChanged != null)
            {
                OnOrderChanged(this, new EventArgs());
            }
        }

        public void RemoveItemFromBox(int boxNumber, string containsUPC)
        {
            int pos = containsUPC.IndexOf("016321");
            string upc = containsUPC.Substring(pos, 12);
            
            if(ItemsOrdered.ContainsKey(upc) && Box[boxNumber].ContainsKey(upc))
            {
                ItemsOrdered[upc].ScannedQTY -= Box[boxNumber][upc].ScannedQTY;
                Box[boxNumber].Remove(upc);
            }

            if(Box[boxNumber].Values.Count < 1)
            {
                RemoveBox(boxNumber);
            }

            if(OnOrderChanged != null)
            {
                OnOrderChanged(this, new EventArgs());
            }
        }

        public void RemoveBox(int boxNumber)
        {
            Box.Remove(boxNumber);
            int newBoxNumber = 1;
            Dictionary<int, Dictionary<string, ItemScanned>> newBox = new Dictionary<int, Dictionary<string, ItemScanned>>();
            foreach (int i in Box.Keys)
            {
                newBox.Add(newBoxNumber++, Box[i]);
            }
            Box = newBox;
        }

        public void Kill()
        {
            if(OnOrderKilled != null)
            {
                OnOrderKilled(this, new EventArgs());
            }
        }

        #endregion


        #region Overides

        public override string ToString()
        {
            return this.orderNumber;
        }

        #endregion


        #region Classes

        public class ItemOrdered
        {
            readonly string itemNumber;
            readonly string itemDescription;
            readonly string upcNumber;
            readonly float weight;
            readonly int conversionFactor;
            readonly string binLoc;

            int orderedQTY;
            int scannedQTY = 0;

            public ItemOrdered(string itemNumber, string itemDescription, string upcNumber, int orderedQTY, float weight, int conversionFactor, string binLoc)
            {
                this.itemNumber = itemNumber;
                this.itemDescription = itemDescription;
                this.upcNumber = upcNumber;
                this.orderedQTY = orderedQTY;
                this.weight = weight;
                this.conversionFactor = conversionFactor;
                this.binLoc = binLoc;
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(upcNumber.PadRight(13));
                sb.Append(itemDescription.PadRight(31));
                sb.Append("Ordered: " + orderedQTY.ToString().PadRight(4));
                sb.Append("Scanned: " + scannedQTY.ToString().PadRight(4));
                return sb.ToString();
            }

            public string ItemNumber
            {
                get
                {
                    return itemNumber;
                }
            }

            public string ItemDescription
            {
                get
                {
                    return itemDescription;
                }
            }

            public string UPCNumber
            {
                get
                {
                    return upcNumber;
                }
            }

            public int OrderedQTY
            {
                get
                {
                    return orderedQTY;
                }
                set
                {
                    orderedQTY = value;
                }
            }

            public float Weight
            {
                get
                {
                    return weight;
                }
            }

            public int ScannedQTY
            {
                get
                {
                    return scannedQTY;
                }
                set
                {
                    scannedQTY = value;
                }
            }

            public int ConversionFactor
            {
                get
                {
                    return conversionFactor;
                }
            }

            public string BinLOC
            {
                get
                {
                    return binLoc;
                }
            }
        }

        public class ItemScanned
        {
            string upcNumber;
            string itemNumber;
            string description;
            int scannedQTY;
            int orderedQTY;
            float scannedWeight;

            public ItemScanned(string UPC, string itemNumber, string description,int orderedQTY, int scannedQTY, float scannedWeight)
            {
                this.upcNumber = UPC;
                this.itemNumber = itemNumber;
                this.description = description;
                this.scannedQTY = scannedQTY;
                this.orderedQTY = orderedQTY;
                this.scannedWeight = scannedWeight;
            }

            public string UPCNumber
            {
                get
                {
                    return upcNumber;
                }
            }

            public string ItemNumber
            {
                get
                {
                    return itemNumber;
                }
            }

            public string Description
            {
                get
                {
                    return description;
                }
            }

            public int ScannedQTY
            {
                get
                {
                    return scannedQTY;
                }
                set
                {
                    scannedQTY = value;
                }
            }

            public float ScannedWeight
            {
                get
                {
                    return scannedWeight;
                }
                set
                {
                    scannedWeight = value;
                }
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(upcNumber.PadRight(13));
                sb.Append(description.PadRight(31));
                sb.Append("Ordered: " + orderedQTY.ToString().PadRight(4));
                sb.Append("Scanned: " + scannedQTY.ToString().PadRight(4));
                return sb.ToString();
            }
        } // end class ItemScanned

        public class UPCScannedEventArgs : EventArgs
        {
            readonly string upcNumber;
            readonly int boxNumber;
            readonly int conversionFactor;
            readonly int scannedQTY;
            readonly int orderedQTY;
            readonly bool orderFinished;

            public UPCScannedEventArgs(string upcNumber, int boxNumber, int conversionFactor, int scannedQTY, int orderedQTY, bool orderFinished)
            {
                this.upcNumber = upcNumber;
                this.boxNumber = boxNumber;
                this.conversionFactor = conversionFactor;
                this.scannedQTY = scannedQTY;
                this.orderedQTY = orderedQTY;
                this.orderFinished = orderFinished;
            }

            public string UPCNumber
            {
                get
                {
                    return upcNumber;
                }
            }

            public int BoxNumber
            {
                get
                {
                    return boxNumber;
                }
            }

            public int ConversionFactor
            {
                get
                {
                    return conversionFactor;
                }
            }

            public int ScannedQTY
            {
                get
                {
                    return scannedQTY;
                }
            }

            public int OrderedQTY
            {
                get
                {
                    return orderedQTY;
                }
            }

            public bool OrderFinished
            {
                get
                {
                    return orderFinished;
                }
            }
        }

        public class ErrorEventArgs : EventArgs
        {
            readonly string message;

            public ErrorEventArgs(string message)
            {
                this.message = message;
            }

            public string Message
            {
                get
                {
                    return this.message;
                }
            }
        }
        //-------------------------------------------------------------------------------------------------------------------------------------------------
        public class MistakeLogger
        {
            //Setup MySQL items here
            public const string myLoggerDbString = "server=192.168.123.24;user id=audit;password=audit;database=audit";

            //Items object will hold individual Item objects
            private Dictionary<string, Item> Items;

            public string OrderNumber { get; private set; }

            //Constructor
            public MistakeLogger(string orderNumber)
            {
               Items = new Dictionary<string, Item>();
               this.OrderNumber = orderNumber;
            }

            //Object.ToString() will return the Order Number
            public override string ToString()
            {
                return this.OrderNumber;
            }

            //Add Item
            public void LogItem(string number, string description, string upc, string binLoc, int conversionFactor, Item.Reason problem)
            {
                if(Items.ContainsKey(upc))
                    Items[upc].Scan();
                else
                    Items.Add(upc, new Item(number, description, binLoc, conversionFactor, problem));
            }

            //Commit items to log table
            public void LogWrite(int orderScans, int orderConvFactor)
            {
                //check to see if there are any errors and if not log a perfect audit
                if (this.Items.Count < 1)
                    LogItem("1111111111", "This order had no mistakes", "016321111111", "", 0, Item.Reason.Perfect);

                using (MySqlConnection myLoggerDbCon = new MySqlConnection(myLoggerDbString))
                {
                    using (MySqlCommand myCommand = myLoggerDbCon.CreateCommand())
                    {
                        foreach (string upc in Items.Keys)
                        {
                            myCommand.CommandText = "INSERT INTO log (order_number, audit_date, item, description, upc, problem, scans, conversion_factor)" +
                                " VALUES (?ORDERNUMBER, ?AUDITDATE, ?ITEM, ?DESCRIPTION, ?UPC, ?PROBLEM, ?SCANS, ?CONVERSIONFACTOR)";
                            myCommand.Parameters.Clear();
                            myCommand.Parameters.AddWithValue("?ORDERNUMBER", this.OrderNumber);
                            myCommand.Parameters.AddWithValue("?AUDITDATE", DateTime.Today);
                            myCommand.Parameters.AddWithValue("?ITEM", Items[upc].Number);
                            myCommand.Parameters.AddWithValue("?DESCRIPTION", Items[upc].Description);
                            myCommand.Parameters.AddWithValue("?UPC", upc);
                            myCommand.Parameters.AddWithValue("?PROBLEM", Items[upc].Problem);
                            myCommand.Parameters.AddWithValue("?SCANS", Items[upc].Scans);
                            myCommand.Parameters.AddWithValue("?CONVERSIONFACTOR", Items[upc].ConversionFactor);
                            try
                            {
                                myLoggerDbCon.Open();
                                myCommand.ExecuteNonQuery();
                            }
                            finally
                            {
                                if (myLoggerDbCon.State != System.Data.ConnectionState.Closed)
                                    myLoggerDbCon.Close();
                            }
                        }
                    }
                }
                //This second query inserts order information into the table log_scanned_order_info
                using (MySqlConnection myLoggerDbCon = new MySqlConnection(myLoggerDbString))
                {
                    using (MySqlCommand myCommand = myLoggerDbCon.CreateCommand())
                    {
                            myCommand.CommandText = "INSERT INTO log_scanned_order_info (order_number, audit_date, scans, conversion_factor)" +
                                " VALUES (?ORDERNUMBER, ?AUDITDATE, ?SCANS, ?CONVERSIONFACTOR)";
                            myCommand.Parameters.Clear();
                            myCommand.Parameters.AddWithValue("?ORDERNUMBER", this.OrderNumber);
                            myCommand.Parameters.AddWithValue("?AUDITDATE", DateTime.Today);
                            myCommand.Parameters.AddWithValue("?SCANS", orderScans);
                            myCommand.Parameters.AddWithValue("?CONVERSIONFACTOR", orderConvFactor);
                            try
                            {
                                myLoggerDbCon.Open();
                                myCommand.ExecuteNonQuery();
                            }
                            finally
                            {
                                if (myLoggerDbCon.State != System.Data.ConnectionState.Closed)
                                    myLoggerDbCon.Close();
                            }
                    }
                }
            }

            //A class that represents an item object
            public class Item
            {
                //unsettable properties
                public string Number { get; private set; }
                public string Description { get; private set; }
                //public string UPC { get; private set; }
                public string BinLoc { get; private set; }
                public int ConversionFactor { get; private set; }
                public int Scans { get; private set; }
                public DateTime AuditDate { get; private set; }
                public string Problem { get; private set; }

                //Reason item is being logged
                public enum Reason { Overscan, Missing, Wrong, Perfect }

                //Constructor, set defaults for item
                public Item(string number, string description, string binLoc, int conversionFactor, Reason problem)
                {
                    this.Scans = 1;
                    this.AuditDate = DateTime.Today;

                    this.Number = number;
                    this.Description = description;
                    //this.UPC = upc;
                    this.BinLoc = binLoc;
                    this.ConversionFactor = conversionFactor;
                    switch (problem)
                    {
                        case Reason.Overscan:
                            this.Problem = "overscan";
                            break;
                        case Reason.Missing:
                            this.Problem = "missing";
                            break;
                        case Reason.Wrong:
                            this.Problem = "wrong";
                            break;
                        case Reason.Perfect:
                            this.Problem = "perfect";
                            break;
                        default:
                            this.Problem = "error";
                            break;
                    }
                    //this.Problem = problem;
                }

                public void Scan()
                {
                    ++Scans;
                }
            }
            //-------------------------------------------------------------------------------------------------------------------------------------------------





            //A class to hold all Walmart Items
            //Will be used to log the info of items scanned but not in order
            /*
            private class WalmartItems
            {
                private class Item
                {
                    private string upc;
                    private string number;
                    private string description;
                    private string cost;
                    private string price;
                    private string wmBayLoc;

                    //Constructor
                    public Item(string upc)
                    {
                        this.upc = upc;
                    }

                    //UPC
                    public string GetUpc()
                    {
                        return this.upc;
                    }
                    //Item Number
                    public void Number(string number)
                    {
                        this.number = number;
                    }
                    public string Number()
                    {
                        if (this.number.Length > 0)
                        {
                            return this.number;
                        }
                        else
                        {
                            return "";
                        }
                    }
                    //Item Description
                    public void Description(string description)
                    {
                        this.description = description;
                    }
                    public string Description()
                    {
                        if (this.description.Length > 0)
                        {
                            return this.description;
                        }
                        else
                        {
                            return "";
                        }
                    }
                    //Item Cost
                    public void Cost(string cost)
                    {
                        this.cost = cost;
                    }
                    public string Cost()
                    {
                        if (this.cost.Length > 0)
                        {
                            return this.cost;
                        }
                        else
                        {
                            return "";
                        }
                    }
                    //Item Price
                    public void Price(string price)
                    {
                        this.price = price;
                    }
                    public string Price()
                    {
                        if (this.price.Length > 0)
                        {
                            return this.price;
                        }
                        else
                        {
                            return "";
                        }
                    }
                    //WM Bay Location
                    public void WmBayLoc(string wmBayLoc)
                    {
                        this.wmBayLoc = wmBayLoc;
                    }
                    public string WmBayLoc()
                    {
                        if (this.wmBayLoc.Length > 0)
                        {
                            return this.wmBayLoc;
                        }
                        else
                        {
                            return "";
                        }
                    }
                }//End Class Item

                private class Items
                {
                    //Constructor
                    public Items()
                    {

                    }

                    public void AddItem(Item item)
                    {

                    }
                    public Item GetItem(string upc)
                    {
                        return new Item(upc);
                    }

                }//End class Items
            }//End class WalmartItems
            */

        }

        #endregion
    }

}
