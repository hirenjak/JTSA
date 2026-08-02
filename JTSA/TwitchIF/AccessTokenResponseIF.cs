using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Forms.TwitchIF
{
    public class AccessTokenResponseIF
    {
        public int expiresIn { get; set; }
        public int interval { get; set; }
        public required string refreshToken { get; set; }
        public required string accessToken { get; set; }
    }
}
