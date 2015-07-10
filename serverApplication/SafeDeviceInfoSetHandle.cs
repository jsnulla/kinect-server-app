using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace serverApplication
{
    public class SafeDeviceInfoSetHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeDeviceInfoSetHandle()
            : base(true)
        {
            // Non
        }

        protected override bool ReleaseHandle()
        {
            return Win32.SetupDiDestroyDeviceInfoList(this.handle);
        }
    }
}
