using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cousin.Audit.Logger
{
    public class Mistakes
    {
        //Setup MySQL items here
        public const string ConnectionString = "server=192.168.123.24;user id=audit;password=audit;database=audit";

        //Setup MS SQL items here
        string myConnectionString = "Data Source=srv-swmssql;Integrated Security=False;User ID=audit;Password=audit;Connect Timeout=15;Encrypt=False;TrustServerCertificate=False";
        string myQueryString = "SELECT stocknumber, stockdescription1, pricingmethod, itemprice_1, itemprice_2, " +
                "standardcost, lastcost, averagecost, listpricemult, upc, walmartbayloc, walmartcsunit " +
                "FROM dbo.j_WMAuditData";
        SqlCommand myCommand;
        SqlDataReader myReader;

        //Constructor
        public Mistakes()
        {
            List<String> rowData = new List<string>();

            using (SqlConnection myConnection = new SqlConnection())
            {
                myConnection.ConnectionString = myConnectionString;
                myCommand = new SqlCommand(myQueryString, myConnection);
                myConnection.Open();
                myReader = myCommand.ExecuteReader();
                while (myReader.Read())
                {
                    //myReader.GetOrdinal("stocknumber");
                }
            }
        }

        //A class that represents an item object
        private class Item
        {
            //unsettable properties
            public string Number { get; private set; }
            public string Description { get; private set; }
            public string UPC { get; private set; }
            public string BinLoc { get; private set; }
            public int ConversionFactor { get; private set; }
            public int Scans { get; private set; }
            public DateTime AuditDate { get; private set; }
            public Reason Problem { get; private set; }

            //Reason item is being logged
            private enum Reason { Overscan, Missing, Wrong }

            //Constructor, set defaults for item
            public Item(string number, string description, string upc, string binLoc, int conversionFactor, Reason problem)
            {
                this.Scans = 0;
                this.AuditDate = DateTime.Today;

                this.Number = number;
                this.Description = description;
                this.UPC = upc;
                this.BinLoc = binLoc;
                this.ConversionFactor = conversionFactor;
                this.Problem = problem;
            }

            public void Scan()
            {
                ++Scans;
            }
        }






        //A class to hold all Walmart Items
        //Will be used to log the info of items scanned but not in order
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

    }
}
