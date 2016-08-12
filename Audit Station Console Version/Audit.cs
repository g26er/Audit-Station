using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Media;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Printing;

using Cousin.Audit;

namespace Audit_Station_Console_Version
{
    public class Audit: IDisposable
    {
        #region Fields and Objects
        private bool disposing = false;
        string txtToPrint = string.Empty;
        Order myOrder;
        SoundPlayer myDingPlayer = new SoundPlayer();
        string scannedText;
        bool Exit = false;
        int currentBoxNumber = 1;
        PrintDocument myPrinter = new PrintDocument();
        string lastUpcScanned = "";

        ////Timer
        //System.Timers.Timer closeOrderTimer;
        //int currDayOfYear;
        ////End Timer

        #endregion


        #region Constructor

        public Audit()
        {
            //March 18th, 2015 edited app title to make it easier to know if a station has LE version - Jason
            Console.Title = " Audit Station " + Application.ProductVersion + " LE - Logging Edition";

            myPrinter.PrintPage += new PrintPageEventHandler(myPrinter_PrintPage);

            Order.OnMySqlError += new Order.MySQLErrorHandler(Order_OnMySqlError);
            Order.OnError += new Order.ErrorHandler(Order_OnError);

            //if (Properties.Settings.Default.PlayGunshot)
            //{
            //    myDingPlayer.Stream = Properties.Resources.gunshot;
            //}
            //else
            //{
            //    myDingPlayer.Stream = Properties.Resources.ding;
            //}

            /* TIMER NOT USED ANYMORE
            //Timer
            closeOrderTimer = new System.Timers.Timer(3600000);
            closeOrderTimer.Elapsed += new System.Timers.ElapsedEventHandler(closeOrderTimer_Elapsed);
            if (closeOrderTimer.Enabled == false)
            {
                closeOrderTimer.Enabled = true;
            }
            closeOrderTimer.Start();
            currDayOfYear = DateTime.Now.DayOfYear;
            //End Timer
            */
        }

        #endregion


