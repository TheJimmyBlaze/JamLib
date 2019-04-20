﻿using JamLib.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ExampleClient.Domain
{
    public struct DisplayableAccount
    {
        public Account Account { get; set; }
        public DisplayableAccount(Account account)
        {
            Account = account;
        }

        public string Initial
        {
            get { return Account?.Username.Substring(0, 1); }
            set { }
        }

        public Brush Colour
        {
            get
            {
                if (Account == null)
                    return null;

                Random random = new Random(Account.AccountID.GetHashCode());
                int r = (int)(random.NextDouble() * 100) + 100;
                int g = (int)(random.NextDouble() * 100) + 100;
                int b = (int)(random.NextDouble() * 100) + 100;

                return new SolidColorBrush(Color.FromArgb(255, (byte)r, (byte)g, (byte)b));
            }
            set { }
        }
    }
}
