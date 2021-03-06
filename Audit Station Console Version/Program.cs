using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Cousin.Audit;



namespace Audit_Station_Console_Version
{
  class Program
  {
    static void Main(string[] args)
    {
      try
      {
        Audit myAudit = new Audit();
        myAudit.Run();
      }
      catch (Exception e)
      {
        MyErrorLogger mel = new MyErrorLogger();
        mel.ErrorLog(e.Message);
        Console.WriteLine("{0} Exception caught.", e);
        Console.WriteLine("{0} Exception caught.", e.Message);
        Console.WriteLine("{0} Exception caught.", e.ToString());
      }
    }
  }
}