        public void Run()
        {
                //Console.Clear();
                //Console.WriteLine();
                //Console.WriteLine("INITIALIZING...");
                //Shipments.Close();
                Console.Clear();
                Console.WriteLine();
                Console.WriteLine("Please scan an order to begin");
                Console.WriteLine("Press H for help at anytime");
                do
                {
                    Console.Write(">> ");
                    scannedText = Console.ReadLine().ToLower();

                    #region Order Scanned   "^(900|999)\d{6}$"

                    if (Regex.IsMatch(scannedText, @"^(999|900|800|700)\d{6}$"))
                    {
                        if (myOrder == null)
                        {
                            if (Order.Exists(scannedText))
                            {
                                if (Order.ASNSent(scannedText))
                                {
                                    Console.WriteLine();
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("ASN has already been send for order {0}", scannedText);
                                    Console.WriteLine("Can not reaudit this order");
                                    Console.ResetColor();
                                    continue; // Quit here and go back to beginning of RUN loop
                                }
                                if (Order.GetStatus(scannedText) == 3)
                                {
                                    Console.WriteLine();
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("Order {0} has already been audited", scannedText);
                                    Console.WriteLine("Would you like to audit this order again?");
                                    Console.ResetColor();
                                    switch (GetYorN())
                                    {
                                        case "n":
                                            Console.WriteLine();
                                            Console.WriteLine("Audit of order {0} was canceled", scannedText);
                                            continue; // Quit here and go back to beginning of RUN loop
                                        default:
                                            break;
                                    }
                                    if (Order.GetAuditDate(scannedText) != DateTime.Today)
                                    {
                                        Console.WriteLine();
                                        Console.ForegroundColor = ConsoleColor.Yellow;
                                        Console.WriteLine("Order {0} was not audited today", scannedText);
                                        Console.WriteLine("Can not reaudit an old order");
                                        Console.ResetColor();
                                        continue; // Quit here and go back to beginning of RUN loop
                                    }
                                }
                                StartOrder(scannedText);
                                PlayDing();
                                Console.Clear();
                                Console.WriteLine();
                                Console.WriteLine("Order {0} is now active - ready to scan products", myOrder.ToString());
                                continue;
                            }
                            else
                            {
                                PlayError();
                                Console.WriteLine();
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Order {0} does not exist", scannedText);
                                Console.ResetColor();
                                continue;
                            }
                        }
                        else
                        {
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Order {0} is currently active, Please", myOrder.ToString());
                            Console.WriteLine("finish this order before starting a new order");
                            Console.ResetColor();
                            continue;
                        }
                    }

                    #endregion

                    #region Help    H

                    else if (scannedText.Equals("h"))
                    {
                        ShowHelp();
                        continue;
                    }

                    #endregion

                    #region UPC Scanned "^016321\d{6}$"

                    else if (Regex.IsMatch(scannedText, @"^\d{12}$") || Regex.IsMatch(scannedText, @"^400044\d{6}$"))
                    {
                        if (myOrder == null)
                        {
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Please begin an order before scanning a product");
                            Console.ResetColor();
                        }
                        else
                        {
                            lastUpcScanned = scannedText;
                            myOrder.ScanItem(scannedText, currentBoxNumber);
                        }
                        continue;
                    }

                    #endregion

                    #region New Box NB

                    else if (scannedText.Equals("nb") || scannedText.Equals("."))
                    {
                        if (myOrder != null)
                        {
                            if (myOrder.BoxHasScans(currentBoxNumber))
                            {
                                Console.WriteLine();
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("Are you sure you want to start box {0}?", currentBoxNumber + 1);
                                Console.ResetColor();
                                switch (GetYorN())
                                //switch ("y")
                                {
                                    case "y":
                                        currentBoxNumber++;
                                        PlayDing();
                                        Console.WriteLine();
                                        Console.WriteLine("Now on box {0}", currentBoxNumber);
                                        break;
                                    default:
                                        Console.WriteLine();
                                        Console.WriteLine("Staying on box {0}", currentBoxNumber);
                                        break;
                                }
                            }
                            else
                            {
                                Console.WriteLine();
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("Can't start box {0} there are no items in box {1} yet", currentBoxNumber + 1, currentBoxNumber);
                                Console.ResetColor();
                            }
                        }
                        else
                        {
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Currently no active order");
                            Console.ResetColor();
                        }
                        continue;
                    }

                    #endregion

                    #region Release Order   R

                    else if (scannedText.Equals("r"))
                    {
                        if (myOrder == null)
                        {
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Currently no active order");
                            Console.ResetColor();
                        }
                        else
                        {
                            if (myOrder.OrderHasScans())
                            {
                                Console.WriteLine();
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("Are you sure you want to release {0} as incomplete?", myOrder.ToString());
                                Console.ResetColor();
                                switch (GetYorN())
                                {
                                    case "y":
                                        myOrder.FinishOrder();
                                        break;
                                    default:
                                        Console.WriteLine();
                                        Console.WriteLine("Canceled");
                                        break;
                                }
                            }
                            else
                            {
                                Console.WriteLine();
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("Are you sure you want to release {0} as complete, one box?", myOrder.ToString());
                                Console.ResetColor();
                                switch (GetYorN())
                                {
                                    case "y":
                                        myOrder.ReleaseCompleteOneBox();
                                        break;
                                    default:
                                        Console.WriteLine();
                                        Console.WriteLine("Canceled");
                                        break;
                                }
                            }
                        }
                        continue;
                    }

                    #endregion

                    #region Empty Current Box   ECB

                    else if (scannedText.ToLower().Equals("ecb"))
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        if (myOrder != null)
                        {
                            //    Console.WriteLine("Order {0} is currently active", myOrder.ToString());
                            Console.WriteLine("Do you want to empty box " + currentBoxNumber + "?");
                            //Console.ResetColor();
                            switch (GetYorN())
                            {
                                case "y":
                                    myOrder.EmptyBox(currentBoxNumber);
                                    Console.WriteLine("Box " + currentBoxNumber + " is now empty");
                                    Console.WriteLine("Please scan items for box " + currentBoxNumber);
                                    break;
                                default:
                                    Console.WriteLine();
                                    Console.WriteLine("Canceled");
                                    break;
                            }
                        }
                        Console.ResetColor();
                        continue;
                    }

                    #endregion

                    #region Exit    X

                    else if (scannedText.ToLower().Equals("x"))
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        if (myOrder != null)
                        {
                            Console.WriteLine("Order {0} is currently active", myOrder.ToString());
                            Console.WriteLine("Do you want to deactivate this order?");
                            Console.ResetColor();
                            switch (GetYorN())
                            {
                                case "y":
                                    myOrder.Kill();
                                    break;
                                default:
                                    Console.WriteLine();
                                    Console.WriteLine("Canceled");
                                    break;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Are you sure you want to quit?");
                            Console.ResetColor();
                            switch (GetYorN())
                            {
                                case "y":
                                    Exit = true;
                                    break;
                                default:
                                    Console.WriteLine();
                                    Console.WriteLine("Canceled");
                                    break;
                            }
                        }
                        continue;
                    }

                    #endregion

                    #region Print Labels    L
                    else if (scannedText.Equals("l"))
                    {
                        // If order is active DO NOT print labels
                        if (myOrder != null)
                        {
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Please wait until current active order");
                            Console.WriteLine("is finished before reprinting labels");
                            Console.ResetColor();
                        }
                        else
                        {
                            // Fields for label reprint
                            bool okToGoLabel = false;
                            string typedLine;
                            string orderNumber;

                            // Get order number
                            Console.WriteLine();
                            do
                            {
                                Console.Write("Order Number: ");
                                typedLine = Console.ReadLine().ToLower();
                                if (typedLine.Equals("x") || Regex.IsMatch(typedLine, @"^(900|999)\d{6}$"))
                                {
                                    okToGoLabel = true;
                                }
                                else
                                {
                                    Console.WriteLine();
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("Not a valid order number");
                                    Console.WriteLine("Please try again        ");
                                    Console.ResetColor();
                                }
                            } while (!okToGoLabel);

                            // If they typed x continue else setup ordernumber
                            if (typedLine.Equals("x"))
                            {
                                Console.WriteLine();
                                Console.WriteLine("Label reprint canceled by user");
                            }
                            else
                            {
                                orderNumber = typedLine;

                                // Does order exist
                                if (Order.Exists(orderNumber))
                                {
                                    // Is order FINISHED
                                    if (Order.GetStatus(orderNumber) >= 3)
                                    {
                                        // Get customer and print labels
                                        Cousin.ASN.ASN myASN = new Cousin.ASN.ASN();
                                        int numOfBoxes = 0;
                                        switch (Order.GetCustomer(orderNumber))
                                        {
                                            //commented out 3 lines of code below and added michaels ca code on aug 12 2010
                                            case Order.Customer.MichaelsUS:
                                                Console.WriteLine("Please wait while label(s) prints");
                                                myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.MichaelsCA, orderNumber, myASN.GetNumBoxes_Order(Cousin.ASN.ASN.Customer.MichaelsCA, orderNumber));
                                                Console.WriteLine("Done...");
                                                //Console.ForegroundColor = ConsoleColor.Yellow;
                                                //Console.WriteLine("Labels are not needed for Michaels non Canada");
                                                //Console.ResetColor();
                                                break;
                                            case Order.Customer.Meijer:
                                                Console.WriteLine("Please wait while label(s) prints");
                                                myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.Meijer, orderNumber, myASN.GetNumBoxes_Order(Cousin.ASN.ASN.Customer.Meijer, orderNumber));
                                                Console.WriteLine("Done...");
                                                break;
                                            case Order.Customer.MichaelsCA:
                                                Console.WriteLine("Please wait while label(s) prints");
                                                myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.MichaelsCA, orderNumber, myASN.GetNumBoxes_Order(Cousin.ASN.ASN.Customer.MichaelsCA, orderNumber));
                                                Console.WriteLine("Done...");
                                                break;
                                            case Order.Customer.WalmartUS:
                                                Console.WriteLine("Please wait while label(s) prints");
                                                numOfBoxes = myASN.GetNumBoxes_Order(Cousin.ASN.ASN.Customer.WalMart, orderNumber);
                                                for (int i = 1; i <= numOfBoxes; i++)
                                                {
                                                    myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.WalMart, orderNumber, i);
                                                }
                                                Console.WriteLine("Done...");
                                                break;
                                            case Order.Customer.WalmartCA:
                                                Console.WriteLine("Please wait while label(s) prints");
                                                numOfBoxes = myASN.GetNumBoxes_Order(Cousin.ASN.ASN.Customer.WalmartCA, orderNumber);
                                                for (int i = 1; i <= numOfBoxes; i++)
                                                {
                                                    myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.WalmartCA, orderNumber, i);
                                                }
                                                Console.WriteLine("Done...");
                                                break;
                                            case Order.Customer.Joann:
                                                Console.WriteLine("Please wait while label(s) prints");
                                                numOfBoxes = myASN.GetNumBoxes_Order(Cousin.ASN.ASN.Customer.Joann, orderNumber);
                                                for (int i = 1; i <= numOfBoxes; i++)
                                                {
                                                    myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.Joann, orderNumber, i);
                                                }
                                                Console.WriteLine("Done...");
                                                break;
                                            case Order.Customer.Acmoore:
                                                Console.WriteLine("Please wait while label(s) prints");
                                                numOfBoxes = myASN.GetNumBoxes_Order(Cousin.ASN.ASN.Customer.Acmoore, orderNumber);


                                                //BROKE
                                                //for (int i = 1; i <= numOfBoxes; i++)
                                                //{
                                                //    myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.Acmoore, orderNumber, i);
                                                //}
                                                myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.Acmoore, orderNumber, numOfBoxes);
                                                //BROKE



