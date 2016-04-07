using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Audit_Station_Console_Version
{
    class MyErrorLogger
    {
        #region Error Log
        public void ErrorLog(string Message)
        {
            StreamWriter sw = null;

            try
            {
                string sLogFormat = DateTime.Now.ToShortDateString().ToString() + " " + DateTime.Now.ToLongTimeString().ToString() + " ==> ";
                string sPathName = @"C:\";

                string sYear = DateTime.Now.Year.ToString();
                string sMonth = DateTime.Now.Month.ToString();
                string sDay = DateTime.Now.Day.ToString();

                string sErrorTime = sDay + "-" + sMonth + "-" + sYear;

                sw = new StreamWriter(sPathName + "SMSapplication_ErrorLog_" + sErrorTime + ".txt", true);
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(sLogFormat + Message);
                Console.ResetColor();
                sw.WriteLine(sLogFormat + Message);
                sw.Flush();

            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(ex.ToString());
                Console.ResetColor();
                //Would have tried to right to error log if there was an exception thrown for writing to the error log... DUH! This can't work so above code writes to console instead
                //ErrorLog(ex.ToString());
            }
            finally
            {
                if (sw != null)
                {
                    sw.Dispose();
                    sw.Close();
                }
            }


        }
        #endregion
    }
}
