using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanicballCore
{
    public enum MessageTypes : byte
    {
        Discover, Validate, Connect, Disconnect, PlayerMovement, Match
    }
}