                                                Console.WriteLine("Done...");
                                                break;
                                            default:
                                                Console.ForegroundColor = ConsoleColor.Yellow;
                                                Console.WriteLine("Labels are not needed for GENERIC customer");
                                                Console.ResetColor();
                                                break;
                                        }
                                    }
                                    else // Order has not been audited yet
                                    {
                                        Console.WriteLine();
                                        Console.ForegroundColor = ConsoleColor.Yellow;
                                        Console.WriteLine("Order {0} has not been audited", orderNumber);
                                        Console.WriteLine("Can not print label(s)");
                                        Console.ResetColor();
                                    }
                                }
                                else // Order doesn't exist
                                {
                                    Console.WriteLine();
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("Order {0} doesn't exist", orderNumber);
                                    Console.ResetColor();
                                }
                            }
                        }
                        continue;
                    }

                    #endregion

                    #region Print Picksheet P

                    else if (scannedText.Equals("p"))
                    {
                        if (myOrder != null)
                        {
                            Console.Clear();
                            foreach (string s in myOrder.GetMissingItemPrintSheet())
                            {
                                Console.WriteLine(s);
                            }
                            Console.Write("Would you like to print this? ");
                            if (GetYorN().Equals("y"))
                            {
                                int numPagesPrinted = 0;
                                Console.WriteLine("Please wait while the picking sheet(s) prints");
                                foreach (string s in myOrder.GetMissingItemPrintSheet())
                                {
                                    txtToPrint = s;
                                    myPrinter.Print();
                                    numPagesPrinted++;
                                }
                                Console.WriteLine("Done printing {0} page(s)", numPagesPrinted);
                            }
                        }
                        else
                        {
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Currently no order active");
                            Console.ResetColor();
                        }
                        continue;
                    }

                    #endregion

                    #region ReWeigh Boxes   W

                    else if (scannedText == "w")
                    {
                        // If order is active DO NOT get weight
                        if (myOrder == null)
                        {
                            // Fields for reweigh
                            bool okToGoLabel = false;
                            string typedLine;
                            string orderNumber;

                            // Get order number
                            Console.WriteLine();
                            do
                            {
                                Console.Write("Order Number: ");
                                typedLine = Console.ReadLine().ToLower();
                                if (typedLine.Equals("x") || Regex.IsMatch(typedLine, @"^(900|999)\d{6}$"))
                                {
                                    okToGoLabel = true;
                                }
                                else
                                {
                                    Console.WriteLine();
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("Not a valid order number");
                                    Console.WriteLine("Please try again        ");
                                    Console.ResetColor();
                                }
                            } while (!okToGoLabel);

                            // If they typed x continue else setup ordernumber
                            if (typedLine.Equals("x"))
                            {
                                Console.WriteLine();
                                Console.WriteLine("Box reweigh canceled by user");
                            }
                            else
                            {
                                orderNumber = typedLine;

                                // Does order exist
                                if (Order.Exists(orderNumber))
                                {
                                    // Is order FINISHED
                                    if (Order.GetStatus(orderNumber) >= 3)
                                    {
                                        // If order is older then today Don't reweigh
                                        //if(Order.GetAuditDate(orderNumber) == DateTime.Today)
                                        //BROKE
                                        if (true)
                                        {
                                            // Get customer and reweigh
                                            Cousin.ASN.ASN myASN = new Cousin.ASN.ASN();
                                            switch (Order.GetCustomer(orderNumber))
                                            {
                                                case Order.Customer.MichaelsUS:
                                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                                    Console.WriteLine("Box weights are not needed for Michaels non Canada");
                                                    Console.ResetColor();
                                                    break;
                                                case Order.Customer.MichaelsCA:
                                                case Order.Customer.Joann:
                                                    foreach (int i in Order.GetBoxNumbers(orderNumber))
                                                    {
                                                        if (i > 0)
                                                        {
                                                            float weight;
                                                            string typedWeight;
                                                            bool okToGo;

                                                            Console.WriteLine();
                                                            Console.WriteLine("Please enter the weight of box {0}", i);
                                                            do
                                                            {
                                                                okToGo = false;

                                                                Console.Write(">> ");
                                                                typedWeight = Console.ReadLine();
                                                                if (float.TryParse(typedWeight, out weight))
                                                                {
                                                                    if (weight > 0 && weight < 100)
                                                                    {
                                                                        Order.SetBoxWeight(orderNumber, i, weight);
                                                                        okToGo = true;
                                                                        continue;
                                                                    }
                                                                }
                                                                Console.WriteLine();
                                                                Console.WriteLine("Please enter a number between 0 and 100");
                                                            } while (!okToGo);
                                                        }
                                                    }
                                                    Console.WriteLine("Done...");
                                                    Console.WriteLine();
                                                    break;
                                                case Order.Customer.Acmoore:
                                                    foreach (int i in Order.GetBoxNumbers(orderNumber))
                                                    {
                                                        if (i > 0)
                                                        {
                                                            float weight;
                                                            string typedWeight;
                                                            bool okToGo;

                                                            Console.WriteLine();
                                                            Console.WriteLine("Please enter the weight of box {0}", i);
                                                            do
                                                            {
                                                                okToGo = false;

                                                                Console.Write(">> ");
                                                                typedWeight = Console.ReadLine();
                                                                if (float.TryParse(typedWeight, out weight))
                                                                {
                                                                    if (weight > 0 && weight < 100)
                                                                    {
                                                                        Order.SetBoxWeight(orderNumber, i, weight);
                                                                        okToGo = true;
                                                                        continue;
                                                                    }
                                                                }
                                                                Console.WriteLine();
                                                                Console.WriteLine("Please enter a number between 0 and 100");
                                                            } while (!okToGo);
                                                        }
                                                    }
                                                    Console.WriteLine("Done...");
                                                    Console.WriteLine();
                                                    break;
                                                case Order.Customer.Meijer:
                                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                                    Console.WriteLine("Box weights are not needed for Meijer");
                                                    Console.ResetColor();
                                                    break;
                                                case Order.Customer.WalmartUS:
                                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                                    Console.WriteLine("Box weights are not needed for Wal-Mart");
                                                    Console.ResetColor();
                                                    break;
                                                default:
                                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                                    Console.WriteLine("Box weights are not needed GENERIC customer");
                                                    Console.ResetColor();
                                                    break;
                                            }
                                        }
                                        //else // Order is too old
                                        //{
                                        //    Console.WriteLine();
                                        //    Console.ForegroundColor = ConsoleColor.Yellow;
                                        //    Console.WriteLine("Order {0} was not audited today", orderNumber);
                                        //    Console.WriteLine("Can not reweigh box(es)");
                                        //    Console.ResetColor();
                                        //}
                                    }
                                    else // Order has not been audited yet
                                    {
                                        Console.WriteLine();
                                        Console.ForegroundColor = ConsoleColor.Yellow;
                                        Console.WriteLine("Order {0} has not been audited", orderNumber);
                                        Console.WriteLine("Can not reweigh box(es)");
                                        Console.ResetColor();
                                    }
                                }
                                else // Order doesn't exist
                                {
                                    Console.WriteLine();
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("Order {0} doesn't exist", orderNumber);
                                    Console.ResetColor();
                                }
                            }
                        }
                        else // Order is active - cant reweigh
                        {
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Please wait until current active order");
                            Console.WriteLine("is finished before reweighing boxes");
                            Console.ResetColor();
                        }
                        continue;
                    }

                    #endregion

                    //else if (scannedText == "wtf")
                    //{
                    //    System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("http://192.168.123.24/test.php?wtf=epicfail");
                    //    System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse();
                    //    Console.ForegroundColor = ConsoleColor.Magenta;
                    //    Console.WriteLine(response.StatusCode);
                    //    request.Abort();
                    //    response.Close();
                    //}

                    #region PlayDing or Not PD *NOT ON HELP*

                    /*else if (scannedText == "pd")
                    {
                        Console.WriteLine();
                        Console.WriteLine("Turn On / Keep On 'PLAY DING'?");
                        switch (GetYorN())
                        {
                            case "y":
                                Properties.Settings.Default.PlayDing = true;
                                Properties.Settings.Default.PlayGunshot = false;
                                myDingPlayer.Stream = Properties.Resources.ding;
                                Console.WriteLine();
                                Console.WriteLine("'PLAY DING' is now ON");
                                break;
                            case "n":
                                Properties.Settings.Default.PlayDing = false;
                                Console.WriteLine();
                                Console.WriteLine("'PLAY DING' is now OFF");
                                break;
                        }
                        Properties.Settings.Default.Save();
                        Console.WriteLine("Setting was saved");
                    }*/

                    #endregion

                    #region PlayGunshot or Not PG *NOT ON HELP*

                    /*else if (scannedText == "pg")
                    {
                        Console.WriteLine();
                        Console.WriteLine("Turn On / Keep On 'PLAY GUNSHOT'?");
                        switch (GetYorN())
                        {
                            case "y":
                                Properties.Settings.Default.PlayDing = false;
                                Properties.Settings.Default.PlayGunshot = true;
                                myDingPlayer.Stream = Properties.Resources.gunshot;
                                Console.WriteLine();
                                Console.WriteLine("'PLAY GUNSHOT' is now ON");
                                break;
                            case "n":
                                Properties.Settings.Default.PlayDing = false;
                                Console.WriteLine();
                                Console.WriteLine("'PLAY GUNSHOT' is now OFF");
                                break;
                        }
                        Properties.Settings.Default.Save();
                        Console.WriteLine("Setting was saved");
                    }*/

                    #endregion

                    #region PlaySounds *NOT ON HELP*

                    else if (scannedText == "ps")
                    {
                        Console.WriteLine();
                        switch (GetSounds())
                        {
                            case 1:
                                Properties.Settings.Default.PlaySound = true;
                                Properties.Settings.Default.SoundFile = 1;
                                break;
                            case 2:
                                Properties.Settings.Default.PlaySound = true;
                                Properties.Settings.Default.SoundFile = 2;
                                break;
                            case 3:
                                Properties.Settings.Default.PlaySound = true;
                                Properties.Settings.Default.SoundFile = 3;
                                break;
                            case 4:
                                Properties.Settings.Default.PlaySound = true;
                                Properties.Settings.Default.SoundFile = 4;
                                break;
                            default:
                                Properties.Settings.Default.PlaySound = false;
                                break;
                        }
                        Properties.Settings.Default.Save();
                        Console.WriteLine("Setting was saved");
                        PlayDing();
                    }

                    #endregion

                    #region Command Not Found

                    else
                    {
                        PlayError();
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("{0} unknown, please try again", scannedText);
                        Console.ResetColor();
                    }

                    #endregion

                } while (!Exit);
        }


        #region Sounds

        void PlayError()
        {
            SoundPlayer myPlayer = new SoundPlayer();
            myPlayer.Stream = Properties.Resources.error;
            myPlayer.Play();
        }

        void PlayFinish()
        {
            //SoundPlayer myPlayer = new SoundPlayer();
            //myPlayer.Stream = Properties.Resources.orderdone;
            //myPlayer.Play();
        }

        void PlayDing()
        {
            PlayDing(false);
        }

        void PlayDing(bool finished)
        {
            if(Properties.Settings.Default.PlaySound)
            {
                switch (Properties.Settings.Default.SoundFile)
                {
                    case 1:
                        myDingPlayer.Stream = Properties.Resources.ding;
                        break;
                    case 2:
                        myDingPlayer.Stream = Properties.Resources.gunshot;
                        break;
                    case 3:
                        myDingPlayer.Stream = Properties.Resources.speechon;
                        break;
                    case 4:
                        myDingPlayer.Stream = Properties.Resources.windef;
                        break;
                    default:
                        break;
                }
                if(finished)
                    myDingPlayer.PlaySync();
                else
                    myDingPlayer.Play();
            }
        }

        #endregion


        #region Private methods

        void StartOrder(string orderNumber)
        {
            myOrder = new Order(orderNumber);
            currentBoxNumber = 1;
            if (Order.GetStatus(orderNumber) == 1)
            {
                Order.SetStatus2(orderNumber);
            }
            //myOrder.OrderChanged += new Order.OrderChangedHandler(myOrder_OrderChanged);
            myOrder.OnOrderFinished += new Order.OrderFinishedHandler(myOrder_OnOrderFinished);
            myOrder.OnUPCScanned += new Order.UPCScannedHandler(myOrder_OnUPCScanned);
            myOrder.OnNewBox += new Order.NewBoxHandler(myOrder_OnNewBox);
            myOrder.OnOrderKilled += new Order.OrderKilledHandler(myOrder_OnOrderKilled);
        }

        string GetYorN()
        {
            string yn;
            do
            {
                Console.Write("Y or N >> ");
                yn = Console.ReadLine().ToLower();
            } while(!yn.Equals("y") && !yn.Equals("n"));
            return yn;
        }

        int GetSounds()
        {
            string input;
            int num = -1;
            do
            {
                Console.WriteLine("0 - No sound");
                Console.WriteLine("1 - Ding");
                Console.WriteLine("2 - Gunshot");
                Console.WriteLine("3 - SoSound");
                Console.WriteLine("4 - Win default");
                Console.Write(">> ");
                input = Console.ReadLine().ToLower();
                try
                {
                    num = int.Parse(input);
                }
                catch (Exception)
                {
                    //throw;
                }
            } while (num < 0 && num >3);
            return num;
        }

        void WeighBoxes()
        {
            foreach(int i in myOrder.Box.Keys)
            {
                float weight;
                string typedWeight;
                bool okToGo;

                Console.WriteLine();
                Console.WriteLine("Please enter the weight of box {0}", i);
                do
                {
                    okToGo = false;

                    Console.Write(">> ");
                    typedWeight = Console.ReadLine();
                    if(float.TryParse(typedWeight, out weight))
                    {
                        if(weight > 0 && weight < 100)
                        {
                            Order.SetBoxWeight(myOrder.ToString(), i, weight);
                            okToGo = true;
                            continue;
                        }
                    }
                    Console.WriteLine();
                    Console.WriteLine("Please enter a number between 0 and 100");
                } while(!okToGo);
            }
        }

        void ShowHelp()
        {
            StringBuilder helpText = new StringBuilder();
            helpText.AppendLine("#######################################");
            helpText.AppendLine("#                                      #");
            helpText.AppendLine("# H  - Shows this help                 #");
            helpText.AppendLine("# NB - Starts a new box                #");
            helpText.AppendLine("# ECB- Empty current box to start over #");
            helpText.AppendLine("# R  - Releases current order          #");
            helpText.AppendLine("# P  - Prints missing item pick sheet  #");
            helpText.AppendLine("# L  - Reprint box labels for WalMart  #");
            helpText.AppendLine("#      or Michaels CA                  #");
            helpText.AppendLine("# W  - Reweigh boxes for Michaels CA   #");
            helpText.AppendLine("# X  - Exit current order              #");
            helpText.AppendLine("#                                      #");
            helpText.AppendLine("########################################");

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(helpText.ToString());
            Console.ResetColor();
        }

        #endregion


        #region Events

        /* NOT USED ANYMORE
        void closeOrderTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            int temp = DateTime.Now.DayOfYear;
            if (temp != currDayOfYear)
            {
                try
                {
                    Console.WriteLine();
                    Console.WriteLine("Running cleanup... Please wait!");
                    Shipments.Close();
                    Console.WriteLine("Ready");
                    Console.WriteLine();
                    Console.Write(">> ");
                }
                catch (Exception)
                {
                    // Do something here
                }
                finally
                {
                    currDayOfYear = DateTime.Now.DayOfYear;
                }
            }
        }
        */

        void myOrder_OnUPCScanned(object sender, Order.UPCScannedEventArgs e)
        {
            PlayDing(e.OrderFinished);
            Console.WriteLine();
            Console.WriteLine("Scanned {0} {1} for box {2} - Total Scanned:{3}  Ordered:{4}", e.ConversionFactor, e.UPCNumber, e.BoxNumber, e.ScannedQTY, e.OrderedQTY);
        }

        void myOrder_OnNewBox(object sender, int boxNumber)
        {
            Cousin.ASN.ASN myASN = new Cousin.ASN.ASN();
            switch (Order.GetCustomer(myOrder.ToString()))
            {
                case Order.Customer.Joann:
                    Console.WriteLine("Joann's labels will print when done auditing");
                    break;
                case Order.Customer.Acmoore:
                    Console.WriteLine("Acmoore's labels will print when done auditing");
                    break;
                case Order.Customer.WalmartUS:
                    if (boxNumber > 1)
                    {
                        myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.WalMart, myOrder.ToString(), boxNumber);
                    }
                    break;
                case Order.Customer.WalmartCA:
                    if (boxNumber > 1)
                    {
                        myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.WalmartCA, myOrder.ToString(), boxNumber);
                    }
                    break;
                case Order.Customer.Meijer:
                    myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.Meijer, myOrder.ToString(), boxNumber, lastUpcScanned);
                    break;
                case Order.Customer.Hancock:
                    myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.Hancock, myOrder.ToString(), boxNumber, lastUpcScanned);
                    break;
                case Order.Customer.Amazon:
                case Order.Customer.AmazonCanada: //added by Jason 4-22-13
                    myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.Amazon, myOrder.ToString(), boxNumber, lastUpcScanned);
                    break;
                default:
                    break;
            }
        }

        void myOrder_OnOrderFinished(object sender, EventArgs e)
        {
            switch(Order.GetCustomer(myOrder.ToString()))
            {
                    //Added MichaelsUS on Aug 12 2010 just copied canada code below
                case Order.Customer.MichaelsUS:

                    WeighBoxes();

                    if (!myOrder.ToString().Equals("900000001"))
                    {
                        try
                        {
                            Cousin.ASN.ASN myASN = new Cousin.ASN.ASN();
                            int numBoxs = myOrder.GetNextBoxNumber() - 1;
                            myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.MichaelsCA, myOrder.ToString(), numBoxs);
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine();
                            Console.WriteLine(err.Message.ToString());
                            Console.WriteLine();
                        }
                    }
                    break;
                case Order.Customer.MichaelsCA:

                    WeighBoxes();

                    if (!myOrder.ToString().Equals("900000001"))
                    {
                        try
                        {
                            Cousin.ASN.ASN myASN = new Cousin.ASN.ASN();
                            int numBoxs = myOrder.GetNextBoxNumber() - 1;
                            myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.MichaelsCA, myOrder.ToString(), numBoxs);
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine();
                            Console.WriteLine(err.Message.ToString());
                            Console.WriteLine();
                        }
                    }
                    break;
                case Order.Customer.Joann:

                    WeighBoxes();

                    try
                    {
                        Cousin.ASN.ASN myASN = new Cousin.ASN.ASN();
                        int numBoxs = myOrder.GetNextBoxNumber() - 1;

                        myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.Joann, myOrder.ToString(), numBoxs);

                        //for (int i = 1; i < myOrder.GetNextBoxNumber(); i++)
                        //{
                        //    myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.Joann, myOrder.ToString(), i);
                        //}
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine();
                        Console.WriteLine(err.Message.ToString());
                        Console.WriteLine();
                    }
                    break;
                case Order.Customer.Acmoore:

                    WeighBoxes();

                    try
                    {
                        Cousin.ASN.ASN myASN = new Cousin.ASN.ASN();
                        int numBoxs = myOrder.GetNextBoxNumber() - 1;

                        //*****uncomment this when going live
                        myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.Acmoore, myOrder.ToString(), numBoxs);

                        //for (int i = 1; i < myOrder.GetNextBoxNumber(); i++)
                        //{
                        //    myASN.PrintBarcodeLabels(Cousin.ASN.ASN.Customer.Acmoore, myOrder.ToString(), i);
                        //}
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine();
                        Console.WriteLine(err.Message.ToString());
                        Console.WriteLine();
                    }
                    break;
                default:
                    break;
            }

            PlayFinish();
            //Console.Clear();
            Console.WriteLine();
            Console.WriteLine("{0} is now complete.", myOrder.ToString());
            Console.WriteLine("You may begin a new order.");
            Console.WriteLine();
            myOrder = null;
        }

        void Order_OnMySqlError(object sender, Order.ErrorEventArgs e)
        {
            PlayError();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(e.Message.ToString());
            Console.ResetColor();
        }

        void Order_OnError(object sender, Order.ErrorEventArgs e)
        {
            PlayError();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(e.Message.ToString());
            Console.ResetColor();
        }

        void myPrinter_PrintPage(object sender, PrintPageEventArgs e)
        {
            Font printFont = new Font("Courier New", 10);
            e.Graphics.DrawString(txtToPrint, printFont, Brushes.Black, 25, 25);
        }

        void myOrder_OnOrderKilled(object sender, EventArgs e)
        {
            Console.Clear();
            Console.WriteLine();
            Console.WriteLine("Order {0} canceled", myOrder.ToString());
            Console.WriteLine("You may begin a new order.");
            Console.WriteLine();
            myOrder = null;
        }

        #endregion


        public void Dispose()
        {
            if (!this.disposing)
            {
                this.disposing = true;
                myDingPlayer.Dispose();
                myPrinter.Dispose();
            }
        }
    }
}
