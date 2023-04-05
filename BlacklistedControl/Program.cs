using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Suprema;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace BSDemo
{
    class Program : UnitTest
    {
        private BlacklistedControl ac = new BlacklistedControl();       

        protected override void runImpl(UInt32 deviceID)
        {
            ac.execute(sdkContext, deviceID, true);
        }

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Title = "Test for blacklisted user control";
            program.run();
        }
    }
}
